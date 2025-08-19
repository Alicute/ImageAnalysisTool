using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using ImageAnalysisTool.Core.Processors;

namespace ImageAnalysisTool.Core.Analyzers
{
    /// <summary>
    /// 图像增强算法逆向分析工具
    /// 通过对比原图和增强后的图像，分析可能的增强算法和参数
    /// </summary>
    public class ImageEnhancementAnalyzer
    {
        private readonly PixelProcessor pixelProcessor;
        
        public ImageEnhancementAnalyzer()
        {
            pixelProcessor = new PixelProcessor();
        }
        
        /// <summary>
        /// 分析结果数据结构
        /// </summary>
        public class AnalysisResult
        {
            public Dictionary<int, int> PixelMapping { get; set; } = new Dictionary<int, int>();
            public ushort[] CompleteLUT { get; set; } // 完整的65536项LUT
            public double[] OriginalHistogram { get; set; }
            public double[] EnhancedHistogram { get; set; }
            public double ContrastRatio { get; set; }
            public double BrightnessChange { get; set; }
            public double GammaEstimate { get; set; }
            public LocalEnhancementInfo LocalInfo { get; set; }
            public string SuggestedAlgorithm { get; set; }
            public Dictionary<string, double> EstimatedParameters { get; set; } = new Dictionary<string, double>();
            public string LUTGenerationMethod { get; set; } // LUT生成方法说明
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

            // 1. 像素映射分析 - 使用新的LUT算法
            AnalyzePixelMappingWithNewLUT(original, enhanced, result);

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
        /// 使用新的LUT算法分析像素值映射关系
        /// </summary>
        private void AnalyzePixelMappingWithNewLUT(Mat original, Mat enhanced, AnalysisResult result)
        {
            // 智能采样策略
            int totalPixels = original.Rows * original.Cols;
            bool useSampling = totalPixels > 1000000; // 超过100万像素使用采样
            int step = useSampling ? Math.Max(1, original.Rows / 100) : 1;

            // 收集像素数据
            var pixels = new List<PixelTriple>();
            for (int y = 0; y < original.Rows; y += step)
            {
                for (int x = 0; x < original.Cols; x += step)
                {
                    pixels.Add(new PixelTriple
                    {
                        Position = new System.Drawing.Point(x, y),
                        OriginalValue = original.At<ushort>(y, x),
                        TargetValue = enhanced.At<ushort>(y, x)
                    });
                }
            }

            // 使用新的LUT算法
            var lutResult = pixelProcessor.GenerateCompleteLUT(pixels);
            
            // 设置结果
            result.CompleteLUT = lutResult;
            result.LUTGenerationMethod = "中位数+多数投票+插值";
            
            // 为了兼容性，同时生成简化的映射字典
            result.PixelMapping = new Dictionary<int, int>();
            int samplingInterval = 1024; // 每1024个值取一个样本点
            for (int i = 0; i < 65536; i += samplingInterval)
            {
                if (lutResult[i] != i) // 只记录有变化的值
                {
                    result.PixelMapping[i] = lutResult[i];
                }
            }
        }

        /// <summary>
        /// 分析直方图（支持16位图像）
        /// </summary>
        private (double[], double[]) AnalyzeHistograms(Mat original, Mat enhanced)
        {
            // 根据图像位深动态确定直方图大小
            double minVal, maxVal;
            Cv2.MinMaxLoc(original, out minVal, out maxVal);

            // 使用16位直方图
            int histSize = 65536;
            float histRange = 65536f;

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
            
            // 添加LUT相关参数
            if (result.CompleteLUT != null)
            {
                // 计算LUT的非线性程度
                double nonLinearity = CalculateLUTNonLinearity(result.CompleteLUT);
                parameters["LUT非线性程度"] = nonLinearity;
                
                // 计算LUT的动态范围扩展
                int origMin = 0, origMax = 65535;
                int lutMin = result.CompleteLUT.Where(x => x > 0).Min();
                int lutMax = result.CompleteLUT.Max();
                double dynamicRangeExpansion = (double)(lutMax - lutMin) / (origMax - origMin);
                parameters["动态范围扩展倍数"] = dynamicRangeExpansion;
            }

            return parameters;
        }
        
        /// <summary>
        /// 计算LUT的非线性程度
        /// </summary>
        private double CalculateLUTNonLinearity(ushort[] lut)
        {
            if (lut == null || lut.Length == 0) return 0;
            
            // 计算LUT与线性映射的偏差
            double totalDeviation = 0;
            int samplePoints = Math.Min(1000, lut.Length); // 采样1000个点
            int step = lut.Length / samplePoints;
            
            for (int i = 0; i < lut.Length; i += step)
            {
                double expectedLinear = i; // 线性映射应该等于输入值
                double actual = lut[i];
                double deviation = Math.Abs(actual - expectedLinear) / Math.Max(expectedLinear, 1);
                totalDeviation += deviation;
            }
            
            return totalDeviation / samplePoints;
        }
    }
}
