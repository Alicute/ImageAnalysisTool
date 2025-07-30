using System;
using OpenCvSharp;

namespace ImageAnalysisTool.Core.Enhancers
{
    /// <summary>
    /// 对比度保护增强器 - 在提升亮度的同时保护局部对比度
    /// </summary>
    public static class ContrastProtectedEnhancer
    {
        /// <summary>
        /// 对比度保护的ROI增强（优化版：控制噪声和光晕）
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="roiMask">ROI区域掩码</param>
        /// <param name="brightnessGain">亮度提升倍数</param>
        /// <param name="contrastPreservation">对比度保护强度 (0-1)</param>
        /// <param name="noiseControl">噪声控制强度 (0-1，越高越抑制噪声)</param>
        /// <param name="haloSuppression">光晕抑制强度 (0-1，越高越抑制光晕)</param>
        /// <returns>增强后的图像</returns>
        public static Mat EnhanceWithContrastProtection(Mat input, Mat roiMask, double brightnessGain = 2.5,
            double contrastPreservation = 0.6, double noiseControl = 0.7, double haloSuppression = 0.8)
        {
            if (input == null || roiMask == null)
                return input?.Clone();

            try
            {
                Mat result = input.Clone();

                // 1. 分离亮度和对比度处理
                Mat brightnessEnhanced = EnhanceBrightness(input, roiMask, brightnessGain);

                // 2. 噪声抑制处理（新增）
                Mat noiseReduced = ApplyNoiseReduction(brightnessEnhanced, roiMask, noiseControl);

                // 3. 保护局部对比度（降低强度）
                double adjustedPreservation = contrastPreservation * (1.0 - noiseControl * 0.3); // 噪声控制越强，对比度保护越弱
                Mat contrastProtected = PreserveLocalContrast(input, noiseReduced, roiMask, adjustedPreservation);

                // 4. 光晕抑制处理（新增）
                Mat haloSuppressed = SuppressHaloEffect(contrastProtected, roiMask, haloSuppression);

                // 5. 细线特征检测和保护（温和处理）
                Mat thinLineProtected = ProtectThinLineFeatures(input, haloSuppressed, roiMask, 0.5); // 降低细线增强强度

                // 6. 融合到结果图像
                thinLineProtected.CopyTo(result, roiMask);

                // 清理资源
                brightnessEnhanced.Dispose();
                noiseReduced.Dispose();
                contrastProtected.Dispose();
                haloSuppressed.Dispose();
                thinLineProtected.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"对比度保护增强失败: {ex.Message}");
                return input?.Clone();
            }
        }

        /// <summary>
        /// 增强亮度（保持原有的亮度提升效果）
        /// </summary>
        private static Mat EnhanceBrightness(Mat input, Mat roiMask, double gain)
        {
            try
            {
                Mat result = input.Clone();
                
                // 计算ROI区域的统计信息
                Scalar roiMean = Cv2.Mean(input, roiMask);
                double targetMean = roiMean.Val0 * gain;
                
                // 限制目标亮度，避免过曝
                targetMean = Math.Min(targetMean, 45000); // 16位图像的安全上限
                
                // 计算增强系数
                double enhancementFactor = targetMean / Math.Max(roiMean.Val0, 1.0);
                
                // 应用亮度增强
                Mat enhanced = new Mat();
                input.ConvertTo(enhanced, MatType.CV_16UC1, enhancementFactor, 0);
                
                // 只在ROI区域应用增强
                enhanced.CopyTo(result, roiMask);
                
                enhanced.Dispose();
                return result;
            }
            catch
            {
                return input.Clone();
            }
        }

        /// <summary>
        /// 保护局部对比度
        /// </summary>
        private static Mat PreserveLocalContrast(Mat original, Mat brightened, Mat roiMask, double preservationStrength)
        {
            try
            {
                // 1. 计算原图的局部对比度
                Mat originalContrast = CalculateLocalContrast(original, roiMask);
                
                // 2. 计算增强后的局部对比度
                Mat brightenedContrast = CalculateLocalContrast(brightened, roiMask);
                
                // 3. 计算对比度损失
                Mat contrastLoss = new Mat();
                Cv2.Subtract(originalContrast, brightenedContrast, contrastLoss);
                
                // 4. 补偿对比度损失
                Mat contrastCompensation = new Mat();
                contrastLoss.ConvertTo(contrastCompensation, MatType.CV_16UC1, preservationStrength, 0);
                
                // 5. 应用对比度补偿
                Mat result = new Mat();
                Cv2.Add(brightened, contrastCompensation, result);
                
                // 6. 限制像素值范围
                Cv2.Threshold(result, result, 65535, 65535, ThresholdTypes.Trunc);
                
                // 清理资源
                originalContrast.Dispose();
                brightenedContrast.Dispose();
                contrastLoss.Dispose();
                contrastCompensation.Dispose();
                
                return result;
            }
            catch
            {
                return brightened.Clone();
            }
        }

        /// <summary>
        /// 计算局部对比度
        /// </summary>
        private static Mat CalculateLocalContrast(Mat image, Mat mask)
        {
            try
            {
                // 使用局部标准差作为对比度度量
                Mat mean = new Mat();
                Mat sqrMean = new Mat();
                
                // 计算局部均值
                Cv2.BoxFilter(image, mean, MatType.CV_32F, new Size(5, 5));
                
                // 计算局部平方均值
                Mat squared = new Mat();
                Cv2.Multiply(image, image, squared, 1.0, MatType.CV_32F);
                Cv2.BoxFilter(squared, sqrMean, MatType.CV_32F, new Size(5, 5));
                
                // 计算局部方差
                Mat variance = new Mat();
                Mat meanSquared = new Mat();
                Cv2.Multiply(mean, mean, meanSquared);
                Cv2.Subtract(sqrMean, meanSquared, variance);
                
                // 计算局部标准差（对比度）
                Mat contrast = new Mat();
                Cv2.Sqrt(variance, contrast);
                
                // 转换回16位
                Mat result = new Mat();
                contrast.ConvertTo(result, MatType.CV_16UC1);
                
                // 清理资源
                mean.Dispose(); sqrMean.Dispose(); squared.Dispose();
                variance.Dispose(); meanSquared.Dispose(); contrast.Dispose();
                
                return result;
            }
            catch
            {
                return Mat.Zeros(image.Size(), MatType.CV_16UC1);
            }
        }

        /// <summary>
        /// 噪声抑制处理
        /// </summary>
        private static Mat ApplyNoiseReduction(Mat input, Mat roiMask, double noiseControl)
        {
            try
            {
                if (noiseControl <= 0) return input.Clone();

                // 使用双边滤波抑制噪声，同时保护边缘
                Mat result = new Mat();
                int d = (int)(5 * noiseControl); // 滤波器直径
                double sigmaColor = 50 * noiseControl; // 颜色空间标准差
                double sigmaSpace = 50 * noiseControl; // 坐标空间标准差

                // 转换为8位进行双边滤波
                Mat input8bit = new Mat();
                input.ConvertTo(input8bit, MatType.CV_8UC1, 255.0 / 65535.0);

                Mat filtered8bit = new Mat();
                Cv2.BilateralFilter(input8bit, filtered8bit, d, sigmaColor, sigmaSpace);

                // 转换回16位
                filtered8bit.ConvertTo(result, MatType.CV_16UC1, 65535.0 / 255.0);

                // 只在ROI区域应用噪声抑制
                Mat finalResult = input.Clone();
                result.CopyTo(finalResult, roiMask);

                // 清理资源
                input8bit.Dispose();
                filtered8bit.Dispose();
                result.Dispose();

                return finalResult;
            }
            catch
            {
                return input.Clone();
            }
        }

        /// <summary>
        /// 光晕抑制处理
        /// </summary>
        private static Mat SuppressHaloEffect(Mat input, Mat roiMask, double haloSuppression)
        {
            try
            {
                if (haloSuppression <= 0) return input.Clone();

                // 检测边缘区域
                Mat edges = DetectEdges(input);

                // 在边缘区域应用温和的平滑
                Mat smoothed = new Mat();
                int kernelSize = (int)(3 + haloSuppression * 4); // 根据抑制强度调整核大小
                Cv2.GaussianBlur(input, smoothed, new Size(kernelSize, kernelSize), haloSuppression);

                // 创建混合掩码：边缘区域使用平滑版本，其他区域保持原样
                Mat blendMask = new Mat();
                edges.ConvertTo(blendMask, MatType.CV_32F, haloSuppression / 255.0);

                // 混合原图和平滑图
                Mat result = new Mat();
                Mat inputFloat = new Mat();
                Mat smoothedFloat = new Mat();

                input.ConvertTo(inputFloat, MatType.CV_32F);
                smoothed.ConvertTo(smoothedFloat, MatType.CV_32F);

                // result = input * (1 - blendMask) + smoothed * blendMask
                Mat temp1 = new Mat();
                Mat temp2 = new Mat();
                Cv2.Multiply(inputFloat, Scalar.All(1.0) - blendMask, temp1);
                Cv2.Multiply(smoothedFloat, blendMask, temp2);
                Cv2.Add(temp1, temp2, result);

                // 转换回16位
                Mat result16bit = new Mat();
                result.ConvertTo(result16bit, MatType.CV_16UC1);

                // 只在ROI区域应用光晕抑制
                Mat finalResult = input.Clone();
                result16bit.CopyTo(finalResult, roiMask);

                // 清理资源
                edges.Dispose(); smoothed.Dispose(); blendMask.Dispose();
                inputFloat.Dispose(); smoothedFloat.Dispose(); result.Dispose();
                temp1.Dispose(); temp2.Dispose(); result16bit.Dispose();

                return finalResult;
            }
            catch
            {
                return input.Clone();
            }
        }

        /// <summary>
        /// 检测边缘区域
        /// </summary>
        private static Mat DetectEdges(Mat image)
        {
            try
            {
                // 转换为8位
                Mat image8bit = new Mat();
                image.ConvertTo(image8bit, MatType.CV_8UC1, 255.0 / 65535.0);

                // Canny边缘检测
                Mat edges = new Mat();
                Cv2.Canny(image8bit, edges, 50, 150);

                // 膨胀边缘以创建更大的抑制区域
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
                Mat dilated = new Mat();
                Cv2.Dilate(edges, dilated, kernel);

                image8bit.Dispose();
                edges.Dispose();
                kernel.Dispose();

                return dilated;
            }
            catch
            {
                return Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }
        }

        /// <summary>
        /// 保护细线特征（温和版本）
        /// </summary>
        private static Mat ProtectThinLineFeatures(Mat original, Mat enhanced, Mat roiMask, double protectionStrength = 1.0)
        {
            try
            {
                // 1. 检测细线特征
                Mat thinLines = DetectThinLines(original, roiMask);
                
                // 2. 在细线区域使用更保守的增强
                Mat result = enhanced.Clone();
                
                // 3. 对细线区域进行特殊处理
                Mat thinLineEnhanced = EnhanceThinLines(original, thinLines);
                
                // 4. 融合细线增强结果
                thinLineEnhanced.CopyTo(result, thinLines);
                
                thinLines.Dispose();
                thinLineEnhanced.Dispose();
                
                return result;
            }
            catch
            {
                return enhanced.Clone();
            }
        }

        /// <summary>
        /// 检测细线特征
        /// </summary>
        private static Mat DetectThinLines(Mat image, Mat roiMask)
        {
            try
            {
                // 转换为8位进行处理
                Mat image8bit = new Mat();
                image.ConvertTo(image8bit, MatType.CV_8UC1, 255.0 / 65535.0);
                
                // 使用形态学操作检测细线
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 1)); // 水平线
                Mat horizontal = new Mat();
                Cv2.MorphologyEx(image8bit, horizontal, MorphTypes.Open, kernel);
                
                kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 3)); // 垂直线
                Mat vertical = new Mat();
                Cv2.MorphologyEx(image8bit, vertical, MorphTypes.Open, kernel);
                
                // 合并水平和垂直线
                Mat lines = new Mat();
                Cv2.Add(horizontal, vertical, lines);
                
                // 阈值化
                Mat result = new Mat();
                Cv2.Threshold(lines, result, 30, 255, ThresholdTypes.Binary);
                
                // 只保留ROI区域内的细线
                Cv2.BitwiseAnd(result, roiMask, result);
                
                // 清理资源
                image8bit.Dispose(); kernel.Dispose();
                horizontal.Dispose(); vertical.Dispose(); lines.Dispose();
                
                return result;
            }
            catch
            {
                return Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }
        }

        /// <summary>
        /// 增强细线特征
        /// </summary>
        private static Mat EnhanceThinLines(Mat original, Mat thinLineMask)
        {
            try
            {
                Mat result = original.Clone();
                
                // 对细线区域应用更温和的增强
                Mat enhanced = new Mat();
                original.ConvertTo(enhanced, MatType.CV_16UC1, 2.0, 1000); // 较温和的增强
                
                // 只在细线区域应用
                enhanced.CopyTo(result, thinLineMask);
                
                enhanced.Dispose();
                return result;
            }
            catch
            {
                return original.Clone();
            }
        }

        /// <summary>
        /// 分层自适应增强（高级版本）
        /// </summary>
        public static Mat LayeredAdaptiveEnhancement(Mat input, Mat roiMask)
        {
            try
            {
                Mat result = input.Clone();
                
                // 1. 分离不同类型的区域
                Mat backgroundROI = DetectBackgroundInROI(input, roiMask);
                Mat detailROI = DetectDetailInROI(input, roiMask);
                Mat edgeROI = DetectEdgeInROI(input, roiMask);
                
                // 2. 分层处理
                Mat enhancedBackground = EnhanceBrightness(input, backgroundROI, 4.0); // 背景可以强增强
                Mat enhancedDetails = EnhanceBrightness(input, detailROI, 2.5);        // 细节区域温和增强
                Mat enhancedEdges = EnhanceBrightness(input, edgeROI, 1.8);            // 边缘区域保守增强
                
                // 3. 加权融合
                enhancedBackground.CopyTo(result, backgroundROI);
                enhancedDetails.CopyTo(result, detailROI);
                enhancedEdges.CopyTo(result, edgeROI);
                
                // 清理资源
                backgroundROI.Dispose(); detailROI.Dispose(); edgeROI.Dispose();
                enhancedBackground.Dispose(); enhancedDetails.Dispose(); enhancedEdges.Dispose();
                
                return result;
            }
            catch
            {
                return input?.Clone();
            }
        }

        /// <summary>
        /// 检测ROI中的背景区域
        /// </summary>
        private static Mat DetectBackgroundInROI(Mat image, Mat roiMask)
        {
            try
            {
                // 使用形态学操作检测大面积的均匀区域
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(15, 15));
                Mat opened = new Mat();
                Cv2.MorphologyEx(image, opened, MorphTypes.Open, kernel);
                
                // 创建背景掩码
                Mat backgroundMask = new Mat();
                Cv2.Threshold(opened, backgroundMask, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                backgroundMask.ConvertTo(backgroundMask, MatType.CV_8UC1, 1.0 / 256.0);
                
                // 与ROI掩码相交
                Mat result = new Mat();
                Cv2.BitwiseAnd(backgroundMask, roiMask, result);
                
                kernel.Dispose(); opened.Dispose(); backgroundMask.Dispose();
                return result;
            }
            catch
            {
                return roiMask.Clone();
            }
        }

        /// <summary>
        /// 检测ROI中的细节区域
        /// </summary>
        private static Mat DetectDetailInROI(Mat image, Mat roiMask)
        {
            try
            {
                // 使用拉普拉斯算子检测细节
                Mat laplacian = new Mat();
                Cv2.Laplacian(image, laplacian, MatType.CV_16S, 3);
                Cv2.ConvertScaleAbs(laplacian, laplacian);
                
                // 阈值化得到细节掩码
                Mat detailMask = new Mat();
                Cv2.Threshold(laplacian, detailMask, 100, 255, ThresholdTypes.Binary);
                detailMask.ConvertTo(detailMask, MatType.CV_8UC1, 1.0 / 256.0);
                
                // 与ROI掩码相交
                Mat result = new Mat();
                Cv2.BitwiseAnd(detailMask, roiMask, result);
                
                laplacian.Dispose(); detailMask.Dispose();
                return result;
            }
            catch
            {
                return Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }
        }

        /// <summary>
        /// 检测ROI中的边缘区域
        /// </summary>
        private static Mat DetectEdgeInROI(Mat image, Mat roiMask)
        {
            try
            {
                // 转换为8位
                Mat image8bit = new Mat();
                image.ConvertTo(image8bit, MatType.CV_8UC1, 255.0 / 65535.0);
                
                // Canny边缘检测
                Mat edges = new Mat();
                Cv2.Canny(image8bit, edges, 50, 150);
                
                // 膨胀边缘以创建边缘区域
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
                Mat dilated = new Mat();
                Cv2.Dilate(edges, dilated, kernel);
                
                // 与ROI掩码相交
                Mat result = new Mat();
                Cv2.BitwiseAnd(dilated, roiMask, result);
                
                image8bit.Dispose(); edges.Dispose(); kernel.Dispose(); dilated.Dispose();
                return result;
            }
            catch
            {
                return Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }
        }
    }
}
