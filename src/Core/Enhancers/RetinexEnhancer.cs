using OpenCvSharp;
using System;
using NLog;

namespace ImageAnalysisTool.Core.Enhancers
{
    /// <summary>
    /// 基于多尺度Retinex + 局部对比度增强的图像增强器
    /// 避免平滑效果，保持图像锐利感
    /// </summary>
    public class RetinexEnhancer
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// 主要增强方法：基于ROI掩码的多尺度Retinex + 局部对比度增强
        /// 使用OTSU动态阈值自动检测工件区域，只对工件区域进行增强，过曝区域保持原值
        /// </summary>
        /// <param name="input">输入图像 (16位灰度)</param>
        /// <param name="retinexStrength">Retinex增强强度 (0.1-2.0)</param>
        /// <param name="localContrastStrength">局部对比度增强强度 (0.1-2.0)</param>
        /// <param name="gammaCorrection">Gamma校正值 (0.5-2.0)</param>
        /// <param name="useContrastProtection">是否使用对比度保护 (推荐true)</param>
        /// <returns>增强后的图像</returns>
        public static Mat EnhanceImage(Mat input, double retinexStrength = 1.5,
            double localContrastStrength = 2.0, double gammaCorrection = 1.2, bool useContrastProtection = true)
        {
            if (input == null || input.Empty())
                return new Mat();

            try
            {
                // 0. 创建ROI掩码 - 使用OTSU动态阈值检测工件区域
                Mat roiMask = CreateROIMask(input);

                Mat finalResult;

                if (useContrastProtection)
                {
                    // 新的对比度保护增强路径（优化版：控制噪声和光晕）
                    logger.Debug("使用对比度保护增强模式（噪声和光晕控制版）");

                    // 使用优化后的参数：大幅降低增强强度，增加噪声和光晕控制
                    finalResult = ContrastProtectedEnhancer.EnhanceWithContrastProtection(
                        input, roiMask,
                        brightnessGain: 2.5,        // 从3.6降到2.5，减少过度增强
                        contrastPreservation: 0.6,  // 从0.75降到0.6，减少对比度增强
                        noiseControl: 0.7,          // 新增：70%噪声控制
                        haloSuppression: 0.8);      // 新增：80%光晕抑制
                }
                else
                {
                    // 原有的增强路径
                    logger.Debug("使用传统增强模式");

                    // 1. 多尺度Retinex增强 (只对工件区域)
                    Mat retinexResult = ApplyMultiScaleRetinex(input, retinexStrength);

                    // 2. 局部对比度增强 (使用Unsharp Masking)
                    Mat contrastEnhanced = ApplyLocalContrastEnhancement(retinexResult, localContrastStrength);
                    retinexResult.Dispose();

                    // 3. 自适应Gamma校正
                    Mat gammaResult = ApplyAdaptiveGamma(contrastEnhanced, gammaCorrection);
                    contrastEnhanced.Dispose();

                    // 4. 专门的对比度增强 - 让暗部更暗，亮部更亮  2.2: 对比度增强倍数，可调整为1.5-3.0
                    Mat contrastResult = ApplyContrastEnhancement(gammaResult, 2.2);
                    gammaResult.Dispose();

                    // 5. 应用ROI掩码 - 只保留工件区域的增强结果，过曝区域使用原图
                    finalResult = ApplyROIMask(input, contrastResult, roiMask);
                    contrastResult.Dispose();
                }
                roiMask.Dispose();

                return finalResult;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "RetinexEnhancer Error");
                return input.Clone();
            }
        }

        /// <summary>
        /// 高级增强方法：提供更多控制选项，专门针对缺陷检测优化（优化版：控制噪声和光晕）
        /// </summary>
        /// <param name="input">输入图像 (16位灰度)</param>
        /// <param name="brightnessGain">亮度提升倍数 (2.0-4.0，推荐2.5)</param>
        /// <param name="contrastPreservation">对比度保护强度 (0.3-0.8，推荐0.6)</param>
        /// <param name="noiseControl">噪声控制强度 (0.5-1.0，推荐0.7)</param>
        /// <param name="haloSuppression">光晕抑制强度 (0.5-1.0，推荐0.8)</param>
        /// <param name="useLayeredEnhancement">是否使用分层增强</param>
        /// <returns>增强后的图像</returns>
        public static Mat EnhanceImageAdvanced(Mat input, double brightnessGain = 2.5,
            double contrastPreservation = 0.6, double noiseControl = 0.7, double haloSuppression = 0.8, bool useLayeredEnhancement = false)
        {
            if (input == null || input.Empty())
                return new Mat();

            try
            {
                logger.Debug($"高级增强（优化版） - 亮度增益: {brightnessGain:F1}x, 对比度保护: {contrastPreservation:F2}, 噪声控制: {noiseControl:F2}, 光晕抑制: {haloSuppression:F2}, 分层增强: {useLayeredEnhancement}");

                // 创建ROI掩码
                Mat roiMask = CreateROIMask(input);

                Mat result;
                if (useLayeredEnhancement)
                {
                    // 使用分层自适应增强
                    result = ContrastProtectedEnhancer.LayeredAdaptiveEnhancement(input, roiMask);
                }
                else
                {
                    // 使用对比度保护增强（优化版）
                    result = ContrastProtectedEnhancer.EnhanceWithContrastProtection(
                        input, roiMask, brightnessGain, contrastPreservation, noiseControl, haloSuppression);
                }

                roiMask.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "高级图像增强失败");
                return input.Clone();
            }
        }

        /// <summary>
        /// 创建ROI掩码 - 使用OTSU动态阈值自动检测工件区域
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <returns>ROI掩码，工件区域为255，过曝区域为0</returns>
        private static Mat CreateROIMask(Mat input)
        {
            try
            {
                // 使用OTSU阈值分割来区分工件区域和过曝区域
                Mat binary = new Mat();
                double threshold = Cv2.Threshold(input, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                logger.Debug($"ROI掩码创建 - OTSU阈值: {threshold:F0}");

                // 转换为8位掩码
                Mat mask8 = new Mat();
                binary.ConvertTo(mask8, MatType.CV_8U);

                // 创建工件区域掩码（反转二值图像，因为工件区域灰度值较低）
                Mat roiMask = new Mat();
                Cv2.BitwiseNot(mask8, roiMask);

                // 计算ROI区域像素数量
                int roiPixelCount = Cv2.CountNonZero(roiMask);
                int totalPixels = input.Rows * input.Cols;
                double roiRatio = (double)roiPixelCount / totalPixels * 100;
                logger.Debug($"ROI掩码创建 - ROI区域占比: {roiRatio:F1}%");

                // 形态学操作去除噪声
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
                Mat cleaned = new Mat();
                Cv2.MorphologyEx(roiMask, cleaned, MorphTypes.Close, kernel);
                Cv2.MorphologyEx(cleaned, roiMask, MorphTypes.Open, kernel);

                // 清理资源
                binary.Dispose();
                mask8.Dispose();
                cleaned.Dispose();
                kernel.Dispose();

                return roiMask;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "CreateROIMask Error");
                // 返回全白掩码（处理所有区域）
                return Mat.Ones(input.Size(), MatType.CV_8U) * 255;
            }
        }

        /// <summary>
        /// 应用ROI掩码 - 只保留工件区域的增强结果，过曝区域使用原图
        /// </summary>
        /// <param name="original">原始图像</param>
        /// <param name="enhanced">增强后图像</param>
        /// <param name="roiMask">ROI掩码</param>
        /// <returns>应用掩码后的最终结果</returns>
        private static Mat ApplyROIMask(Mat original, Mat enhanced, Mat roiMask)
        {
            try
            {
                Mat result = new Mat();

                // 将掩码转换为浮点型 (0.0-1.0)
                Mat maskFloat = new Mat();
                roiMask.ConvertTo(maskFloat, MatType.CV_32F, 1.0/255.0);

                // 转换图像为浮点型
                Mat originalFloat = new Mat();
                Mat enhancedFloat = new Mat();
                original.ConvertTo(originalFloat, MatType.CV_32F);
                enhanced.ConvertTo(enhancedFloat, MatType.CV_32F);

                // 应用掩码混合：result = enhanced * mask + original * (1 - mask)
                Mat resultFloat = new Mat();
                Mat temp1 = new Mat();
                Mat temp2 = new Mat();
                Mat inverseMask = new Mat();

                Cv2.Subtract(Scalar.All(1.0), maskFloat, inverseMask);
                Cv2.Multiply(enhancedFloat, maskFloat, temp1);
                Cv2.Multiply(originalFloat, inverseMask, temp2);
                Cv2.Add(temp1, temp2, resultFloat);

                // 边界值保护：确保像素值在有效范围内
                Cv2.Threshold(resultFloat, resultFloat, 0, 0, ThresholdTypes.Tozero); // 确保非负
                Cv2.Threshold(resultFloat, resultFloat, 65535, 65535, ThresholdTypes.Trunc); // 限制上限

                // 转换回16位
                resultFloat.ConvertTo(result, MatType.CV_16U);

                // 清理资源
                maskFloat.Dispose();
                originalFloat.Dispose();
                enhancedFloat.Dispose();
                resultFloat.Dispose();
                temp1.Dispose();
                temp2.Dispose();
                inverseMask.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ApplyROIMask Error");
                return enhanced.Clone();
            }
        }

        /// <summary>
        /// 多尺度Retinex算法 - 保持细节的对比度增强
        /// </summary>
        private static Mat ApplyMultiScaleRetinex(Mat input, double strength)
        {
            Mat result = new Mat();
            input.ConvertTo(result, MatType.CV_32F);

            // 多个尺度的高斯核
            /**
            小尺度 (15.0): 控制细节增强
            中尺度 (80.0): 控制中等结构增强
            大尺度 (250.0): 控制整体对比度
            */
            double[] scales = { 5, 20, 80 };
            double[] weights = { 1.0/3.0, 1.0/3.0, 1.0/3.0 };

            Mat msr = Mat.Zeros(result.Size(), MatType.CV_32F);

            // 预处理：添加安全值避免除零和负数
            Mat safeInput = new Mat();
            Cv2.Add(result, Scalar.All(100.0), safeInput);

            for (int i = 0; i < scales.Length; i++)
            {
                Mat blurred = new Mat();
                Cv2.GaussianBlur(safeInput, blurred, new OpenCvSharp.Size(0, 0), scales[i], 0, BorderTypes.Reflect);

                // 添加安全值到模糊图像
                Cv2.Add(blurred, Scalar.All(100.0), blurred);

                Mat logResult = new Mat();
                Mat logBlurred = new Mat();
                Cv2.Log(safeInput, logResult);
                Cv2.Log(blurred, logBlurred);

                Mat singleScale = new Mat();
                Cv2.Subtract(logResult, logBlurred, singleScale);

                Mat weightedScale = new Mat();
                Cv2.Multiply(singleScale, Scalar.All(weights[i]), weightedScale);

                Cv2.Add(msr, weightedScale, msr);

                // 清理资源
                blurred.Dispose();
                logResult.Dispose();
                logBlurred.Dispose();
                singleScale.Dispose();
                weightedScale.Dispose();
            }

            // 应用增强强度
            Cv2.Multiply(msr, Scalar.All(strength), msr);

            // 转换回原始范围并缩放到合理范围
            Mat enhanced = new Mat();
            Cv2.Exp(msr, enhanced);

            // 调试：检查Exp后的值
            Scalar expMean = Cv2.Mean(enhanced);
            logger.Debug($"Exp后平均值: {expMean.Val0:F2}");

            // 将结果缩放到原始图像的范围
            // 计算原始图像的平均值作为参考
            Scalar originalMean = Cv2.Mean(safeInput);
            double scaleFactor = originalMean.Val0 / expMean.Val0;

            // 应用缩放
            Cv2.Multiply(enhanced, Scalar.All(scaleFactor), enhanced);

            // 调试：检查缩放后的值
            Scalar scaledMean = Cv2.Mean(enhanced);
            logger.Debug($"缩放后平均值: {scaledMean.Val0:F2}, 缩放因子: {scaleFactor:F2}");

            // 限制范围
            Mat clipped = new Mat();
            enhanced.CopyTo(clipped);

            // 确保像素值在有效范围内
            Cv2.Max(clipped, Scalar.All(0), clipped); // 确保非负
            Cv2.Min(clipped, Scalar.All(65535), clipped); // 限制上限

            // 调试：检查最终结果
            Scalar finalMean = Cv2.Mean(clipped);
            logger.Debug($"最终平均值: {finalMean.Val0:F2}");

            clipped.ConvertTo(result, MatType.CV_16U);

            // 清理资源
            safeInput.Dispose();
            msr.Dispose();
            enhanced.Dispose();
            clipped.Dispose();

            return result;
        }

        /// <summary>
        /// 局部对比度增强 - 使用Unsharp Masking保持锐利感
        /// </summary>
        private static Mat ApplyLocalContrastEnhancement(Mat input, double strength)
        {
            // 创建轻微模糊的版本
            Mat blurred = new Mat();
            Cv2.GaussianBlur(input, blurred, new Size(0, 0), 2.0, 0, BorderTypes.Reflect); // 小半径保持细节

            // 计算差值 (细节层)
            Mat detail = new Mat();
            input.ConvertTo(detail, MatType.CV_32F);
            Mat blurredFloat = new Mat();
            blurred.ConvertTo(blurredFloat, MatType.CV_32F);
            Cv2.Subtract(detail, blurredFloat, detail);

            // 增强细节
            Cv2.Multiply(detail, Scalar.All(strength), detail);

            // 重新组合
            Mat result = new Mat();
            Mat inputFloat = new Mat();
            input.ConvertTo(inputFloat, MatType.CV_32F);
            Cv2.Add(inputFloat, detail, result);

            // 转换回16位并限制范围
            Mat final = new Mat();
            result.ConvertTo(final, MatType.CV_16U);
            Cv2.Threshold(final, final, 65535, 65535, ThresholdTypes.Trunc);

            // 清理资源
            blurred.Dispose();
            detail.Dispose();
            blurredFloat.Dispose();
            inputFloat.Dispose();
            result.Dispose();

            return final;
        }

        /// <summary>
        /// 自适应Gamma校正
        /// </summary>
        private static Mat ApplyAdaptiveGamma(Mat input, double gamma)
        {
            Mat result = new Mat();
            Mat normalized = new Mat();

            // 归一化到0-1范围
            input.ConvertTo(normalized, MatType.CV_32F, 1.0/65535.0);

            // 应用Gamma校正
            Cv2.Pow(normalized, gamma, normalized);

            // 转换回16位
            normalized.ConvertTo(result, MatType.CV_16U, 65535.0);

            normalized.Dispose();
            return result;
        }

        /// <summary>
        /// 专门的对比度增强 - 让暗部更暗，亮部更亮
        /// </summary>
        private static Mat ApplyContrastEnhancement(Mat input, double contrastFactor)
        {
            Mat result = new Mat();

            // 转换为浮点型进行计算
            Mat inputFloat = new Mat();
            input.ConvertTo(inputFloat, MatType.CV_32F);

            // 计算图像的平均值作为中心点
            Scalar meanScalar = Cv2.Mean(inputFloat);
            double meanValue = meanScalar.Val0;

            // 应用对比度增强公式: output = mean + (input - mean) * contrastFactor
            Mat centered = new Mat();
            Cv2.Subtract(inputFloat, Scalar.All(meanValue), centered);

            Mat enhanced = new Mat();
            Cv2.Multiply(centered, Scalar.All(contrastFactor), enhanced);

            Mat final = new Mat();
            Cv2.Add(enhanced, Scalar.All(meanValue), final);

            // 限制到有效范围并转换回16位
            Cv2.Threshold(final, final, 0, 0, ThresholdTypes.Tozero); // 确保非负
            Cv2.Threshold(final, final, 65535, 65535, ThresholdTypes.Trunc); // 限制上限

            final.ConvertTo(result, MatType.CV_16U);

            // 清理资源
            inputFloat.Dispose();
            centered.Dispose();
            enhanced.Dispose();
            final.Dispose();

            return result;
        }
    }
}
