using System;
using OpenCvSharp;
using ImageAnalysisTool.Core.Models;

namespace ImageAnalysisTool.Core.Analyzers
{
    /// <summary>
    /// 医学影像专用分析器 - 评估医学影像增强效果的专业指标
    /// </summary>
    public static class MedicalImageAnalyzer
    {
        /// <summary>
        /// 分析医学影像专用指标
        /// </summary>
        /// <param name="original">原始图像</param>
        /// <param name="enhanced">增强后图像</param>
        /// <param name="config">分析配置</param>
        /// <param name="roiMask">ROI区域掩码，如果为null则分析全图</param>
        /// <returns>医学影像专用指标</returns>
        public static MedicalImageMetrics AnalyzeMedicalImage(Mat original, Mat enhanced, AnalysisConfiguration config, Mat roiMask = null)
        {
            var metrics = new MedicalImageMetrics();

            try
            {
                // 如果提供了ROI掩码，只分析ROI区域
                Mat originalRegion = roiMask != null ? ApplyMask(original, roiMask) : original;
                Mat enhancedRegion = roiMask != null ? ApplyMask(enhanced, roiMask) : enhanced;

                // 1. 分析信息保持度
                metrics.InformationPreservation = AnalyzeInformationPreservation(originalRegion, enhancedRegion, roiMask);

                // 2. 分析动态范围利用率
                metrics.DynamicRangeUtilization = AnalyzeDynamicRangeUtilization(enhancedRegion, roiMask);

                // 3. 分析局部对比度增强效果
                metrics.LocalContrastEnhancement = AnalyzeLocalContrastEnhancement(originalRegion, enhancedRegion, roiMask);

                // 4. 分析细节保真度
                metrics.DetailFidelity = AnalyzeDetailFidelity(originalRegion, enhancedRegion, roiMask);

                // 5. 分析窗宽窗位适应性
                metrics.WindowLevelAdaptability = AnalyzeWindowLevelAdaptability(originalRegion, enhancedRegion, roiMask);

                // 6. 计算综合医学影像质量评分
                metrics.OverallMedicalQuality = CalculateOverallMedicalQuality(metrics);

                // 清理临时图像
                if (roiMask != null)
                {
                    originalRegion?.Dispose();
                    enhancedRegion?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"医学影像分析失败: {ex.Message}");
            }

            return metrics;
        }

        /// <summary>
        /// 应用掩码到图像
        /// </summary>
        private static Mat ApplyMask(Mat image, Mat mask)
        {
            if (mask == null) return image;

            Mat result = new Mat();
            image.CopyTo(result, mask);
            return result;
        }

        /// <summary>
        /// 分析信息保持度 - 通过互信息和梯度相关性评估
        /// </summary>
        private static double AnalyzeInformationPreservation(Mat original, Mat enhanced, Mat roiMask)
        {
            try
            {
                // 确保图像为8位用于计算
                Mat original8bit = new Mat();
                Mat enhanced8bit = new Mat();
                
                if (original.Depth() != MatType.CV_8U)
                {
                    original.ConvertTo(original8bit, MatType.CV_8U, 255.0 / 65535.0);
                }
                else
                {
                    original8bit = original.Clone();
                }

                if (enhanced.Depth() != MatType.CV_8U)
                {
                    enhanced.ConvertTo(enhanced8bit, MatType.CV_8U, 255.0 / 65535.0);
                }
                else
                {
                    enhanced8bit = enhanced.Clone();
                }

                // 计算梯度相关性
                Mat gradX1 = new Mat(), gradY1 = new Mat();
                Mat gradX2 = new Mat(), gradY2 = new Mat();
                
                Cv2.Sobel(original8bit, gradX1, MatType.CV_32F, 1, 0, 3);
                Cv2.Sobel(original8bit, gradY1, MatType.CV_32F, 0, 1, 3);
                Cv2.Sobel(enhanced8bit, gradX2, MatType.CV_32F, 1, 0, 3);
                Cv2.Sobel(enhanced8bit, gradY2, MatType.CV_32F, 0, 1, 3);

                // 计算梯度幅值
                Mat gradMag1 = new Mat(), gradMag2 = new Mat();
                Cv2.Magnitude(gradX1, gradY1, gradMag1);
                Cv2.Magnitude(gradX2, gradY2, gradMag2);

                // 计算相关系数
                Scalar mean1 = Cv2.Mean(gradMag1, roiMask);
                Scalar mean2 = Cv2.Mean(gradMag2, roiMask);
                
                Mat diff1 = new Mat(), diff2 = new Mat();
                Cv2.Subtract(gradMag1, mean1, diff1);
                Cv2.Subtract(gradMag2, mean2, diff2);
                
                Mat product = new Mat();
                Cv2.Multiply(diff1, diff2, product);
                
                Scalar numerator = Cv2.Sum(product);
                Scalar variance1 = Cv2.Sum(diff1.Mul(diff1));
                Scalar variance2 = Cv2.Sum(diff2.Mul(diff2));
                
                double correlation = numerator.Val0 / Math.Sqrt(variance1.Val0 * variance2.Val0);
                
                // 清理资源
                original8bit.Dispose(); enhanced8bit.Dispose();
                gradX1.Dispose(); gradY1.Dispose(); gradX2.Dispose(); gradY2.Dispose();
                gradMag1.Dispose(); gradMag2.Dispose();
                diff1.Dispose(); diff2.Dispose(); product.Dispose();

                // 转换为0-100分数
                return Math.Max(0, Math.Min(100, (correlation + 1) * 50));
            }
            catch
            {
                return 50; // 默认中等分数
            }
        }

        /// <summary>
        /// 分析动态范围利用率 - 评估灰度范围的充分利用程度
        /// </summary>
        private static double AnalyzeDynamicRangeUtilization(Mat enhanced, Mat roiMask)
        {
            try
            {
                // 计算直方图
                Mat hist = new Mat();
                int[] histSize = { 256 };
                Rangef[] ranges = { new Rangef(0, 256) };
                
                // 如果是16位图像，需要先转换
                Mat enhanced8bit = new Mat();
                if (enhanced.Depth() != MatType.CV_8U)
                {
                    enhanced.ConvertTo(enhanced8bit, MatType.CV_8U, 255.0 / 65535.0);
                }
                else
                {
                    enhanced8bit = enhanced.Clone();
                }
                
                Cv2.CalcHist(new Mat[] { enhanced8bit }, new int[] { 0 }, roiMask, hist, 1, histSize, ranges);

                // 计算有效灰度级数量
                int usedLevels = 0;
                for (int i = 0; i < 256; i++)
                {
                    if (hist.At<float>(i) > 0)
                        usedLevels++;
                }

                // 计算动态范围利用率
                double utilization = (double)usedLevels / 256.0 * 100;

                // 计算分布均匀性
                double entropy = 0;
                float totalPixels = (float)Cv2.Sum(hist).Val0;
                for (int i = 0; i < 256; i++)
                {
                    float prob = hist.At<float>(i) / totalPixels;
                    if (prob > 0)
                        entropy -= prob * Math.Log(prob, 2);
                }
                double maxEntropy = Math.Log(256, 2);
                double uniformity = entropy / maxEntropy * 100;

                // 综合评分
                double score = utilization * 0.6 + uniformity * 0.4;

                enhanced8bit.Dispose();
                hist.Dispose();

                return Math.Max(0, Math.Min(100, score));
            }
            catch
            {
                return 50;
            }
        }

        /// <summary>
        /// 分析局部对比度增强效果
        /// </summary>
        private static double AnalyzeLocalContrastEnhancement(Mat original, Mat enhanced, Mat roiMask)
        {
            try
            {
                // 计算局部标准差
                Mat originalFloat = new Mat(), enhancedFloat = new Mat();
                original.ConvertTo(originalFloat, MatType.CV_32F);
                enhanced.ConvertTo(enhancedFloat, MatType.CV_32F);

                // 使用滑动窗口计算局部标准差
                int kernelSize = 9;
                Mat kernel = Mat.Ones(new Size(kernelSize, kernelSize), MatType.CV_32F) / (kernelSize * kernelSize);
                
                Mat originalMean = new Mat(), enhancedMean = new Mat();
                Cv2.Filter2D(originalFloat, originalMean, -1, kernel);
                Cv2.Filter2D(enhancedFloat, enhancedMean, -1, kernel);
                
                Mat originalSqr = new Mat(), enhancedSqr = new Mat();
                Cv2.Multiply(originalFloat, originalFloat, originalSqr);
                Cv2.Multiply(enhancedFloat, enhancedFloat, enhancedSqr);
                
                Mat originalSqrMean = new Mat(), enhancedSqrMean = new Mat();
                Cv2.Filter2D(originalSqr, originalSqrMean, -1, kernel);
                Cv2.Filter2D(enhancedSqr, enhancedSqrMean, -1, kernel);
                
                Mat originalVar = new Mat(), enhancedVar = new Mat();
                Cv2.Subtract(originalSqrMean, originalMean.Mul(originalMean), originalVar);
                Cv2.Subtract(enhancedSqrMean, enhancedMean.Mul(enhancedMean), enhancedVar);
                
                Mat originalStd = new Mat(), enhancedStd = new Mat();
                Cv2.Sqrt(originalVar, originalStd);
                Cv2.Sqrt(enhancedVar, enhancedStd);

                // 计算对比度提升比例
                Scalar originalMeanStd = Cv2.Mean(originalStd, roiMask);
                Scalar enhancedMeanStd = Cv2.Mean(enhancedStd, roiMask);
                
                double improvement = enhancedMeanStd.Val0 / Math.Max(originalMeanStd.Val0, 1.0);
                
                // 清理资源
                originalFloat.Dispose(); enhancedFloat.Dispose(); kernel.Dispose();
                originalMean.Dispose(); enhancedMean.Dispose();
                originalSqr.Dispose(); enhancedSqr.Dispose();
                originalSqrMean.Dispose(); enhancedSqrMean.Dispose();
                originalVar.Dispose(); enhancedVar.Dispose();
                originalStd.Dispose(); enhancedStd.Dispose();

                // 转换为0-100分数，理想提升为1.5-3倍
                double score = Math.Min(100, Math.Max(0, (improvement - 1.0) * 50));
                return score;
            }
            catch
            {
                return 50;
            }
        }

        /// <summary>
        /// 分析细节保真度 - 通过高频信息保持度评估
        /// </summary>
        private static double AnalyzeDetailFidelity(Mat original, Mat enhanced, Mat roiMask)
        {
            try
            {
                // 高通滤波提取细节
                Mat originalFloat = new Mat(), enhancedFloat = new Mat();
                original.ConvertTo(originalFloat, MatType.CV_32F);
                enhanced.ConvertTo(enhancedFloat, MatType.CV_32F);

                // 拉普拉斯算子提取边缘细节
                Mat originalLaplacian = new Mat(), enhancedLaplacian = new Mat();
                Cv2.Laplacian(originalFloat, originalLaplacian, MatType.CV_32F);
                Cv2.Laplacian(enhancedFloat, enhancedLaplacian, MatType.CV_32F);

                // 计算细节信息的相关性
                Scalar originalMean = Cv2.Mean(originalLaplacian, roiMask);
                Scalar enhancedMean = Cv2.Mean(enhancedLaplacian, roiMask);
                
                Mat originalCentered = new Mat(), enhancedCentered = new Mat();
                Cv2.Subtract(originalLaplacian, originalMean, originalCentered);
                Cv2.Subtract(enhancedLaplacian, enhancedMean, enhancedCentered);
                
                Mat product = new Mat();
                Cv2.Multiply(originalCentered, enhancedCentered, product);
                
                Scalar numerator = Cv2.Sum(product);
                Scalar variance1 = Cv2.Sum(originalCentered.Mul(originalCentered));
                Scalar variance2 = Cv2.Sum(enhancedCentered.Mul(enhancedCentered));
                
                double correlation = numerator.Val0 / Math.Sqrt(variance1.Val0 * variance2.Val0);
                
                // 清理资源
                originalFloat.Dispose(); enhancedFloat.Dispose();
                originalLaplacian.Dispose(); enhancedLaplacian.Dispose();
                originalCentered.Dispose(); enhancedCentered.Dispose();
                product.Dispose();

                // 转换为0-100分数
                return Math.Max(0, Math.Min(100, (correlation + 1) * 50));
            }
            catch
            {
                return 50;
            }
        }

        /// <summary>
        /// 分析窗宽窗位适应性 - 模拟不同窗宽窗位下的表现稳定性
        /// </summary>
        private static double AnalyzeWindowLevelAdaptability(Mat original, Mat enhanced, Mat roiMask)
        {
            try
            {
                // 模拟3种不同的窗宽窗位设置
                double[] windowCenters = { 0.3, 0.5, 0.7 }; // 相对于最大值的比例
                double[] windowWidths = { 0.3, 0.5, 0.8 };  // 相对于最大值的比例
                
                double totalScore = 0;
                int validTests = 0;

                foreach (double center in windowCenters)
                {
                    foreach (double width in windowWidths)
                    {
                        try
                        {
                            // 应用窗宽窗位变换
                            Mat windowedOriginal = ApplyWindowLevel(original, center, width);
                            Mat windowedEnhanced = ApplyWindowLevel(enhanced, center, width);
                            
                            // 计算在此窗宽窗位下的对比度
                            Scalar originalStd = CalculateStandardDeviation(windowedOriginal, roiMask);
                            Scalar enhancedStd = CalculateStandardDeviation(windowedEnhanced, roiMask);
                            
                            double contrastRatio = enhancedStd.Val0 / Math.Max(originalStd.Val0, 1.0);
                            totalScore += Math.Min(100, Math.Max(0, (contrastRatio - 1.0) * 50));
                            validTests++;
                            
                            windowedOriginal.Dispose();
                            windowedEnhanced.Dispose();
                        }
                        catch
                        {
                            // 跳过失败的测试
                        }
                    }
                }

                return validTests > 0 ? totalScore / validTests : 50;
            }
            catch
            {
                return 50;
            }
        }

        /// <summary>
        /// 应用窗宽窗位变换
        /// </summary>
        private static Mat ApplyWindowLevel(Mat image, double center, double width)
        {
            Mat result = new Mat();
            
            // 获取图像的最大值
            double maxVal;
            Cv2.MinMaxLoc(image, out _, out maxVal);
            
            double windowCenter = center * maxVal;
            double windowWidth = width * maxVal;
            double minLevel = windowCenter - windowWidth / 2;
            double maxLevel = windowCenter + windowWidth / 2;
            
            // 应用窗宽窗位
            Mat normalized = new Mat();
            image.ConvertTo(normalized, MatType.CV_32F);
            
            // 限制到窗口范围
            Cv2.Threshold(normalized, normalized, maxLevel, maxLevel, ThresholdTypes.Trunc);
            Cv2.Threshold(normalized, normalized, minLevel, 0, ThresholdTypes.Tozero);
            
            // 归一化到0-255
            if (maxLevel > minLevel)
            {
                normalized = (normalized - minLevel) * 255.0 / (maxLevel - minLevel);
            }
            
            normalized.ConvertTo(result, image.Type());
            normalized.Dispose();
            
            return result;
        }

        /// <summary>
        /// 计算标准差
        /// </summary>
        private static Scalar CalculateStandardDeviation(Mat image, Mat mask)
        {
            Scalar mean = Cv2.Mean(image, mask);
            Mat diff = new Mat();
            Cv2.Subtract(image, mean, diff);
            Mat squared = new Mat();
            Cv2.Multiply(diff, diff, squared);
            Scalar variance = Cv2.Mean(squared, mask);
            
            diff.Dispose();
            squared.Dispose();
            
            return new Scalar(Math.Sqrt(variance.Val0));
        }

        /// <summary>
        /// 计算综合医学影像质量评分
        /// </summary>
        private static double CalculateOverallMedicalQuality(MedicalImageMetrics metrics)
        {
            // 加权平均计算综合评分
            double weightedScore = 
                metrics.InformationPreservation * 0.25 +      // 信息保持最重要
                metrics.DetailFidelity * 0.25 +              // 细节保真度同样重要
                metrics.LocalContrastEnhancement * 0.20 +    // 局部对比度增强
                metrics.DynamicRangeUtilization * 0.15 +     // 动态范围利用
                metrics.WindowLevelAdaptability * 0.15;      // 窗宽窗位适应性

            return Math.Max(0, Math.Min(100, weightedScore));
        }
    }
}
