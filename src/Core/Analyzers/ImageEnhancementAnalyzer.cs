using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageAnalysisTool.Core.Analyzers
{
    /// <summary>
    /// 图像增强算法逆向分析工具
    /// 通过对比原图和增强后的图像，分析可能的增强算法和参数
    /// </summary>
    public class ImageEnhancementAnalyzer
    {
        /// <summary>
        /// 分析结果数据结构
        /// </summary>
        public class AnalysisResult
        {
            public Dictionary<int, int> PixelMapping { get; set; } = new Dictionary<int, int>();
            public double[] OriginalHistogram { get; set; }
            public double[] EnhancedHistogram { get; set; }
            public double ContrastRatio { get; set; }
            public double BrightnessChange { get; set; }
            public double GammaEstimate { get; set; }
            public LocalEnhancementInfo LocalInfo { get; set; }
            public string SuggestedAlgorithm { get; set; }
            public Dictionary<string, double> EstimatedParameters { get; set; } = new Dictionary<string, double>();
        }

        /// <summary>
        /// 局部增强信息
        /// </summary>
        public class LocalEnhancementInfo
        {
            public double DarkRegionChange { get; set; }    // 暗部变化率
            public double MidRegionChange { get; set; }     // 中间调变化率
            public double BrightRegionChange { get; set; }  // 亮部变化率
            public bool HasLocalContrast { get; set; }      // 是否有局部对比度增强
            public double EdgeEnhancement { get; set; }     // 边缘增强程度
        }

        /// <summary>
        /// 主要分析方法
        /// </summary>
        public AnalysisResult AnalyzeEnhancement(Mat original, Mat enhanced)
        {
            if (original == null || enhanced == null || original.Size() != enhanced.Size())
                throw new ArgumentException("图像无效或尺寸不匹配");

            var result = new AnalysisResult();

            // 1. 像素映射分析
            result.PixelMapping = AnalyzePixelMapping(original, enhanced);

            // 2. 直方图分析
            (result.OriginalHistogram, result.EnhancedHistogram) = AnalyzeHistograms(original, enhanced);

            // 3. 全局变化分析
            result.ContrastRatio = CalculateContrastRatio(original, enhanced);
            result.BrightnessChange = CalculateBrightnessChange(original, enhanced);
            result.GammaEstimate = EstimateGamma(original, enhanced);

            // 4. 局部增强分析
            result.LocalInfo = AnalyzeLocalEnhancement(original, enhanced);

            // 5. 算法推断
            result.SuggestedAlgorithm = InferAlgorithm(result);
            result.EstimatedParameters = EstimateParameters(result);

            return result;
        }

        /// <summary>
        /// 分析像素值映射关系
        /// </summary>
        private Dictionary<int, int> AnalyzePixelMapping(Mat original, Mat enhanced)
        {
            var mapping = new Dictionary<int, List<int>>();

            // 采样分析（避免处理所有像素）
            int step = Math.Max(1, original.Rows / 100); // 采样步长

            // 检测图像的实际位深
            double minVal, maxVal;
            Cv2.MinMaxLoc(original, out minVal, out maxVal);
            bool is16Bit = maxVal > 255;

            // 根据位深确定分组策略 - 16位使用较小的分组以保持更好的精度
            int binSize = is16Bit ? 64 : 1; // 16位图像分组到1024个区间，8位图像不分组

            for (int y = 0; y < original.Rows; y += step)
            {
                for (int x = 0; x < original.Cols; x += step)
                {
                    ushort origValue = original.At<ushort>(y, x);
                    ushort enhValue = enhanced.At<ushort>(y, x);

                    // 修复：保持更高精度的灰度值分组
                    int origKey = is16Bit ? (origValue / binSize) * binSize : origValue; // 对16位图像进行精细分组
                    int enhVal = enhValue; // 保持完整的增强后像素值

                    if (!mapping.ContainsKey(origKey))
                        mapping[origKey] = new List<int>();
                    
                    mapping[origKey].Add(enhVal);
                }
            }

            // 计算每个区间的平均映射值
            var result = new Dictionary<int, int>();
            foreach (var kvp in mapping)
            {
                if (kvp.Value.Count > 0)
                    result[kvp.Key] = (int)kvp.Value.Average();
            }

            return result;
        }

        /// <summary>
        /// 分析直方图（支持16位图像）
        /// </summary>
        private (double[], double[]) AnalyzeHistograms(Mat original, Mat enhanced)
        {
            // 根据图像位深动态确定直方图大小
            double minVal, maxVal;
            Cv2.MinMaxLoc(original, out minVal, out maxVal);

            // 如果最大值超过255，使用16位直方图；否则使用8位
            int histSize = maxVal > 255 ? 65536 : 256;
            float histRange = maxVal > 255 ? 65536f : 256f;

            var origHist = new double[histSize];
            var enhHist = new double[histSize];

            // 计算直方图
            Mat origHistMat = new Mat();
            Mat enhHistMat = new Mat();

            Cv2.CalcHist(new Mat[] { original }, new int[] { 0 }, null, origHistMat,
                        1, new int[] { histSize }, new Rangef[] { new Rangef(0, histRange) });
            Cv2.CalcHist(new Mat[] { enhanced }, new int[] { 0 }, null, enhHistMat,
                        1, new int[] { histSize }, new Rangef[] { new Rangef(0, histRange) });

            // 转换为数组并归一化
            for (int i = 0; i < histSize; i++)
            {
                origHist[i] = origHistMat.At<float>(i);
                enhHist[i] = enhHistMat.At<float>(i);
            }

            // 归一化
            double origSum = origHist.Sum();
            double enhSum = enhHist.Sum();
            if (origSum > 0) for (int i = 0; i < histSize; i++) origHist[i] /= origSum;
            if (enhSum > 0) for (int i = 0; i < histSize; i++) enhHist[i] /= enhSum;

            origHistMat.Dispose();
            enhHistMat.Dispose();

            return (origHist, enhHist);
        }

        /// <summary>
        /// 计算对比度变化比率
        /// </summary>
        private double CalculateContrastRatio(Mat original, Mat enhanced)
        {
            Scalar origMean, origStd, enhMean, enhStd;
            Cv2.MeanStdDev(original, out origMean, out origStd);
            Cv2.MeanStdDev(enhanced, out enhMean, out enhStd);

            double origContrast = origStd.Val0;
            double enhContrast = enhStd.Val0;

            return enhContrast / Math.Max(origContrast, 1.0);
        }

        /// <summary>
        /// 计算亮度变化
        /// </summary>
        private double CalculateBrightnessChange(Mat original, Mat enhanced)
        {
            Scalar origMean = Cv2.Mean(original);
            Scalar enhMean = Cv2.Mean(enhanced);

            return enhMean.Val0 / Math.Max(origMean.Val0, 1.0);
        }

        /// <summary>
        /// 估算Gamma值
        /// </summary>
        private double EstimateGamma(Mat original, Mat enhanced)
        {
            // 使用中值来估算gamma
            Mat origSorted = new Mat();
            Mat enhSorted = new Mat();
            
            original.Reshape(1, original.Rows * original.Cols).CopyTo(origSorted);
            enhanced.Reshape(1, enhanced.Rows * enhanced.Cols).CopyTo(enhSorted);
            
            Cv2.Sort(origSorted, origSorted, SortFlags.EveryRow | SortFlags.Ascending);
            Cv2.Sort(enhSorted, enhSorted, SortFlags.EveryRow | SortFlags.Ascending);

            int medianIdx = origSorted.Rows / 2;
            double origMedian = origSorted.At<ushort>(medianIdx) / 65535.0;
            double enhMedian = enhSorted.At<ushort>(medianIdx) / 65535.0;

            origSorted.Dispose();
            enhSorted.Dispose();

            if (origMedian > 0.01 && enhMedian > 0.01)
            {
                return Math.Log(enhMedian) / Math.Log(origMedian);
            }

            return 1.0;
        }

        /// <summary>
        /// 分析局部增强特征
        /// </summary>
        private LocalEnhancementInfo AnalyzeLocalEnhancement(Mat original, Mat enhanced)
        {
            var info = new LocalEnhancementInfo();

            // 分析不同亮度区域的变化
            Mat darkMask = new Mat();
            Mat midMask = new Mat();
            Mat brightMask = new Mat();

            Cv2.InRange(original, Scalar.All(0), Scalar.All(21845), darkMask);      // 0-33%
            Cv2.InRange(original, Scalar.All(21845), Scalar.All(43690), midMask);  // 33-66%
            Cv2.InRange(original, Scalar.All(43690), Scalar.All(65535), brightMask); // 66-100%

            info.DarkRegionChange = CalculateRegionChange(original, enhanced, darkMask);
            info.MidRegionChange = CalculateRegionChange(original, enhanced, midMask);
            info.BrightRegionChange = CalculateRegionChange(original, enhanced, brightMask);

            // 检测边缘增强
            info.EdgeEnhancement = DetectEdgeEnhancement(original, enhanced);
            info.HasLocalContrast = info.EdgeEnhancement > 1.1;

            darkMask.Dispose();
            midMask.Dispose();
            brightMask.Dispose();

            return info;
        }

        /// <summary>
        /// 计算特定区域的变化率
        /// </summary>
        private double CalculateRegionChange(Mat original, Mat enhanced, Mat mask)
        {
            Scalar origMean = Cv2.Mean(original, mask);
            Scalar enhMean = Cv2.Mean(enhanced, mask);

            return enhMean.Val0 / Math.Max(origMean.Val0, 1.0);
        }

        /// <summary>
        /// 检测边缘增强程度
        /// </summary>
        private double DetectEdgeEnhancement(Mat original, Mat enhanced)
        {
            Mat origEdges = new Mat();
            Mat enhEdges = new Mat();

            // 使用Sobel算子检测边缘
            Cv2.Sobel(original, origEdges, MatType.CV_16U, 1, 1);
            Cv2.Sobel(enhanced, enhEdges, MatType.CV_16U, 1, 1);

            Scalar origEdgeStrength = Cv2.Mean(origEdges);
            Scalar enhEdgeStrength = Cv2.Mean(enhEdges);

            origEdges.Dispose();
            enhEdges.Dispose();

            return enhEdgeStrength.Val0 / Math.Max(origEdgeStrength.Val0, 1.0);
        }

        /// <summary>
        /// 推断可能的算法
        /// </summary>
        private string InferAlgorithm(AnalysisResult result)
        {
            var suggestions = new List<string>();

            if (Math.Abs(result.GammaEstimate - 1.0) > 0.1)
                suggestions.Add($"Gamma校正 (γ≈{result.GammaEstimate:F2})");

            if (result.ContrastRatio > 1.2)
                suggestions.Add("对比度增强");

            if (result.LocalInfo.HasLocalContrast)
                suggestions.Add("局部对比度增强/Unsharp Masking");

            if (result.LocalInfo.DarkRegionChange > result.LocalInfo.BrightRegionChange * 1.2)
                suggestions.Add("可能包含Retinex算法");

            return suggestions.Count > 0 ? string.Join(" + ", suggestions) : "线性变换";
        }

        /// <summary>
        /// 估算算法参数
        /// </summary>
        private Dictionary<string, double> EstimateParameters(AnalysisResult result)
        {
            var parameters = new Dictionary<string, double>();

            parameters["估算Gamma值"] = result.GammaEstimate;
            parameters["对比度增强倍数"] = result.ContrastRatio;
            parameters["亮度增强倍数"] = result.BrightnessChange;
            parameters["边缘增强强度"] = result.LocalInfo.EdgeEnhancement;
            parameters["暗部增强倍数"] = result.LocalInfo.DarkRegionChange;
            parameters["中间调增强倍数"] = result.LocalInfo.MidRegionChange;
            parameters["亮部增强倍数"] = result.LocalInfo.BrightRegionChange;

            return parameters;
        }
    }
}
