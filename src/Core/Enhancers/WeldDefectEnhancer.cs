using OpenCvSharp;
using NLog;
using System;

namespace ImageAnalysisTool.Core.Enhancers
{
    /// <summary>
    /// 专门针对焊缝缺陷检测的图像增强器
    /// 优化细微线性缺陷(4-5像素)的检测效果
    /// </summary>
    public static class WeldDefectEnhancer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 焊缝缺陷检测专用增强
        /// </summary>
        /// <param name="input">输入的16位灰度图像</param>
        /// <param name="mode">检测模式：Fine(细微缺陷), Standard(标准), Strong(强增强)</param>
        /// <returns>增强后的图像</returns>
        public static Mat EnhanceWeldDefects(Mat input, WeldDetectionMode mode = WeldDetectionMode.Fine)
        {
            if (input == null || input.Empty())
                throw new ArgumentException("输入图像不能为空");

            logger.Info($"开始焊缝缺陷检测增强 - 模式: {mode}");

            try
            {
                // 1. 创建焊缝区域掩码
                Mat weldMask = CreateWeldROIMask(input);

                // 2. 根据模式选择参数
                var parameters = GetWeldParameters(mode);
                logger.Debug($"使用参数 - Retinex强度: {parameters.RetinexStrength}, 对比度: {parameters.ContrastStrength}");

                // 3. 应用多尺度焊缝Retinex增强
                Mat retinexResult = ApplyWeldRetinex(input, parameters);

                // 4. 应用方向性边缘增强(专门检测线性缺陷)
                Mat edgeEnhanced = ApplyDirectionalEdgeEnhancement(retinexResult, parameters.EdgeStrength);

                // 5. 焊缝区域自适应对比度增强
                Mat contrastResult = ApplyWeldContrastEnhancement(edgeEnhanced, parameters.ContrastStrength);

                // 6. 应用焊缝掩码 - 只处理焊缝区域
                Mat finalResult = ApplyWeldMask(input, contrastResult, weldMask);

                // 清理资源
                weldMask.Dispose();
                retinexResult.Dispose();
                edgeEnhanced.Dispose();
                contrastResult.Dispose();

                logger.Info("焊缝缺陷检测增强完成");
                return finalResult;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "焊缝缺陷检测增强失败");
                throw;
            }
        }

        /// <summary>
        /// 创建焊缝区域掩码 - 基于灰度分布特征
        /// </summary>
        private static Mat CreateWeldROIMask(Mat input)
        {
            try
            {
                // 使用双阈值方法检测焊缝区域
                Mat binary1 = new Mat();
                Mat binary2 = new Mat();
                
                // 第一个阈值：检测低灰度区域(焊缝和热影响区)
                double threshold1 = Cv2.Threshold(input, binary1, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                
                // 第二个阈值：检测中等灰度区域
                double threshold2 = threshold1 * 1.2;
                Cv2.Threshold(input, binary2, threshold2, 255, ThresholdTypes.Binary);
                
                logger.Debug($"焊缝掩码创建 - 低阈值: {threshold1:F0}, 高阈值: {threshold2:F0}");

                // 组合两个阈值结果，创建焊缝区域掩码
                Mat weldMask = new Mat();
                Cv2.BitwiseOr(binary1, binary2, weldMask);
                Cv2.BitwiseNot(weldMask, weldMask); // 反转，焊缝区域为白色

                // 形态学操作优化掩码
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
                Cv2.MorphologyEx(weldMask, weldMask, MorphTypes.Close, kernel);
                Cv2.MorphologyEx(weldMask, weldMask, MorphTypes.Open, kernel);

                // 计算焊缝区域占比
                int weldPixels = Cv2.CountNonZero(weldMask);
                double weldRatio = (double)weldPixels / (input.Width * input.Height) * 100;
                logger.Debug($"焊缝区域占比: {weldRatio:F1}%");

                binary1.Dispose();
                binary2.Dispose();
                kernel.Dispose();

                return weldMask;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "创建焊缝掩码失败");
                throw;
            }
        }

        /// <summary>
        /// 应用专门针对焊缝的多尺度Retinex增强
        /// </summary>
        private static Mat ApplyWeldRetinex(Mat input, WeldParameters parameters)
        {
            try
            {
                Mat floatInput = new Mat();
                input.ConvertTo(floatInput, MatType.CV_32F);

                // 添加小常数避免log(0)
                Cv2.Add(floatInput, Scalar.All(1.0), floatInput);
                
                // 计算对数
                Mat logInput = new Mat();
                Cv2.Log(floatInput, logInput);

                Mat msr = Mat.Zeros(input.Size(), MatType.CV_32F);

                // 焊缝专用的多尺度参数
                double[] weldScales = parameters.Scales;
                double[] weights = { 0.5, 0.3, 0.2 }; // 更重视小尺度

                for (int i = 0; i < weldScales.Length; i++)
                {
                    Mat blurred = new Mat();
                    Mat logBlurred = new Mat();
                    Mat difference = new Mat();

                    // 高斯模糊
                    Cv2.GaussianBlur(floatInput, blurred, new Size(0, 0), weldScales[i], 0, BorderTypes.Reflect);
                    
                    // 计算对数
                    Cv2.Log(blurred, logBlurred);
                    
                    // 计算差值并加权
                    Cv2.Subtract(logInput, logBlurred, difference);
                    Cv2.AddWeighted(msr, 1.0, difference, weights[i], 0, msr);

                    blurred.Dispose();
                    logBlurred.Dispose();
                    difference.Dispose();
                }

                // 应用增强强度
                Cv2.Multiply(msr, Scalar.All(parameters.RetinexStrength), msr);

                // 转换回原始范围并缩放
                Mat enhanced = new Mat();
                Cv2.Exp(msr, enhanced);
                
                // 智能缩放到合理范围 - 添加安全检查
                Scalar originalMean = Cv2.Mean(floatInput);
                Scalar enhancedMean = Cv2.Mean(enhanced);

                // 防止除零错误和异常值
                double scaleFactor = 1.0;
                if (enhancedMean.Val0 > 1e-6 && originalMean.Val0 > 1e-6)
                {
                    scaleFactor = originalMean.Val0 / enhancedMean.Val0;
                    // 限制缩放因子在合理范围内
                    scaleFactor = Math.Max(0.1, Math.Min(10.0, scaleFactor));
                }

                Cv2.Multiply(enhanced, Scalar.All(scaleFactor), enhanced);

                // 限制范围
                Cv2.Max(enhanced, Scalar.All(0), enhanced);
                Cv2.Min(enhanced, Scalar.All(65535), enhanced);

                Mat result = new Mat();
                enhanced.ConvertTo(result, MatType.CV_16U);

                // 清理资源
                floatInput.Dispose();
                logInput.Dispose();
                msr.Dispose();
                enhanced.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "焊缝Retinex增强失败");
                throw;
            }
        }

        /// <summary>
        /// 方向性边缘增强 - 专门检测线性缺陷
        /// </summary>
        private static Mat ApplyDirectionalEdgeEnhancement(Mat input, double strength)
        {
            try
            {
                Mat floatInput = new Mat();
                input.ConvertTo(floatInput, MatType.CV_32F);

                // Sobel边缘检测 - 检测不同方向的线性特征
                Mat sobelX = new Mat();
                Mat sobelY = new Mat();
                Mat sobelMag = new Mat();

                Cv2.Sobel(floatInput, sobelX, MatType.CV_32F, 1, 0, 3);
                Cv2.Sobel(floatInput, sobelY, MatType.CV_32F, 0, 1, 3);
                
                // 计算梯度幅值
                Cv2.Magnitude(sobelX, sobelY, sobelMag);

                // 应用边缘增强
                Mat enhanced = new Mat();
                Cv2.AddWeighted(floatInput, 1.0, sobelMag, strength, 0, enhanced);

                // 限制范围并转换回16位
                Cv2.Max(enhanced, Scalar.All(0), enhanced);
                Cv2.Min(enhanced, Scalar.All(65535), enhanced);

                Mat result = new Mat();
                enhanced.ConvertTo(result, MatType.CV_16U);

                // 清理资源
                floatInput.Dispose();
                sobelX.Dispose();
                sobelY.Dispose();
                sobelMag.Dispose();
                enhanced.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "方向性边缘增强失败");
                throw;
            }
        }

        /// <summary>
        /// 焊缝区域自适应对比度增强
        /// </summary>
        private static Mat ApplyWeldContrastEnhancement(Mat input, double strength)
        {
            try
            {
                // 使用小半径的局部对比度增强，保留细节
                Mat blurred = new Mat();
                Cv2.GaussianBlur(input, blurred, new Size(0, 0), 0.8, 0, BorderTypes.Reflect);

                Mat floatInput = new Mat();
                Mat floatBlurred = new Mat();
                input.ConvertTo(floatInput, MatType.CV_32F);
                blurred.ConvertTo(floatBlurred, MatType.CV_32F);

                // 计算细节层
                Mat detail = new Mat();
                Cv2.Subtract(floatInput, floatBlurred, detail);
                Cv2.Multiply(detail, Scalar.All(strength), detail);

                // 重新组合
                Mat result = new Mat();
                Cv2.Add(floatInput, detail, result);

                // 限制范围
                Cv2.Max(result, Scalar.All(0), result);
                Cv2.Min(result, Scalar.All(65535), result);

                Mat finalResult = new Mat();
                result.ConvertTo(finalResult, MatType.CV_16U);

                // 清理资源
                blurred.Dispose();
                floatInput.Dispose();
                floatBlurred.Dispose();
                detail.Dispose();
                result.Dispose();

                return finalResult;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "焊缝对比度增强失败");
                throw;
            }
        }

        /// <summary>
        /// 应用焊缝掩码
        /// </summary>
        private static Mat ApplyWeldMask(Mat original, Mat enhanced, Mat mask)
        {
            try
            {
                Mat result = new Mat();
                Mat maskFloat = new Mat();
                mask.ConvertTo(maskFloat, MatType.CV_32F, 1.0/255.0);

                Mat originalFloat = new Mat();
                Mat enhancedFloat = new Mat();
                original.ConvertTo(originalFloat, MatType.CV_32F);
                enhanced.ConvertTo(enhancedFloat, MatType.CV_32F);

                // 混合公式: result = enhanced * mask + original * (1-mask)
                Mat maskInv = new Mat();
                Cv2.Subtract(Scalar.All(1.0), maskFloat, maskInv);

                Mat maskedEnhanced = new Mat();
                Mat maskedOriginal = new Mat();
                
                Cv2.Multiply(enhancedFloat, maskFloat, maskedEnhanced);
                Cv2.Multiply(originalFloat, maskInv, maskedOriginal);
                
                Mat resultFloat = new Mat();
                Cv2.Add(maskedEnhanced, maskedOriginal, resultFloat);
                
                resultFloat.ConvertTo(result, MatType.CV_16U);

                // 清理资源
                maskFloat.Dispose();
                originalFloat.Dispose();
                enhancedFloat.Dispose();
                maskInv.Dispose();
                maskedEnhanced.Dispose();
                maskedOriginal.Dispose();
                resultFloat.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "应用焊缝掩码失败");
                throw;
            }
        }

        /// <summary>
        /// 根据检测模式获取参数
        /// </summary>
        private static WeldParameters GetWeldParameters(WeldDetectionMode mode)
        {
            return mode switch
            {
                WeldDetectionMode.Fine => new WeldParameters
                {
                    RetinexStrength = 0.15,
                    ContrastStrength = 0.8,
                    EdgeStrength = 0.3,
                    Scales = new double[] { 1.5, 6, 25 }  // 专注细微缺陷
                },
                WeldDetectionMode.Standard => new WeldParameters
                {
                    RetinexStrength = 0.25,
                    ContrastStrength = 1.2,
                    EdgeStrength = 0.5,
                    Scales = new double[] { 2, 8, 30 }    // 平衡检测
                },
                WeldDetectionMode.Strong => new WeldParameters
                {
                    RetinexStrength = 0.4,
                    ContrastStrength = 1.8,
                    EdgeStrength = 0.8,
                    Scales = new double[] { 3, 10, 40 }   // 强增强
                },
                _ => throw new ArgumentException($"未知的检测模式: {mode}")
            };
        }
    }

    /// <summary>
    /// 焊缝检测模式
    /// </summary>
    public enum WeldDetectionMode
    {
        /// <summary>细微缺陷检测模式 - 专门检测4-5像素的细线缺陷</summary>
        Fine,
        /// <summary>标准检测模式 - 平衡的缺陷检测</summary>
        Standard,
        /// <summary>强增强模式 - 检测明显缺陷</summary>
        Strong
    }

    /// <summary>
    /// 焊缝检测参数
    /// </summary>
    internal class WeldParameters
    {
        public double RetinexStrength { get; set; }
        public double ContrastStrength { get; set; }
        public double EdgeStrength { get; set; }
        public double[] Scales { get; set; }
    }
}
