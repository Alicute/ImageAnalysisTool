using System;
using OpenCvSharp;
using ImageAnalysisTool.Core.Models;

namespace ImageAnalysisTool.Core.Analyzers
{
    /// <summary>
    /// 图像质量分析器 - 评估增强后的图像质量
    /// </summary>
    public static class ImageQualityAnalyzer
    {
        /// <summary>
        /// 分析图像质量指标
        /// </summary>
        /// <param name="original">原始图像</param>
        /// <param name="enhanced">增强后图像</param>
        /// <param name="config">分析配置</param>
        /// <param name="roiMask">ROI区域掩码，如果为null则分析全图</param>
        /// <returns>图像质量指标</returns>
        public static ImageQualityMetrics AnalyzeQuality(Mat original, Mat enhanced, AnalysisConfiguration config, Mat roiMask = null)
        {
            var metrics = new ImageQualityMetrics();

            try
            {
                // 如果提供了ROI掩码，只分析ROI区域
                Mat originalRegion = roiMask != null ? ApplyMask(original, roiMask) : original;
                Mat enhancedRegion = roiMask != null ? ApplyMask(enhanced, roiMask) : enhanced;

                // 1. 计算PSNR (峰值信噪比)
                metrics.PSNR = CalculatePSNR(originalRegion, enhancedRegion, roiMask);

                // 2. 计算SSIM (结构相似性) - 可选
                if (config.EnableSSIM)
                {
                    metrics.SSIM = CalculateSSIM(originalRegion, enhancedRegion, roiMask);
                }

                // 3. 检测过度增强
                metrics.OverEnhancementScore = DetectOverEnhancement(enhancedRegion, roiMask);

                // 4. 检测噪声放大
                if (config.EnableNoiseAnalysis)
                {
                    metrics.NoiseAmplification = DetectNoiseAmplification(originalRegion, enhancedRegion, roiMask);
                }

                // 5. 分析边缘质量
                if (config.EnableDetailedEdgeAnalysis)
                {
                    metrics.EdgeQuality = AnalyzeEdgeQuality(enhancedRegion, roiMask);
                    metrics.HaloEffect = DetectHaloEffect(enhancedRegion, roiMask);
                }

                // 6. 计算VIF (视觉信息保真度) - 可选，计算量大
                if (config.EnableVIF)
                {
                    metrics.VIF = CalculateVIF(originalRegion, enhancedRegion, roiMask);
                }

                // 清理临时图像
                if (roiMask != null)
                {
                    originalRegion?.Dispose();
                    enhancedRegion?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"图像质量分析失败: {ex.Message}");
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
        /// 计算峰值信噪比 (PSNR)
        /// </summary>
        private static double CalculatePSNR(Mat original, Mat enhanced, Mat mask = null)
        {
            try
            {
                // 计算均方误差 (MSE)
                Mat diff = new Mat();
                Cv2.Absdiff(original, enhanced, diff);

                // 转换为double类型进行计算
                Mat diffDouble = new Mat();
                diff.ConvertTo(diffDouble, MatType.CV_64F);

                // 计算平方
                Mat squared = new Mat();
                Cv2.Multiply(diffDouble, diffDouble, squared);

                // 计算均值（如果有掩码则只计算掩码区域）
                Scalar mse = mask != null ? Cv2.Mean(squared, mask) : Cv2.Mean(squared);

                // 清理资源
                diff.Dispose();
                diffDouble.Dispose();
                squared.Dispose();

                // 计算PSNR
                if (mse.Val0 == 0) return double.PositiveInfinity; // 完全相同

                double maxPixelValue = 65535.0; // 16位图像的最大值
                double psnr = 20 * Math.Log10(maxPixelValue / Math.Sqrt(mse.Val0));

                return Math.Max(0, Math.Min(100, psnr)); // 限制在合理范围内
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 计算结构相似性指数 (SSIM) - 简化版本
        /// </summary>
        private static double CalculateSSIM(Mat original, Mat enhanced, Mat mask = null)
        {
            try
            {
                // 转换为浮点类型
                Mat img1 = new Mat(), img2 = new Mat();
                original.ConvertTo(img1, MatType.CV_32F);
                enhanced.ConvertTo(img2, MatType.CV_32F);

                // 计算均值
                Mat mu1 = new Mat(), mu2 = new Mat();
                Cv2.GaussianBlur(img1, mu1, new Size(11, 11), 1.5);
                Cv2.GaussianBlur(img2, mu2, new Size(11, 11), 1.5);

                // 计算方差和协方差
                Mat mu1Sq = new Mat(), mu2Sq = new Mat(), mu1Mu2 = new Mat();
                Cv2.Multiply(mu1, mu1, mu1Sq);
                Cv2.Multiply(mu2, mu2, mu2Sq);
                Cv2.Multiply(mu1, mu2, mu1Mu2);

                Mat sigma1Sq = new Mat(), sigma2Sq = new Mat(), sigma12 = new Mat();
                Mat img1Sq = new Mat(), img2Sq = new Mat(), img1Img2 = new Mat();
                
                Cv2.Multiply(img1, img1, img1Sq);
                Cv2.Multiply(img2, img2, img2Sq);
                Cv2.Multiply(img1, img2, img1Img2);

                Cv2.GaussianBlur(img1Sq, sigma1Sq, new Size(11, 11), 1.5);
                Cv2.GaussianBlur(img2Sq, sigma2Sq, new Size(11, 11), 1.5);
                Cv2.GaussianBlur(img1Img2, sigma12, new Size(11, 11), 1.5);

                Cv2.Subtract(sigma1Sq, mu1Sq, sigma1Sq);
                Cv2.Subtract(sigma2Sq, mu2Sq, sigma2Sq);
                Cv2.Subtract(sigma12, mu1Mu2, sigma12);

                // SSIM常数
                double c1 = Math.Pow(0.01 * 65535, 2);
                double c2 = Math.Pow(0.03 * 65535, 2);

                // 计算SSIM
                Mat numerator1 = new Mat(), numerator2 = new Mat(), denominator1 = new Mat(), denominator2 = new Mat();
                
                Cv2.AddWeighted(mu1Mu2, 2.0, new Scalar(c1), 1.0, 0, numerator1);
                Cv2.AddWeighted(sigma12, 2.0, new Scalar(c2), 1.0, 0, numerator2);
                
                Cv2.Add(mu1Sq, mu2Sq, denominator1);
                Cv2.Add(denominator1, new Scalar(c1), denominator1);
                
                Cv2.Add(sigma1Sq, sigma2Sq, denominator2);
                Cv2.Add(denominator2, new Scalar(c2), denominator2);

                Mat ssimMap = new Mat();
                Mat num = new Mat(), den = new Mat();
                Cv2.Multiply(numerator1, numerator2, num);
                Cv2.Multiply(denominator1, denominator2, den);
                Cv2.Divide(num, den, ssimMap);

                Scalar meanSSIM = Cv2.Mean(ssimMap);

                // 清理资源
                img1.Dispose(); img2.Dispose(); mu1.Dispose(); mu2.Dispose();
                mu1Sq.Dispose(); mu2Sq.Dispose(); mu1Mu2.Dispose();
                sigma1Sq.Dispose(); sigma2Sq.Dispose(); sigma12.Dispose();
                img1Sq.Dispose(); img2Sq.Dispose(); img1Img2.Dispose();
                numerator1.Dispose(); numerator2.Dispose();
                denominator1.Dispose(); denominator2.Dispose();
                ssimMap.Dispose(); num.Dispose(); den.Dispose();

                return Math.Max(0, Math.Min(1, meanSSIM.Val0));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检测过度增强
        /// </summary>
        private static double DetectOverEnhancement(Mat enhanced, Mat mask = null)
        {
            try
            {
                // 计算梯度幅值
                Mat gradX = new Mat(), gradY = new Mat();
                Cv2.Sobel(enhanced, gradX, MatType.CV_32F, 1, 0, 3);
                Cv2.Sobel(enhanced, gradY, MatType.CV_32F, 0, 1, 3);

                Mat gradMagnitude = new Mat();
                Cv2.Magnitude(gradX, gradY, gradMagnitude);

                // 计算梯度统计
                Scalar meanGrad = Cv2.Mean(gradMagnitude);
                Scalar stdGrad = new Scalar();
                Cv2.MeanStdDev(gradMagnitude, out var _, out stdGrad);

                // 检测异常高的梯度值 (可能表示过度锐化)
                Mat highGradMask = new Mat();
                double threshold = meanGrad.Val0 + 2 * stdGrad.Val0;
                Cv2.Threshold(gradMagnitude, highGradMask, threshold, 255, ThresholdTypes.Binary);

                int highGradPixels = Cv2.CountNonZero(highGradMask);
                double totalPixels = enhanced.Width * enhanced.Height;
                double overEnhancementRatio = (double)highGradPixels / totalPixels * 100;

                // 清理资源
                gradX.Dispose(); gradY.Dispose(); gradMagnitude.Dispose(); highGradMask.Dispose();

                return Math.Max(0, Math.Min(100, overEnhancementRatio));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检测噪声放大
        /// </summary>
        private static double DetectNoiseAmplification(Mat original, Mat enhanced, Mat mask = null)
        {
            try
            {
                // 使用高斯滤波器估计噪声
                Mat originalSmooth = new Mat(), enhancedSmooth = new Mat();
                Cv2.GaussianBlur(original, originalSmooth, new Size(5, 5), 1.0);
                Cv2.GaussianBlur(enhanced, enhancedSmooth, new Size(5, 5), 1.0);

                Mat originalNoise = new Mat(), enhancedNoise = new Mat();
                Cv2.Subtract(original, originalSmooth, originalNoise);
                Cv2.Subtract(enhanced, enhancedSmooth, enhancedNoise);

                // 计算噪声的标准差
                Scalar originalNoiseStd = new Scalar(), enhancedNoiseStd = new Scalar();
                Cv2.MeanStdDev(originalNoise, out var _, out originalNoiseStd);
                Cv2.MeanStdDev(enhancedNoise, out var _, out enhancedNoiseStd);

                double amplificationRatio = enhancedNoiseStd.Val0 / Math.Max(originalNoiseStd.Val0, 1.0);

                // 清理资源
                originalSmooth.Dispose(); enhancedSmooth.Dispose();
                originalNoise.Dispose(); enhancedNoise.Dispose();

                return Math.Max(1.0, amplificationRatio);
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>
        /// 分析边缘质量
        /// </summary>
        private static double AnalyzeEdgeQuality(Mat enhanced, Mat mask = null)
        {
            try
            {
                // 使用Canny边缘检测
                Mat edges = new Mat();
                Mat enhanced8bit = new Mat();
                enhanced.ConvertTo(enhanced8bit, MatType.CV_8UC1, 255.0 / 65535.0);
                Cv2.Canny(enhanced8bit, edges, 50, 150);

                // 计算边缘密度和连续性
                int edgePixels = Cv2.CountNonZero(edges);
                double totalPixels = enhanced.Width * enhanced.Height;
                double edgeDensity = (double)edgePixels / totalPixels;

                // 分析边缘连续性 (简化版本)
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
                Mat dilated = new Mat();
                Cv2.Dilate(edges, dilated, kernel);
                
                int connectedEdgePixels = Cv2.CountNonZero(dilated);
                double continuity = (double)connectedEdgePixels / Math.Max(edgePixels, 1);

                // 综合评分
                double qualityScore = (edgeDensity * 50 + continuity * 50);

                // 清理资源
                edges.Dispose(); enhanced8bit.Dispose(); kernel.Dispose(); dilated.Dispose();

                return Math.Max(0, Math.Min(100, qualityScore * 100));
            }
            catch
            {
                return 50; // 默认中等质量
            }
        }

        /// <summary>
        /// 检测光晕效应
        /// </summary>
        private static double DetectHaloEffect(Mat enhanced, Mat mask = null)
        {
            try
            {
                // 检测边缘附近的异常亮度变化
                Mat edges = new Mat();
                Mat enhanced8bit = new Mat();
                enhanced.ConvertTo(enhanced8bit, MatType.CV_8UC1, 255.0 / 65535.0);
                Cv2.Canny(enhanced8bit, edges, 50, 150);

                // 膨胀边缘以创建边缘区域
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(5, 5));
                Mat edgeRegion = new Mat();
                Cv2.Dilate(edges, edgeRegion, kernel);

                // 在边缘区域计算亮度变化
                Scalar edgeRegionMean = Cv2.Mean(enhanced, edgeRegion);
                Scalar globalMean = Cv2.Mean(enhanced);

                double brightnessRatio = edgeRegionMean.Val0 / Math.Max(globalMean.Val0, 1.0);
                
                // 如果边缘区域明显比全局更亮，可能存在光晕
                double haloScore = Math.Max(0, (brightnessRatio - 1.2) * 100);

                // 清理资源
                edges.Dispose(); enhanced8bit.Dispose(); kernel.Dispose(); edgeRegion.Dispose();

                return Math.Max(0, Math.Min(100, haloScore));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 计算视觉信息保真度 (VIF) - 简化版本
        /// </summary>
        private static double CalculateVIF(Mat original, Mat enhanced, Mat mask = null)
        {
            try
            {
                // 这是一个简化的VIF实现
                // 实际的VIF计算非常复杂，这里提供基本版本
                
                // 转换为浮点类型
                Mat img1 = new Mat(), img2 = new Mat();
                original.ConvertTo(img1, MatType.CV_32F);
                enhanced.ConvertTo(img2, MatType.CV_32F);

                // 计算局部方差
                Mat mu1 = new Mat(), mu2 = new Mat();
                Cv2.GaussianBlur(img1, mu1, new Size(11, 11), 1.5);
                Cv2.GaussianBlur(img2, mu2, new Size(11, 11), 1.5);

                Mat sigma1 = new Mat(), sigma2 = new Mat();
                Mat img1Sq = new Mat(), img2Sq = new Mat();
                Cv2.Multiply(img1, img1, img1Sq);
                Cv2.Multiply(img2, img2, img2Sq);

                Cv2.GaussianBlur(img1Sq, sigma1, new Size(11, 11), 1.5);
                Cv2.GaussianBlur(img2Sq, sigma2, new Size(11, 11), 1.5);

                Mat mu1Sq = new Mat(), mu2Sq = new Mat();
                Cv2.Multiply(mu1, mu1, mu1Sq);
                Cv2.Multiply(mu2, mu2, mu2Sq);

                Cv2.Subtract(sigma1, mu1Sq, sigma1);
                Cv2.Subtract(sigma2, mu2Sq, sigma2);

                // 简化的VIF计算
                Scalar meanSigma1 = Cv2.Mean(sigma1);
                Scalar meanSigma2 = Cv2.Mean(sigma2);

                double vif = Math.Min(meanSigma2.Val0 / Math.Max(meanSigma1.Val0, 1.0), 1.0);

                // 清理资源
                img1.Dispose(); img2.Dispose(); mu1.Dispose(); mu2.Dispose();
                sigma1.Dispose(); sigma2.Dispose(); img1Sq.Dispose(); img2Sq.Dispose();
                mu1Sq.Dispose(); mu2Sq.Dispose();

                return Math.Max(0, Math.Min(1, vif));
            }
            catch
            {
                return 0.5; // 默认中等值
            }
        }
    }
}
