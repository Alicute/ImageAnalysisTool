using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using OpenCvSharp;
using NLog;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 像素三元组 - 记录三张图像对应像素的信息
    /// </summary>
    public struct PixelTriple
    {
        /// <summary>
        /// 像素位置
        /// </summary>
        public System.Drawing.Point Position { get; set; }

        /// <summary>
        /// 原图灰度值
        /// </summary>
        public ushort OriginalValue { get; set; }

        /// <summary>
        /// 我的增强图灰度值
        /// </summary>
        public ushort MyEnhancedValue { get; set; }

        /// <summary>
        /// 目标图灰度值
        /// </summary>
        public ushort TargetValue { get; set; }

        /// <summary>
        /// 处理后的值
        /// </summary>
        public ushort ProcessedValue { get; set; }

        /// <summary>
        /// 原图到目标图的变化量
        /// </summary>
        public int OriginalToTargetChange => TargetValue - OriginalValue;

        /// <summary>
        /// 原图到我的增强图的变化量
        /// </summary>
        public int OriginalToMyEnhancedChange => MyEnhancedValue - OriginalValue;

        /// <summary>
        /// 目标图与我的增强图的差异
        /// </summary>
        public int TargetToMyEnhancedDifference => TargetValue - MyEnhancedValue;
    }

    /// <summary>
    /// 像素级图像处理器 - 核心处理逻辑
    /// </summary>
    public class PixelProcessor
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 获取三图像素对应关系
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="myEnhanced">我的增强图</param>
        /// <param name="target">目标图</param>
        /// <param name="sampleRate">采样率 (0.01-1.0)，大图像建议使用较小值</param>
        /// <returns>像素三元组列表</returns>
        public List<PixelTriple> GetPixelTriples(Mat original, Mat myEnhanced, Mat target, double sampleRate = 0.1)
        {
            if (original == null || original.Empty())
                throw new ArgumentException("原图不能为空");

            var triples = new List<PixelTriple>();
            
            try
            {
                int rows = original.Rows;
                int cols = original.Cols;
                
                // 计算采样步长
                int totalPixels = rows * cols;
                int targetSampleCount = (int)(totalPixels * sampleRate);
                int step = Math.Max(1, (int)Math.Sqrt(totalPixels / (double)targetSampleCount));
                
                logger.Info($"开始获取像素三元组 - 图像尺寸: {cols}x{rows}, 采样步长: {step}, 预计采样数: {targetSampleCount:N0}");

                var stopwatch = Stopwatch.StartNew();

                for (int y = 0; y < rows; y += step)
                {
                    for (int x = 0; x < cols; x += step)
                    {
                        var triple = new PixelTriple
                        {
                            Position = new System.Drawing.Point(x, y),
                            OriginalValue = original.At<ushort>(y, x)
                        };

                        // 获取我的增强图像素值
                        if (myEnhanced != null && !myEnhanced.Empty())
                        {
                            triple.MyEnhancedValue = myEnhanced.At<ushort>(y, x);
                        }

                        // 获取目标图像素值
                        if (target != null && !target.Empty())
                        {
                            triple.TargetValue = target.At<ushort>(y, x);
                        }

                        triples.Add(triple);
                    }
                }

                stopwatch.Stop();
                logger.Info($"像素三元组获取完成 - 实际采样数: {triples.Count:N0}, 耗时: {stopwatch.ElapsedMilliseconds}ms");

                return triples;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "获取像素三元组失败");
                throw;
            }
        }

        /// <summary>
        /// 应用处理规则到图像
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="rule">处理规则</param>
        /// <returns>处理后的图像</returns>
        public Mat ApplyProcessingRule(Mat input, ProcessingRule rule)
        {
            if (input == null || input.Empty())
                throw new ArgumentException("输入图像不能为空");

            if (rule?.Transform == null)
                throw new ArgumentException("处理规则或变换函数不能为空");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = input.Clone();
                
                int processedCount = 0;
                var changes = new List<int>();
                double beforeSum = 0, afterSum = 0;

                // 确定处理区域
                var region = rule.ApplyRegion ?? new Rectangle(0, 0, input.Cols, input.Rows);
                
                logger.Info($"开始应用处理规则: {rule.RuleName}, 区域: {region}");

                // 应用变换
                for (int y = region.Y; y < Math.Min(region.Bottom, input.Rows); y++)
                {
                    for (int x = region.X; x < Math.Min(region.Right, input.Cols); x++)
                    {
                        ushort originalValue = input.At<ushort>(y, x);
                        ushort newValue = rule.Transform(originalValue);
                        
                        result.Set(y, x, newValue);
                        
                        // 统计信息
                        processedCount++;
                        int change = newValue - originalValue;
                        changes.Add(change);
                        beforeSum += originalValue;
                        afterSum += newValue;
                    }
                }

                stopwatch.Stop();

                // 更新统计信息
                rule.Statistics.ProcessedPixelCount = processedCount;
                rule.Statistics.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                rule.Statistics.BeforeAverageGray = beforeSum / processedCount;
                rule.Statistics.AfterAverageGray = afterSum / processedCount;
                rule.Statistics.AverageChange = changes.Average();
                rule.Statistics.MaxChange = changes.Max();
                rule.Statistics.MinChange = changes.Min();
                rule.Statistics.ChangeStandardDeviation = CalculateStandardDeviation(changes);

                logger.Info($"处理规则应用完成 - 处理像素数: {processedCount:N0}, 耗时: {stopwatch.ElapsedMilliseconds}ms");

                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"应用处理规则失败: {rule.RuleName}");
                throw;
            }
        }

        /// <summary>
        /// 分析像素映射规律
        /// </summary>
        /// <param name="pixels">像素三元组列表</param>
        /// <returns>映射字典 (原图值 -> 目标图值)</returns>
        public Dictionary<int, int> AnalyzePixelMapping(List<PixelTriple> pixels)
        {
            if (pixels == null || pixels.Count == 0)
                return new Dictionary<int, int>();

            try
            {
                logger.Info($"开始分析像素映射规律 - 像素数: {pixels.Count:N0}");

                var mapping = new Dictionary<int, List<int>>();

                // 检测图像位深
                int maxOriginal = pixels.Max(p => p.OriginalValue);
                int maxTarget = pixels.Max(p => p.TargetValue);
                bool is16Bit = maxOriginal > 255 || maxTarget > 255;
                
                // 根据位深确定分组策略
                int binSize = is16Bit ? 64 : 1; // 16位图像分组，8位图像不分组
                
                logger.Debug($"检测到图像位深: {(is16Bit ? "16位" : "8位")}, 分组大小: {binSize}");

                // 分组统计
                foreach (var pixel in pixels)
                {
                    int origKey = is16Bit ? (pixel.OriginalValue / binSize) * binSize : pixel.OriginalValue;
                    int targetValue = pixel.TargetValue;

                    if (!mapping.ContainsKey(origKey))
                        mapping[origKey] = new List<int>();
                    
                    mapping[origKey].Add(targetValue);
                }

                // 计算每个区间的平均映射值
                var result = new Dictionary<int, int>();
                foreach (var kvp in mapping)
                {
                    if (kvp.Value.Count > 0)
                    {
                        result[kvp.Key] = (int)kvp.Value.Average();
                    }
                }

                logger.Info($"像素映射分析完成 - 映射点数: {result.Count}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "分析像素映射规律失败");
                throw;
            }
        }

        /// <summary>
        /// 直接映射处理：将原图按照目标图的像素值进行映射
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="target">目标图</param>
        /// <param name="sampleRate">采样率</param>
        /// <returns>处理后的图像</returns>
        public Mat DirectMapping(Mat original, Mat target, double sampleRate = 0.1)
        {
            if (original == null || target == null)
                throw new ArgumentException("原图和目标图都不能为空");

            try
            {
                logger.Info("开始直接映射处理");

                // 获取像素对应关系
                var pixels = GetPixelTriples(original, null, target, sampleRate);
                
                // 分析映射规律
                var mapping = AnalyzePixelMapping(pixels);
                
                // 创建处理规则
                var rule = ProcessingRule.CreateDirectMapping("直接映射", mapping);
                
                // 应用处理规则
                return ApplyProcessingRule(original, rule);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "直接映射处理失败");
                throw;
            }
        }

        /// <summary>
        /// 数学运算处理
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="operation">运算类型</param>
        /// <param name="value">运算值</param>
        /// <returns>处理后的图像</returns>
        public Mat MathOperation(Mat input, MathOperationType operation, double value)
        {
            if (input == null || input.Empty())
                throw new ArgumentException("输入图像不能为空");

            try
            {
                logger.Info($"开始数学运算处理: {operation} {value}");

                // 创建处理规则
                var rule = ProcessingRule.CreateMathOperation($"{operation}_{value}", operation, value);
                
                // 应用处理规则
                return ApplyProcessingRule(input, rule);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"数学运算处理失败: {operation} {value}");
                throw;
            }
        }

        /// <summary>
        /// 生成AI友好的详细处理规律报告
        /// </summary>
        /// <param name="rules">处理规则列表</param>
        /// <returns>详细报告字符串</returns>
        public string GenerateProcessingReport(List<ProcessingRule> rules)
        {
            if (rules == null || rules.Count == 0)
                return "没有处理规则记录";

            try
            {
                var report = "=== AI图像处理算法逆向分析报告 ===\n\n";
                report += $"报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                report += $"处理步骤总数: {rules.Count}\n";
                report += "报告用途: 供AI分析图像增强算法的具体实现方式\n\n";

                // 1. 处理流程概览
                report += "=== 处理流程概览 ===\n";
                for (int i = 0; i < rules.Count; i++)
                {
                    var rule = rules[i];
                    report += $"步骤{i + 1}: {rule.RuleName} ({rule.Type}) - {rule.CreateTime:HH:mm:ss}\n";
                }
                report += "\n";

                // 2. 详细处理分析
                for (int i = 0; i < rules.Count; i++)
                {
                    report += $"=== 步骤{i + 1}详细分析: {rules[i].RuleName} ===\n";
                    report += GenerateDetailedRuleAnalysis(rules[i]);
                    report += "\n";
                }

                // 3. 算法推断分析
                report += "=== 算法推断与建议 ===\n";
                report += GenerateAlgorithmInference(rules);

                return report;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "生成处理规律报告失败");
                return $"生成报告失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 生成单个规则的详细分析
        /// </summary>
        private string GenerateDetailedRuleAnalysis(ProcessingRule rule)
        {
            var analysis = "";

            // 基本信息
            analysis += $"处理类型: {rule.Type}\n";
            analysis += $"处理描述: {rule.Description}\n";
            analysis += $"处理时间: {rule.CreateTime:yyyy-MM-dd HH:mm:ss}\n";

            if (rule.ApplyRegion.HasValue)
            {
                var region = rule.ApplyRegion.Value;
                analysis += $"处理区域: ({region.X}, {region.Y}) 到 ({region.Right}, {region.Bottom}), 大小: {region.Width}x{region.Height}\n";
            }
            else
            {
                analysis += "处理区域: 全图\n";
            }

            // 参数详情
            analysis += "\n参数详情:\n";
            foreach (var param in rule.Parameters)
            {
                if (param.Key == "Mapping" && param.Value is Dictionary<int, int> mapping)
                {
                    analysis += $"  像素映射关系: {mapping.Count} 个映射点\n";
                    analysis += GenerateMappingAnalysis(mapping);
                }
                else
                {
                    analysis += $"  {param.Key}: {param.Value}\n";
                }
            }

            // 统计信息
            if (rule.Statistics != null)
            {
                analysis += "\n处理效果统计:\n";
                analysis += $"  处理像素总数: {rule.Statistics.ProcessedPixelCount:N0}\n";
                analysis += $"  平均灰度变化: {rule.Statistics.AverageChange:F2}\n";
                analysis += $"  最大变化量: {rule.Statistics.MaxChange}\n";
                analysis += $"  最小变化量: {rule.Statistics.MinChange}\n";
                analysis += $"  变化标准差: {rule.Statistics.ChangeStandardDeviation:F2}\n";
                analysis += $"  处理前平均灰度: {rule.Statistics.BeforeAverageGray:F1}\n";
                analysis += $"  处理后平均灰度: {rule.Statistics.AfterAverageGray:F1}\n";
                analysis += $"  整体亮度变化: {(rule.Statistics.AfterAverageGray - rule.Statistics.BeforeAverageGray):F1}\n";
                analysis += $"  处理耗时: {rule.Statistics.ProcessingTimeMs}ms\n";
            }

            return analysis;
        }

        /// <summary>
        /// 生成像素映射关系的详细分析
        /// </summary>
        private string GenerateMappingAnalysis(Dictionary<int, int> mapping)
        {
            if (mapping.Count == 0) return "  映射关系为空\n";

            var analysis = "";

            // 映射关系采样分析（显示关键点）
            var sortedMapping = mapping.OrderBy(kvp => kvp.Key).ToList();

            analysis += "  关键映射点分析:\n";

            // 显示前10个、中间10个、后10个映射点
            var keyPoints = new List<KeyValuePair<int, int>>();

            // 前10个
            keyPoints.AddRange(sortedMapping.Take(Math.Min(10, sortedMapping.Count)));

            // 中间10个
            if (sortedMapping.Count > 20)
            {
                int midStart = (sortedMapping.Count - 10) / 2;
                keyPoints.AddRange(sortedMapping.Skip(midStart).Take(10));
            }

            // 后10个
            if (sortedMapping.Count > 10)
            {
                keyPoints.AddRange(sortedMapping.Skip(Math.Max(0, sortedMapping.Count - 10)));
            }

            // 去重并排序
            keyPoints = keyPoints.Distinct().OrderBy(kvp => kvp.Key).ToList();

            foreach (var point in keyPoints)
            {
                int change = point.Value - point.Key;
                string changeStr = change >= 0 ? $"+{change}" : change.ToString();
                analysis += $"    原值{point.Key} → 新值{point.Value} (变化: {changeStr})\n";
            }

            // 映射关系特征分析
            analysis += "\n  映射特征分析:\n";

            // 计算映射的线性度
            var linearityAnalysis = AnalyzeMappingLinearity(mapping);
            analysis += $"    线性度: {linearityAnalysis.Linearity:F3} (1.0=完全线性, 0.0=完全非线性)\n";
            analysis += $"    斜率: {linearityAnalysis.Slope:F3}\n";
            analysis += $"    截距: {linearityAnalysis.Intercept:F1}\n";

            // 分析映射类型
            var mappingType = AnalyzeMappingType(mapping);
            analysis += $"    映射类型: {mappingType}\n";

            // 分析增强效果
            var enhancementEffect = AnalyzeEnhancementEffect(mapping);
            analysis += $"    增强效果: {enhancementEffect}\n";

            return analysis;
        }

        /// <summary>
        /// 分析处理模式
        /// </summary>
        private string AnalyzeProcessingPatterns(List<ProcessingRule> rules)
        {
            var analysis = "";
            
            // 统计处理类型
            var typeStats = rules.GroupBy(r => r.Type)
                                 .ToDictionary(g => g.Key, g => g.Count());
            
            analysis += "处理类型统计:\n";
            foreach (var stat in typeStats)
            {
                analysis += $"  {stat.Key}: {stat.Value} 次\n";
            }

            // 分析平均变化量
            var avgChanges = rules.Where(r => r.Statistics != null)
                                 .Select(r => r.Statistics.AverageChange)
                                 .ToList();
            
            if (avgChanges.Count > 0)
            {
                analysis += $"\n平均变化量统计:\n";
                analysis += $"  最小: {avgChanges.Min():F2}\n";
                analysis += $"  最大: {avgChanges.Max():F2}\n";
                analysis += $"  平均: {avgChanges.Average():F2}\n";
            }

            return analysis;
        }

        /// <summary>
        /// 生成算法推断分析
        /// </summary>
        private string GenerateAlgorithmInference(List<ProcessingRule> rules)
        {
            var inference = "";

            inference += "基于处理步骤的算法推断:\n\n";

            // 分析处理类型组合
            var typeSequence = string.Join(" → ", rules.Select(r => r.Type.ToString()));
            inference += $"处理序列: {typeSequence}\n\n";

            // 分析整体效果
            if (rules.Any(r => r.Statistics != null))
            {
                var totalBrightnessChange = rules.Where(r => r.Statistics != null)
                                                .Sum(r => r.Statistics.AfterAverageGray - r.Statistics.BeforeAverageGray);

                inference += $"整体亮度变化: {totalBrightnessChange:F1}\n";

                if (Math.Abs(totalBrightnessChange) < 5)
                    inference += "推断: 主要进行对比度调整，保持整体亮度\n";
                else if (totalBrightnessChange > 0)
                    inference += "推断: 进行亮度提升处理\n";
                else
                    inference += "推断: 进行亮度降低处理\n";
            }

            // 针对直接映射的特殊分析
            var directMappingRules = rules.Where(r => r.Type == ProcessingType.DirectMapping).ToList();
            if (directMappingRules.Any())
            {
                inference += "\n直接映射分析:\n";
                foreach (var rule in directMappingRules)
                {
                    if (rule.Parameters.ContainsKey("Mapping") && rule.Parameters["Mapping"] is Dictionary<int, int> mapping)
                    {
                        var mappingType = AnalyzeMappingType(mapping);
                        var enhancementEffect = AnalyzeEnhancementEffect(mapping);

                        inference += $"  映射类型: {mappingType}\n";
                        inference += $"  增强效果: {enhancementEffect}\n";

                        // 提供算法建议
                        inference += "  算法建议:\n";
                        inference += GenerateAlgorithmSuggestions(mapping);
                    }
                }
            }

            inference += "\n=== AI分析建议 ===\n";
            inference += "请AI根据以上数据分析:\n";
            inference += "1. 可能使用的图像增强算法类型\n";
            inference += "2. 算法的具体参数设置\n";
            inference += "3. 处理的目标效果（亮度增强、对比度调整、细节增强等）\n";
            inference += "4. 可能的算法实现方式（线性变换、非线性变换、分段处理等）\n";

            return inference;
        }

        /// <summary>
        /// 分析映射关系的线性度
        /// </summary>
        private (double Linearity, double Slope, double Intercept) AnalyzeMappingLinearity(Dictionary<int, int> mapping)
        {
            if (mapping.Count < 2) return (0, 0, 0);

            var points = mapping.OrderBy(kvp => kvp.Key).ToList();

            // 计算线性回归
            double sumX = points.Sum(p => p.Key);
            double sumY = points.Sum(p => p.Value);
            double sumXY = points.Sum(p => p.Key * p.Value);
            double sumX2 = points.Sum(p => p.Key * p.Key);
            int n = points.Count;

            double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            double intercept = (sumY - slope * sumX) / n;

            // 计算R²来评估线性度
            double meanY = sumY / n;
            double ssTotal = points.Sum(p => Math.Pow(p.Value - meanY, 2));
            double ssRes = points.Sum(p => Math.Pow(p.Value - (slope * p.Key + intercept), 2));
            double rSquared = 1 - (ssRes / ssTotal);

            return (rSquared, slope, intercept);
        }

        /// <summary>
        /// 分析映射类型
        /// </summary>
        private string AnalyzeMappingType(Dictionary<int, int> mapping)
        {
            if (mapping.Count == 0) return "无映射";

            var linearity = AnalyzeMappingLinearity(mapping);

            if (linearity.Linearity > 0.95)
            {
                if (Math.Abs(linearity.Slope - 1.0) < 0.1)
                    return "线性平移变换";
                else if (linearity.Slope > 1.0)
                    return "线性放大变换";
                else
                    return "线性缩放变换";
            }
            else if (linearity.Linearity > 0.8)
            {
                return "近似线性变换";
            }
            else
            {
                // 分析是否为Gamma变换
                if (IsGammaTransform(mapping))
                    return "Gamma校正变换";
                else if (IsContrastEnhancement(mapping))
                    return "对比度增强变换";
                else
                    return "复杂非线性变换";
            }
        }

        /// <summary>
        /// 分析增强效果
        /// </summary>
        private string AnalyzeEnhancementEffect(Dictionary<int, int> mapping)
        {
            if (mapping.Count == 0) return "无效果";

            var points = mapping.OrderBy(kvp => kvp.Key).ToList();

            // 分析暗部、中间调、亮部的变化
            int darkThreshold = 65535 / 4;      // 暗部阈值
            int brightThreshold = 65535 * 3 / 4; // 亮部阈值

            var darkChanges = points.Where(p => p.Key < darkThreshold).Select(p => p.Value - p.Key).ToList();
            var midChanges = points.Where(p => p.Key >= darkThreshold && p.Key < brightThreshold).Select(p => p.Value - p.Key).ToList();
            var brightChanges = points.Where(p => p.Key >= brightThreshold).Select(p => p.Value - p.Key).ToList();

            var effects = new List<string>();

            if (darkChanges.Any() && darkChanges.Average() > 100)
                effects.Add("暗部提亮");
            else if (darkChanges.Any() && darkChanges.Average() < -100)
                effects.Add("暗部压暗");

            if (midChanges.Any() && midChanges.Average() > 100)
                effects.Add("中间调提亮");
            else if (midChanges.Any() && midChanges.Average() < -100)
                effects.Add("中间调压暗");

            if (brightChanges.Any() && brightChanges.Average() > 100)
                effects.Add("亮部增强");
            else if (brightChanges.Any() && brightChanges.Average() < -100)
                effects.Add("亮部抑制");

            // 分析对比度变化
            var inputRange = points.Max(p => p.Key) - points.Min(p => p.Key);
            var outputRange = points.Max(p => p.Value) - points.Min(p => p.Value);

            if (outputRange > inputRange * 1.2)
                effects.Add("对比度增强");
            else if (outputRange < inputRange * 0.8)
                effects.Add("对比度降低");

            return effects.Any() ? string.Join(", ", effects) : "轻微调整";
        }

        /// <summary>
        /// 生成算法建议
        /// </summary>
        private string GenerateAlgorithmSuggestions(Dictionary<int, int> mapping)
        {
            var suggestions = "";

            var linearity = AnalyzeMappingLinearity(mapping);

            if (linearity.Linearity > 0.95)
            {
                suggestions += $"    - 线性变换: y = {linearity.Slope:F3}x + {linearity.Intercept:F1}\n";
                suggestions += "    - 可使用简单的线性映射实现\n";
            }
            else
            {
                if (IsGammaTransform(mapping))
                {
                    var gamma = EstimateGammaValue(mapping);
                    suggestions += $"    - Gamma校正: y = (x/65535)^{gamma:F2} * 65535\n";
                }
                else
                {
                    suggestions += "    - 建议使用查找表(LUT)实现\n";
                    suggestions += "    - 或考虑分段线性插值\n";
                }
            }

            return suggestions;
        }

        /// <summary>
        /// 判断是否为Gamma变换
        /// </summary>
        private bool IsGammaTransform(Dictionary<int, int> mapping)
        {
            // 简化的Gamma检测逻辑
            var points = mapping.OrderBy(kvp => kvp.Key).Take(10).ToList();
            if (points.Count < 3) return false;

            // 检查是否符合幂函数特征
            double correlation = 0;
            try
            {
                var logX = points.Select(p => Math.Log(Math.Max(1, p.Key))).ToList();
                var logY = points.Select(p => Math.Log(Math.Max(1, p.Value))).ToList();

                correlation = CalculateCorrelation(logX, logY);
            }
            catch
            {
                return false;
            }

            return correlation > 0.9;
        }

        /// <summary>
        /// 判断是否为对比度增强
        /// </summary>
        private bool IsContrastEnhancement(Dictionary<int, int> mapping)
        {
            var points = mapping.OrderBy(kvp => kvp.Key).ToList();
            if (points.Count < 3) return false;

            // 检查是否有S型曲线特征
            int midPoint = points.Count / 2;
            var firstHalf = points.Take(midPoint);
            var secondHalf = points.Skip(midPoint);

            double firstSlope = firstHalf.Count() > 1 ?
                (firstHalf.Last().Value - firstHalf.First().Value) / (double)(firstHalf.Last().Key - firstHalf.First().Key) : 1;
            double secondSlope = secondHalf.Count() > 1 ?
                (secondHalf.Last().Value - secondHalf.First().Value) / (double)(secondHalf.Last().Key - secondHalf.First().Key) : 1;

            return firstSlope > 1.2 && secondSlope > 1.2; // 两段都有增强效果
        }

        /// <summary>
        /// 估算Gamma值
        /// </summary>
        private double EstimateGammaValue(Dictionary<int, int> mapping)
        {
            var points = mapping.OrderBy(kvp => kvp.Key).Where(p => p.Key > 0 && p.Value > 0).Take(10).ToList();
            if (points.Count < 2) return 1.0;

            try
            {
                // 使用中点估算
                var midPoint = points[points.Count / 2];
                double normalizedX = midPoint.Key / 65535.0;
                double normalizedY = midPoint.Value / 65535.0;

                return Math.Log(normalizedY) / Math.Log(normalizedX);
            }
            catch
            {
                return 1.0;
            }
        }

        /// <summary>
        /// 计算相关系数
        /// </summary>
        private double CalculateCorrelation(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0) return 0;

            double meanX = x.Average();
            double meanY = y.Average();

            double numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
            double denominator = Math.Sqrt(x.Sum(xi => Math.Pow(xi - meanX, 2)) * y.Sum(yi => Math.Pow(yi - meanY, 2)));

            return denominator == 0 ? 0 : numerator / denominator;
        }

        /// <summary>
        /// 计算标准差
        /// </summary>
        private double CalculateStandardDeviation(List<int> values)
        {
            if (values.Count == 0) return 0;

            double mean = values.Average();
            double sumSquaredDifferences = values.Sum(v => Math.Pow(v - mean, 2));
            return Math.Sqrt(sumSquaredDifferences / values.Count);
        }
    }
}
