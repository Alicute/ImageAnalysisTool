using System;
using OpenCvSharp;
using ImageAnalysisTool.Core.Models;

namespace ImageAnalysisTool.Core.Analyzers
{
    /// <summary>
    /// 缺陷检测友好度分析器 - 评估增强效果对缺陷检测的帮助程度
    /// </summary>
    public static class DefectDetectionAnalyzer
    {
        /// <summary>
        /// 分析缺陷检测友好度指标
        /// </summary>
        /// <param name="original">原始图像</param>
        /// <param name="enhanced">增强后图像</param>
        /// <param name="config">分析配置</param>
        /// <returns>缺陷检测友好度指标</returns>
        public static DefectDetectionMetrics AnalyzeDetectionFriendliness(Mat original, Mat enhanced, AnalysisConfiguration config)
        {
            var metrics = new DefectDetectionMetrics();

            try
            {
                // 1. 分析细线缺陷可见性提升
                metrics.ThinLineVisibility = AnalyzeThinLineVisibility(original, enhanced);

                // 2. 分析背景噪声抑制效果
                metrics.BackgroundNoiseReduction = AnalyzeBackgroundNoiseReduction(original, enhanced);

                // 3. 分析缺陷与背景对比度提升
                metrics.DefectBackgroundContrast = AnalyzeDefectBackgroundContrast(original, enhanced);

                // 4. 评估假阳性风险
                metrics.FalsePositiveRisk = AnalyzeFalsePositiveRisk(enhanced);

                // 5. 计算综合适用性评分
                metrics.OverallSuitability = CalculateOverallSuitability(metrics);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"缺陷检测友好度分析失败: {ex.Message}");
            }

            return metrics;
        }

        

        /// <summary>
        /// 分析细线缺陷可见性提升
        /// </summary>
        private static double AnalyzeThinLineVisibility(Mat original, Mat enhanced)
        {
            try
            {
                // 使用线检测算子检测细线特征
                Mat originalLines = DetectThinLines(original);
                Mat enhancedLines = DetectThinLines(enhanced);

                // 计算线特征的强度提升
                Scalar originalLineStrength = Cv2.Mean(originalLines);
                Scalar enhancedLineStrength = Cv2.Mean(enhancedLines);

                double visibilityImprovement = (enhancedLineStrength.Val0 / Math.Max(originalLineStrength.Val0, 1.0) - 1.0) * 100;

                // 计算线特征的数量提升
                int originalLinePixels = Cv2.CountNonZero(originalLines);
                int enhancedLinePixels = Cv2.CountNonZero(enhancedLines);

                double quantityImprovement = ((double)enhancedLinePixels / Math.Max(originalLinePixels, 1) - 1.0) * 100;

                // 综合评估
                double overallImprovement = (visibilityImprovement + quantityImprovement) / 2;

                originalLines.Dispose();
                enhancedLines.Dispose();

                return Math.Max(0, Math.Min(100, overallImprovement + 50)); // 转换为0-100分，50为基准
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 检测细线特征
        /// </summary>
        private static Mat DetectThinLines(Mat image)
        {
            Mat result = new Mat();
            
            try
            {
                // 转换为8位图像
                Mat image8bit = new Mat();
                image.ConvertTo(image8bit, MatType.CV_8UC1, 255.0 / 65535.0);

                // 使用多方向的线检测核
                Mat[] lineKernels = new Mat[4];
                
                // 水平线检测核
                lineKernels[0] = new Mat(3, 3, MatType.CV_32F);
                lineKernels[0].Set<float>(0, 0, -1); lineKernels[0].Set<float>(0, 1, -1); lineKernels[0].Set<float>(0, 2, -1);
                lineKernels[0].Set<float>(1, 0, 2);  lineKernels[0].Set<float>(1, 1, 2);  lineKernels[0].Set<float>(1, 2, 2);
                lineKernels[0].Set<float>(2, 0, -1); lineKernels[0].Set<float>(2, 1, -1); lineKernels[0].Set<float>(2, 2, -1);

                // 垂直线检测核
                lineKernels[1] = new Mat(3, 3, MatType.CV_32F);
                lineKernels[1].Set<float>(0, 0, -1); lineKernels[1].Set<float>(0, 1, 2); lineKernels[1].Set<float>(0, 2, -1);
                lineKernels[1].Set<float>(1, 0, -1); lineKernels[1].Set<float>(1, 1, 2); lineKernels[1].Set<float>(1, 2, -1);
                lineKernels[1].Set<float>(2, 0, -1); lineKernels[1].Set<float>(2, 1, 2); lineKernels[1].Set<float>(2, 2, -1);

                // 对角线检测核
                lineKernels[2] = new Mat(3, 3, MatType.CV_32F);
                lineKernels[2].Set<float>(0, 0, -1); lineKernels[2].Set<float>(0, 1, -1); lineKernels[2].Set<float>(0, 2, 2);
                lineKernels[2].Set<float>(1, 0, -1); lineKernels[2].Set<float>(1, 1, 2);  lineKernels[2].Set<float>(1, 2, -1);
                lineKernels[2].Set<float>(2, 0, 2);  lineKernels[2].Set<float>(2, 1, -1); lineKernels[2].Set<float>(2, 2, -1);

                // 反对角线检测核
                lineKernels[3] = new Mat(3, 3, MatType.CV_32F);
                lineKernels[3].Set<float>(0, 0, 2);  lineKernels[3].Set<float>(0, 1, -1); lineKernels[3].Set<float>(0, 2, -1);
                lineKernels[3].Set<float>(1, 0, -1); lineKernels[3].Set<float>(1, 1, 2);  lineKernels[3].Set<float>(1, 2, -1);
                lineKernels[3].Set<float>(2, 0, -1); lineKernels[3].Set<float>(2, 1, -1); lineKernels[3].Set<float>(2, 2, 2);

                Mat[] responses = new Mat[4];
                for (int i = 0; i < 4; i++)
                {
                    responses[i] = new Mat();
                    Cv2.Filter2D(image8bit, responses[i], MatType.CV_32F, lineKernels[i]);
                    Cv2.ConvertScaleAbs(responses[i], responses[i]);
                }

                // 合并所有方向的响应
                result = new Mat(image.Size(), MatType.CV_8UC1, Scalar.All(0));
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Max(result, responses[i], result);
                }

                // 阈值化以突出线特征
                Cv2.Threshold(result, result, 30, 255, ThresholdTypes.Binary);

                // 清理资源
                image8bit.Dispose();
                for (int i = 0; i < 4; i++)
                {
                    lineKernels[i].Dispose();
                    responses[i].Dispose();
                }
            }
            catch
            {
                result = Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }

            return result;
        }

        /// <summary>
        /// 分析背景噪声抑制效果
        /// </summary>
        private static double AnalyzeBackgroundNoiseReduction(Mat original, Mat enhanced)
        {
            try
            {
                // 创建背景区域掩码
                Mat backgroundMask = CreateBackgroundMask(original);

                // 检查背景掩码是否有效
                int backgroundPixels = Cv2.CountNonZero(backgroundMask);
                if (backgroundPixels < 100) // 如果背景区域太小，使用简化计算
                {
                    backgroundMask.Dispose();
                    return CalculateSimpleNoiseReduction(original, enhanced);
                }

                // 计算背景区域的噪声水平
                double originalNoise = CalculateNoiseLevel(original, backgroundMask);
                double enhancedNoise = CalculateNoiseLevel(enhanced, backgroundMask);

                // 计算噪声抑制效果
                double noiseReduction = 0;
                if (originalNoise > 0.001)
                {
                    noiseReduction = (1.0 - enhancedNoise / originalNoise) * 100;
                }

                backgroundMask.Dispose();

                return Math.Max(0, Math.Min(100, noiseReduction));
            }
            catch
            {
                return CalculateSimpleNoiseReduction(original, enhanced);
            }
        }

        /// <summary>
        /// 简化的噪声抑制计算
        /// </summary>
        private static double CalculateSimpleNoiseReduction(Mat original, Mat enhanced)
        {
            try
            {
                // 使用拉普拉斯算子计算全图噪声水平
                Mat originalLap = new Mat(), enhancedLap = new Mat();
                Cv2.Laplacian(original, originalLap, MatType.CV_16S);
                Cv2.Laplacian(enhanced, enhancedLap, MatType.CV_16S);

                Scalar originalNoise = Cv2.Mean(Cv2.Abs(originalLap));
                Scalar enhancedNoise = Cv2.Mean(Cv2.Abs(enhancedLap));

                double noiseReduction = 0;
                if (originalNoise.Val0 > 0.001)
                {
                    noiseReduction = (1.0 - enhancedNoise.Val0 / originalNoise.Val0) * 100;
                }

                originalLap.Dispose();
                enhancedLap.Dispose();

                return Math.Max(0, Math.Min(100, noiseReduction));
            }
            catch
            {
                return 25; // 返回中性评分
            }
        }

        /// <summary>
        /// 创建背景区域掩码
        /// </summary>
        private static Mat CreateBackgroundMask(Mat image)
        {
            Mat mask = new Mat();
            
            try
            {
                // 使用OTSU阈值分割背景和前景
                Mat binary = new Mat();
                double threshold = Cv2.Threshold(image, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                // 背景通常是低灰度值区域
                Cv2.Threshold(image, mask, threshold * 0.8, 255, ThresholdTypes.BinaryInv);
                mask.ConvertTo(mask, MatType.CV_8UC1, 1.0 / 256.0);

                binary.Dispose();
            }
            catch
            {
                mask = Mat.Ones(image.Size(), MatType.CV_8UC1);
            }

            return mask;
        }

        /// <summary>
        /// 计算指定区域的噪声水平
        /// </summary>
        private static double CalculateNoiseLevel(Mat image, Mat mask)
        {
            try
            {
                // 使用高斯滤波估计信号
                Mat smoothed = new Mat();
                Cv2.GaussianBlur(image, smoothed, new Size(5, 5), 1.0);

                // 计算噪声 = 原图 - 平滑图
                Mat noise = new Mat();
                Cv2.Subtract(image, smoothed, noise);

                // 在指定区域计算噪声的标准差
                Scalar mean = new Scalar(), stddev = new Scalar();
                Cv2.MeanStdDev(noise, out mean, out stddev, mask);

                smoothed.Dispose();
                noise.Dispose();

                return stddev.Val0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 分析缺陷与背景对比度提升
        /// </summary>
        private static double AnalyzeDefectBackgroundContrast(Mat original, Mat enhanced)
        {
            try
            {
                // 检测潜在的缺陷区域（暗部区域）
                Mat defectMask = CreateDefectMask(original);
                Mat backgroundMask = CreateBackgroundMask(original);

                // 计算原图中缺陷区域和背景区域的对比度
                Scalar originalDefectMean = Cv2.Mean(original, defectMask);
                Scalar originalBackgroundMean = Cv2.Mean(original, backgroundMask);
                double originalContrast = Math.Abs(originalDefectMean.Val0 - originalBackgroundMean.Val0);

                // 计算增强图中缺陷区域和背景区域的对比度
                Scalar enhancedDefectMean = Cv2.Mean(enhanced, defectMask);
                Scalar enhancedBackgroundMean = Cv2.Mean(enhanced, backgroundMask);
                double enhancedContrast = Math.Abs(enhancedDefectMean.Val0 - enhancedBackgroundMean.Val0);

                // 计算对比度提升倍数
                double contrastImprovement = enhancedContrast / Math.Max(originalContrast, 1.0);

                defectMask.Dispose();
                backgroundMask.Dispose();

                return Math.Max(0.1, Math.Min(10.0, contrastImprovement));
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>
        /// 创建缺陷区域掩码
        /// </summary>
        private static Mat CreateDefectMask(Mat image)
        {
            Mat mask = new Mat();
            
            try
            {
                // 缺陷通常是暗部区域
                Scalar mean = new Scalar(), stddev = new Scalar();
                Cv2.MeanStdDev(image, out mean, out stddev);

                double threshold = mean.Val0 - stddev.Val0; // 低于平均值一个标准差的区域
                Cv2.Threshold(image, mask, threshold, 255, ThresholdTypes.BinaryInv);
                mask.ConvertTo(mask, MatType.CV_8UC1, 1.0 / 256.0);

                // 形态学操作去除小噪点
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
                Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
                kernel.Dispose();
            }
            catch
            {
                mask = Mat.Zeros(image.Size(), MatType.CV_8UC1);
            }

            return mask;
        }

        /// <summary>
        /// 分析假阳性风险
        /// </summary>
        private static double AnalyzeFalsePositiveRisk(Mat enhanced)
        {
            try
            {
                // 检测可能导致假阳性的因素

                // 1. 检测过度锐化导致的伪影
                Mat edges = new Mat();
                Mat enhanced8bit = new Mat();
                enhanced.ConvertTo(enhanced8bit, MatType.CV_8UC1, 255.0 / 65535.0);
                Cv2.Canny(enhanced8bit, edges, 50, 150);

                // 计算边缘密度
                int edgePixels = Cv2.CountNonZero(edges);
                double totalPixels = enhanced.Width * enhanced.Height;
                double edgeDensity = (double)edgePixels / totalPixels;

                // 过高的边缘密度可能表示过度锐化
                double oversharpening = Math.Max(0, (edgeDensity - 0.1) * 500);

                // 2. 检测噪声放大
                Mat noise = new Mat();
                Mat smoothed = new Mat();
                Cv2.GaussianBlur(enhanced, smoothed, new Size(3, 3), 1.0);
                Cv2.Subtract(enhanced, smoothed, noise);

                Scalar noiseStd = new Scalar();
                Cv2.MeanStdDev(noise, out var _, out noiseStd);
                double noiseLevel = noiseStd.Val0 / 655.35; // 归一化到0-1

                // 3. 检测不均匀增强
                Mat localMean = new Mat();
                Cv2.GaussianBlur(enhanced, localMean, new Size(15, 15), 5);
                
                Mat unevenness = new Mat();
                Cv2.Subtract(enhanced, localMean, unevenness);
                Cv2.ConvertScaleAbs(unevenness, unevenness);

                Scalar unevennessStd = new Scalar();
                Cv2.MeanStdDev(unevenness, out var _, out unevennessStd);
                double unevennessLevel = unevennessStd.Val0 / 655.35;

                // 综合风险评估
                double riskScore = (oversharpening + noiseLevel * 50 + unevennessLevel * 30);

                // 清理资源
                edges.Dispose(); enhanced8bit.Dispose(); noise.Dispose();
                smoothed.Dispose(); localMean.Dispose(); unevenness.Dispose();

                return Math.Max(0, Math.Min(100, riskScore));
            }
            catch
            {
                return 50; // 默认中等风险
            }
        }

        /// <summary>
        /// 计算综合适用性评分
        /// </summary>
        private static double CalculateOverallSuitability(DefectDetectionMetrics metrics)
        {
            // 加权计算综合评分
            double visibilityScore = Math.Max(0, Math.Min(100, metrics.ThinLineVisibility)); // 已经是0-100分
            double noiseScore = metrics.BackgroundNoiseReduction;
            double contrastScore = Math.Min(100, (metrics.DefectBackgroundContrast - 1.0) * 50 + 50); // 转换为0-100分
            double riskScore = 100 - metrics.FalsePositiveRisk; // 风险越低分数越高

            double weightedScore = 
                visibilityScore * 0.4 +
                contrastScore * 0.3 +
                noiseScore * 0.2 +
                riskScore * 0.1;

            return Math.Max(0, Math.Min(100, weightedScore));
        }
    }
}
