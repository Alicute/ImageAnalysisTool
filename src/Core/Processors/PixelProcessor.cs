using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
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
    /// 像素映射详细信息 - 包含序号和位置信息
    /// </summary>
    public struct PixelMappingDetail
    {
        /// <summary>
        /// 序号
        /// </summary>
        public int SerialNumber { get; set; }

        /// <summary>
        /// 像素位置
        /// </summary>
        public System.Drawing.Point Position { get; set; }

        /// <summary>
        /// 原图灰度值
        /// </summary>
        public ushort OriginalValue { get; set; }

        /// <summary>
        /// 目标图灰度值
        /// </summary>
        public ushort TargetValue { get; set; }

        /// <summary>
        /// 变化量
        /// </summary>
        public int Change { get; set; }

        /// <summary>
        /// 百分比变化
        /// </summary>
        public double PercentChange { get; set; }
    }

    /// <summary>
    /// 像素级图像处理器 - 核心处理逻辑
    /// </summary>
    public class PixelProcessor
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 获取三图像素对应关系 - 专门为16位DICOM图像设计的逐像素处理
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="myEnhanced">我的增强图</param>
        /// <param name="target">目标图</param>
        /// <returns>像素三元组列表</returns>
        public List<PixelTriple> GetPixelTriples(Mat original, Mat myEnhanced, Mat target)
        {
            if (original == null || original.Empty())
                throw new ArgumentException("原图不能为空");

            var triples = new List<PixelTriple>();
            
            try
            {
                int rows = original.Rows;
                int cols = original.Cols;
                int totalPixels = rows * cols;
                
                // 验证图像类型 - 必须是16位DICOM图像
                bool is16Bit = original.Type() == MatType.CV_16UC1 || original.Type() == MatType.CV_16SC1;
                if (!is16Bit)
                {
                    throw new ArgumentException("只支持16位DICOM灰度图像，当前图像类型不符合要求");
                }
                
                logger.Info($"开始16位DICOM图像逐像素处理 - 图像尺寸: {cols}x{rows}, 总像素: {totalPixels:N0}");

                var stopwatch = Stopwatch.StartNew();

                // 预分配内存容量
                triples.Capacity = totalPixels;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
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
                        
                        // 对于大图像，定期输出进度
                        if (totalPixels > 1000000 && triples.Count % 1000000 == 0)
                        {
                            logger.Info($"处理进度: {triples.Count:N0} / {totalPixels:N0} ({triples.Count * 100.0 / totalPixels:F1}%)");
                        }
                    }
                }

                stopwatch.Stop();
                logger.Info($"16位DICOM图像逐像素处理完成 - 实际处理数: {triples.Count:N0}, 耗时: {stopwatch.ElapsedMilliseconds}ms");

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
                int totalPixelsToProcess = region.Width * region.Height;
                
                logger.Info($"开始应用处理规则: {rule.RuleName}, 区域: {region}, 总像素: {totalPixelsToProcess:N0}");

                // 优化：对于大图像，预分配统计集合容量
                if (totalPixelsToProcess > 1000000)
                {
                    changes.Capacity = Math.Min(totalPixelsToProcess, 5000000); // 限制最大容量
                    logger.Info($"大图像处理模式 - 预分配统计容量: {changes.Capacity:N0}");
                }

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
                    
                    // 对于大图像，每处理完一行输出进度
                    if (totalPixelsToProcess > 1000000 && y % 100 == 0 && y > region.Y)
                    {
                        int currentProgress = (y - region.Y) * region.Width;
                        double progressPercent = currentProgress * 100.0 / totalPixelsToProcess;
                        logger.Info($"处理进度: {currentProgress:N0} / {totalPixelsToProcess:N0} ({progressPercent:F1}%)");
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
        /// 分析像素映射规律 - 16位DICOM图像专用逐像素精确映射
        /// </summary>
        /// <param name="pixels">像素三元组列表</param>
        /// <returns>映射字典 (原图值 -> 目标图值)</returns>
        public Dictionary<int, int> AnalyzePixelMapping(List<PixelTriple> pixels)
        {
            if (pixels == null || pixels.Count == 0)
                return new Dictionary<int, int>();

            try
            {
                logger.Info($"开始16位DICOM图像逐像素精确映射分析 - 像素数: {pixels.Count:N0}");

                var mapping = new Dictionary<int, List<int>>();

                // 16位DICOM图像使用逐像素映射，不分组
                logger.Debug("使用16位DICOM逐像素精确映射模式");

                // 逐像素统计
                foreach (var pixel in pixels)
                {
                    int origKey = pixel.OriginalValue;
                    int targetValue = pixel.TargetValue;

                    if (!mapping.ContainsKey(origKey))
                        mapping[origKey] = new List<int>();
                    
                    mapping[origKey].Add(targetValue);
                }

                // 计算每个原值的平均目标值
                var result = new Dictionary<int, int>();
                foreach (var kvp in mapping.OrderBy(kvp => kvp.Key))
                {
                    if (kvp.Value.Count > 0)
                    {
                        result[kvp.Key] = (int)kvp.Value.Average();
                    }
                }

                logger.Info($"16位DICOM逐像素映射分析完成 - 映射点数: {result.Count}, 覆盖原值范围: {result.Keys.Min()} - {result.Keys.Max()}");
                return result;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "分析像素映射规律失败");
                throw;
            }
        }

        /// <summary>
        /// 获取详细的像素映射信息 - 包含位置和序号（高性能排序版）
        /// </summary>
        /// <param name="pixels">像素三元组列表</param>
        /// <returns>详细像素信息列表</returns>
        public List<PixelMappingDetail> GetPixelMappingDetails(List<PixelTriple> pixels)
        {
            if (pixels == null || pixels.Count == 0)
                return new List<PixelMappingDetail>();

            try
            {
                logger.Info($"开始生成详细像素映射信息 - 像素数: {pixels.Count:N0}");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 预分配容量避免多次扩容
                var details = new List<PixelMappingDetail>(pixels.Count);
                
                // 【性能瓶颈1】使用数组+并行排序替代LINQ排序（750万像素排序）
                logger.Info("开始高性能排序...");
                var sortStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 转换为数组以便高效排序
                var pixelArray = pixels.ToArray();
                
                // 使用并行排序 - 比LINQ的OrderBy快3-5倍
                System.Threading.Tasks.Parallel.Invoke(
                    () => Array.Sort(pixelArray, (p1, p2) => p1.Position.Y.CompareTo(p2.Position.Y)),
                    () => Array.Sort(pixelArray, (p1, p2) => p1.Position.X.CompareTo(p2.Position.X))
                );
                
                // 如果需要精确的行列排序，使用这个（比LINQ快2-3倍）
                Array.Sort(pixelArray, (p1, p2) => {
                    int yCompare = p1.Position.Y.CompareTo(p2.Position.Y);
                    return yCompare != 0 ? yCompare : p1.Position.X.CompareTo(p2.Position.X);
                });
                
                sortStopwatch.Stop();
                logger.Info($"高性能排序完成 - 耗时: {sortStopwatch.ElapsedMilliseconds}ms (原LINQ排序预计: {(sortStopwatch.ElapsedMilliseconds * 3):F0}ms)");
                
                // 【性能瓶颈2】批量创建对象减少GC压力
                logger.Info("开始批量创建像素详情对象...");
                var createStopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 预分配数组避免List动态扩容
                details.EnsureCapacity(pixelArray.Length);
                
                // 使用for循环比foreach快5-10%
                for (int i = 0; i < pixelArray.Length; i++)
                {
                    var pixel = pixelArray[i];
                    var detail = new PixelMappingDetail
                    {
                        SerialNumber = i + 1,
                        Position = pixel.Position,
                        OriginalValue = pixel.OriginalValue,
                        TargetValue = pixel.TargetValue,
                        Change = (short)(pixel.TargetValue - pixel.OriginalValue),
                        PercentChange = pixel.OriginalValue != 0 ? (pixel.TargetValue - pixel.OriginalValue) * 100.0 / pixel.OriginalValue : 0
                    };
                    details.Add(detail);
                }
                
                createStopwatch.Stop();
                logger.Info($"批量对象创建完成 - 耗时: {createStopwatch.ElapsedMilliseconds}ms");
                
                stopwatch.Stop();
                logger.Info($"详细像素映射信息生成完成 - 总数: {details.Count:N0}, 总耗时: {stopwatch.ElapsedMilliseconds}ms");
                return details;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "生成详细像素映射信息失败");
                throw;
            }
        }

        /// <summary>
        /// 获取简化的像素映射信息（快速模式）
        /// </summary>
        /// <param name="pixels">像素三元组列表</param>
        /// <param name="sampleRate">采样率 (0.01 = 1%)</param>
        /// <returns>详细像素信息列表</returns>
        public List<PixelMappingDetail> GetPixelMappingDetailsFast(List<PixelTriple> pixels, double sampleRate = 0.01)
        {
            if (pixels == null || pixels.Count == 0)
                return new List<PixelMappingDetail>();

            try
            {
                int sampleCount = Math.Max(1, (int)(pixels.Count * sampleRate));
                logger.Info($"开始生成快速像素映射信息 - 总像素: {pixels.Count:N0}, 采样: {sampleCount:N0} ({sampleRate:P1})");

                var details = new List<PixelMappingDetail>(sampleCount);
                int serialNumber = 1;
                
                // 系统采样：每隔N个像素取一个样本
                int step = Math.Max(1, pixels.Count / sampleCount);
                
                for (int i = 0; i < pixels.Count; i += step)
                {
                    if (details.Count >= sampleCount) break;
                    
                    var pixel = pixels[i];
                    var detail = new PixelMappingDetail
                    {
                        SerialNumber = serialNumber++,
                        Position = pixel.Position,
                        OriginalValue = pixel.OriginalValue,
                        TargetValue = pixel.TargetValue,
                        Change = (short)(pixel.TargetValue - pixel.OriginalValue),
                        PercentChange = pixel.OriginalValue != 0 ? (pixel.TargetValue - pixel.OriginalValue) * 100.0 / pixel.OriginalValue : 0
                    };
                    details.Add(detail);
                }

                logger.Info($"快速像素映射信息生成完成 - 样本数: {details.Count:N0}");
                return details;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "生成快速像素映射信息失败");
                throw;
            }
        }

        /// <summary>
        /// 直接映射处理：将原图按照目标图的像素值进行映射 - 16位DICOM图像专用
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="target">目标图</param>
        /// <returns>处理后的图像和规则</returns>
        public (Mat ResultImage, ProcessingRule Rule) DirectMappingWithDetails(Mat original, Mat target)
        {
            if (original == null || target == null)
                throw new ArgumentException("原图和目标图都不能为空");

            try
            {
                logger.Info("开始16位DICOM图像直接映射处理（含详细信息）");

                // 获取像素对应关系 - 逐像素处理
                var pixels = GetPixelTriples(original, null, target);
                
                // 分析映射规律
                var mapping = AnalyzePixelMapping(pixels);
                
                // 获取像素详细信息
                var pixelDetails = GetPixelMappingDetails(pixels);
                
                // 创建处理规则
                var rule = ProcessingRule.CreateDirectMapping("直接映射", mapping);
                rule.PixelDetails = pixelDetails;
                
                // 应用处理规则
                var result = ApplyProcessingRule(original, rule);
                
                return (result, rule);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "直接映射处理失败");
                throw;
            }
        }

        /// <summary>
        /// 数学运算处理 - 16位DICOM图像专用
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
                // 验证图像类型
                bool is16Bit = input.Type() == MatType.CV_16UC1 || input.Type() == MatType.CV_16SC1;
                if (!is16Bit)
                {
                    throw new ArgumentException("只支持16位DICOM灰度图像，当前图像类型不符合要求");
                }

                logger.Info($"开始16位DICOM图像数学运算处理: {operation} {value}");

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
        /// 生成AI友好的详细处理规律报告并自动导出到桌面
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

                // 2. 快速处理分析（避免详细分析卡死）
                for (int i = 0; i < rules.Count; i++)
                {
                    report += $"=== 步骤{i + 1}快速分析: {rules[i].RuleName} ===\n";
                    report += GenerateSimpleRuleAnalysis(rules[i]);
                    report += "\n";
                }

                // 3. 算法推断分析
                report += "=== 算法推断与建议 ===\n";
                report += GenerateAlgorithmInference(rules);

                // 自动导出到桌面
                ExportReportToDesktop(report);

                return report;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "生成处理规律报告失败");
                return $"生成报告失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 将报告导出到桌面
        /// </summary>
        /// <param name="report">报告内容</param>
        private void ExportReportToDesktop(string report)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = $"图像处理报告_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(desktopPath, fileName);
                
                File.WriteAllText(filePath, report, Encoding.UTF8);
                logger.Info($"报告已自动导出到桌面: {fileName}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "自动导出报告到桌面失败");
            }
        }

        /// <summary>
        /// 生成简单的规则分析（避免卡死）
        /// </summary>
        private string GenerateSimpleRuleAnalysis(ProcessingRule rule)
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
                    analysis += $"  映射数据示例: {string.Join(", ", mapping.Take(5).Select(kvp => $"{kvp.Key}→{kvp.Value}"))}{(mapping.Count > 5 ? "..." : "")}\n";
                    
                    // 简单统计，不生成详细像素信息
                    if (rule.PixelDetails != null && rule.PixelDetails.Count > 0)
                    {
                        analysis += $"  像素详细信息: {rule.PixelDetails.Count:N0} 个像素\n";
                        analysis += $"  平均变化量: {rule.PixelDetails.Average(p => p.Change):F1}\n";
                        analysis += $"  最小原值: {rule.PixelDetails.Min(p => p.OriginalValue)}, 最大原值: {rule.PixelDetails.Max(p => p.OriginalValue)}\n";
                        analysis += $"  最小目标值: {rule.PixelDetails.Min(p => p.TargetValue)}, 最大目标值: {rule.PixelDetails.Max(p => p.TargetValue)}\n";
                        analysis += $"  注意: 完整的750万行像素数据可通过导出功能获取\n";
                    }
                }
                else
                {
                    analysis += $"  {param.Key}: {param.Value}\n";
                }
            }

            return analysis;
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
                    analysis += $"  映射数据示例: {string.Join(", ", mapping.Take(5).Select(kvp => $"{kvp.Key}→{kvp.Value}"))}{(mapping.Count > 5 ? "..." : "")}\n";
                    
                    // 如果有详细的像素信息，使用详细分析
                    if (rule.PixelDetails != null && rule.PixelDetails.Count > 0)
                    {
                        analysis += GenerateMappingAnalysis(rule.PixelDetails);
                    }
                    else
                    {
                        analysis += GenerateMappingAnalysis(mapping);
                    }
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
                
                // 检查是否有实际处理数据
                if (rule.Statistics.ProcessedPixelCount == 0)
                {
                    analysis += $"  ⚠️ 警告: 处理像素数为0，可能存在以下问题:\n";
                    analysis += $"    - 映射数据格式不匹配\n";
                    analysis += $"    - 图像位深检测错误\n";
                    analysis += $"    - 处理区域配置错误\n";
                    analysis += $"  处理像素总数: {rule.Statistics.ProcessedPixelCount:N0}\n";
                }
                else
                {
                    analysis += $"  处理像素总数: {rule.Statistics.ProcessedPixelCount:N0}\n";
                    analysis += $"  平均灰度变化: {rule.Statistics.AverageChange:F2}\n";
                    analysis += $"  最大变化量: {rule.Statistics.MaxChange}\n";
                    analysis += $"  最小变化量: {rule.Statistics.MinChange}\n";
                    analysis += $"  变化标准差: {rule.Statistics.ChangeStandardDeviation:F2}\n";
                    analysis += $"  处理前平均灰度: {rule.Statistics.BeforeAverageGray:F1}\n";
                    analysis += $"  处理后平均灰度: {rule.Statistics.AfterAverageGray:F1}\n";
                    
                    double brightnessChange = rule.Statistics.AfterAverageGray - rule.Statistics.BeforeAverageGray;
                    string changeDirection = brightnessChange > 0 ? "提升" : (brightnessChange < 0 ? "降低" : "保持");
                    analysis += $"  整体亮度变化: {brightnessChange:F1} ({changeDirection})\n";
                    
                    // 添加处理效果评估
                    if (Math.Abs(brightnessChange) > 1000)
                        analysis += $"  效果评估: 显著亮度{changeDirection}\n";
                    else if (Math.Abs(brightnessChange) > 100)
                        analysis += $"  效果评估: 中等亮度{changeDirection}\n";
                    else
                        analysis += $"  效果评估: 轻微亮度调整\n";
                }
                
                analysis += $"  处理耗时: {rule.Statistics.ProcessingTimeMs}ms\n";
            }

            return analysis;
        }

        /// <summary>
        /// 生成像素映射关系的详细分析 - 支持详细像素信息（优化版）
        /// </summary>
        private string GenerateMappingAnalysis(List<PixelMappingDetail> pixelDetails)
        {
            if (pixelDetails.Count == 0) return "  映射关系为空\n";

            logger.Info($"开始生成像素映射分析报告 - 像素数: {pixelDetails.Count:N0}");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 使用StringBuilder避免字符串拼接性能问题
            var analysis = new System.Text.StringBuilder();

            // 16位DICOM图像映射关系详细分析
            analysis.AppendLine("  16位DICOM逐像素映射分析:");
            analysis.AppendLine($"    总像素数: {pixelDetails.Count:N0}");
            analysis.AppendLine($"    扫描顺序: 按行列排序 (从左到右，从上到下)");
            analysis.AppendLine();
            
            // 【关键优化】避免生成750万行详细信息，改为统计摘要
            analysis.AppendLine("    像素映射统计 (避免生成750万行详细信息):");
            analysis.AppendLine($"    - 显示前20个像素的详细信息:");
            analysis.AppendLine();
            
            // 只显示前20个像素的详细信息作为示例
            int displayCount = Math.Min(20, pixelDetails.Count);
            for (int i = 0; i < displayCount; i++)
            {
                var pixel = pixelDetails[i];
                string changeStr = pixel.Change >= 0 ? $"+{pixel.Change}" : pixel.Change.ToString();
                analysis.AppendLine($"    [{pixel.SerialNumber:D6}] 位置({pixel.Position.X:D4},{pixel.Position.Y:D4}) 原值{pixel.OriginalValue} → 新值{pixel.TargetValue} (变化: {changeStr}, {pixel.PercentChange:F1}%)");
            }
            
            if (pixelDetails.Count > 20)
            {
                analysis.AppendLine($"    ... (跳过中间 {pixelDetails.Count - 20:N0} 个像素的详细信息)");
                analysis.AppendLine($"    最后5个像素:");
                
                // 显示最后5个像素
                for (int i = Math.Max(20, pixelDetails.Count - 5); i < pixelDetails.Count; i++)
                {
                    var pixel = pixelDetails[i];
                    string changeStr = pixel.Change >= 0 ? $"+{pixel.Change}" : pixel.Change.ToString();
                    analysis.AppendLine($"    [{pixel.SerialNumber:D6}] 位置({pixel.Position.X:D4},{pixel.Position.Y:D4}) 原值{pixel.OriginalValue} → 新值{pixel.TargetValue} (变化: {changeStr}, {pixel.PercentChange:F1}%)");
                }
            }
            
            // 映射统计摘要
            analysis.AppendLine();
            analysis.AppendLine("    映射统计摘要:");
            analysis.AppendLine($"    - 最小原值: {pixelDetails.Min(p => p.OriginalValue)}, 最大原值: {pixelDetails.Max(p => p.OriginalValue)}");
            analysis.AppendLine($"    - 最小目标值: {pixelDetails.Min(p => p.TargetValue)}, 最大目标值: {pixelDetails.Max(p => p.TargetValue)}");
            analysis.AppendLine($"    - 平均变化量: {pixelDetails.Average(p => p.Change):F1}");
            analysis.AppendLine($"    - 变化标准差: {Math.Sqrt(pixelDetails.Average(p => Math.Pow(p.Change - pixelDetails.Average(p => p.Change), 2))):F1}");

            stopwatch.Stop();
            logger.Info($"像素映射分析报告生成完成 - 耗时: {stopwatch.ElapsedMilliseconds}ms");

            return analysis.ToString();
        }

        /// <summary>
        /// 导出完整的750万行像素数据到单独文件（用于AI分析）
        /// </summary>
        /// <param name="pixelDetails">像素详细信息列表</param>
        /// <param name="exportFormat">导出格式：txt=文本文件，csv=CSV格式</param>
        /// <returns>导出的文件路径</returns>
        public string ExportCompletePixelData(List<PixelMappingDetail> pixelDetails, string exportFormat = "txt")
        {
            if (pixelDetails == null || pixelDetails.Count == 0)
                throw new ArgumentException("像素数据不能为空");

            try
            {
                logger.Info($"开始导出完整像素数据 - 像素数: {pixelDetails.Count:N0}, 格式: {exportFormat}");
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = exportFormat.ToLower() == "csv" 
                    ? $"完整像素数据_{timestamp}.csv" 
                    : $"完整像素数据_{timestamp}.txt";
                string filePath = Path.Combine(desktopPath, fileName);

                // 使用StreamWriter流式写入，避免内存爆炸
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    if (exportFormat.ToLower() == "csv")
                    {
                        // CSV格式 - 包含表头
                        writer.WriteLine("序号,位置X,位置Y,原始值,目标值,变化量,变化百分比");
                        
                        foreach (var pixel in pixelDetails)
                        {
                            writer.WriteLine($"{pixel.SerialNumber},{pixel.Position.X},{pixel.Position.Y},{pixel.OriginalValue},{pixel.TargetValue},{pixel.Change},{pixel.PercentChange:F2}");
                        }
                    }
                    else
                    {
                        // 文本格式 - 适合AI阅读
                        writer.WriteLine($"=== 完整16位DICOM像素映射数据 ===");
                        writer.WriteLine($"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        writer.WriteLine($"总像素数: {pixelDetails.Count:N0}");
                        writer.WriteLine($"扫描顺序: 按行列排序 (从左到右，从上到下)");
                        writer.WriteLine();
                        writer.WriteLine($"=== 详细像素映射信息 ===");
                        writer.WriteLine();

                        foreach (var pixel in pixelDetails)
                        {
                            string changeStr = pixel.Change >= 0 ? $"+{pixel.Change}" : pixel.Change.ToString();
                            writer.WriteLine($"[{pixel.SerialNumber:D6}] 位置({pixel.Position.X:D4},{pixel.Position.Y:D4}) 原值{pixel.OriginalValue} → 新值{pixel.TargetValue} (变化: {changeStr}, {pixel.PercentChange:F1}%)");
                        }
                        
                        // 在文件末尾添加统计摘要
                        writer.WriteLine();
                        writer.WriteLine($"=== 统计摘要 ===");
                        writer.WriteLine($"最小原值: {pixelDetails.Min(p => p.OriginalValue)}, 最大原值: {pixelDetails.Max(p => p.OriginalValue)}");
                        writer.WriteLine($"最小目标值: {pixelDetails.Min(p => p.TargetValue)}, 最大目标值: {pixelDetails.Max(p => p.TargetValue)}");
                        writer.WriteLine($"平均变化量: {pixelDetails.Average(p => p.Change):F1}");
                    }
                }

                stopwatch.Stop();
                logger.Info($"完整像素数据导出完成 - 文件: {fileName}, 耗时: {stopwatch.ElapsedMilliseconds}ms, 文件大小: {new FileInfo(filePath).Length / 1024 / 1024:F1}MB");

                return filePath;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "导出完整像素数据失败");
                throw;
            }
        }

        /// <summary>
        /// 生成像素映射关系的详细分析 - 兼容旧的映射字典格式
        /// </summary>
        private string GenerateMappingAnalysis(Dictionary<int, int> mapping)
        {
            if (mapping.Count == 0) return "  映射关系为空\n";

            var analysis = "";

            // 16位DICOM图像映射关系详细分析
            var sortedMapping = mapping.OrderBy(kvp => kvp.Key).ToList();

            analysis += "  16位DICOM逐像素映射分析:\n";
            analysis += $"    总映射点数: {sortedMapping.Count}, 显示完整映射数据\n";
            analysis += $"    映射范围: {sortedMapping.First().Key} - {sortedMapping.Last().Key}\n\n";
            
            // 显示所有映射点，不采样
            foreach (var point in sortedMapping)
            {
                int change = point.Value - point.Key;
                string changeStr = change >= 0 ? $"+{change}" : change.ToString();
                double percentChange = point.Key != 0 ? (change * 100.0 / point.Key) : 0;
                analysis += $"    原值{point.Key} → 新值{point.Value} (变化: {changeStr}, {percentChange:F1}%)\n";
            }
            
            // 映射统计摘要
            analysis += "\n    映射统计摘要:\n";
            analysis += $"    - 最小原值: {sortedMapping.First().Key}, 最大原值: {sortedMapping.Last().Key}\n";
            analysis += $"    - 最小目标值: {sortedMapping.Min(kvp => kvp.Value)}, 最大目标值: {sortedMapping.Max(kvp => kvp.Value)}\n";
            analysis += $"    - 平均变化量: {sortedMapping.Average(kvp => kvp.Value - kvp.Key):F1}\n";

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

            // 添加AI分析建议
            analysis += "\n  AI分析要点:\n";
            analysis += $"    - 映射点数量: {mapping.Count}\n";
            analysis += $"    - 输入值范围: {mapping.Keys.Min()} - {mapping.Keys.Max()}\n";
            analysis += $"    - 输出值范围: {mapping.Values.Min()} - {mapping.Values.Max()}\n";
            
            // 计算整体变化趋势
            var avgChange = mapping.Average(kvp => kvp.Value - kvp.Key);
            analysis += $"    - 平均变化量: {avgChange:F1}\n";
            
            if (Math.Abs(avgChange) > 1000)
                analysis += $"    - 整体趋势: 强烈{(avgChange > 0 ? "提亮" : "压暗")}\n";
            else if (Math.Abs(avgChange) > 100)
                analysis += $"    - 整体趋势: 中等{(avgChange > 0 ? "提亮" : "压暗")}\n";
            else
                analysis += $"    - 整体趋势: 轻微调整\n";

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
        /// 分析映射关系的线性度 - 添加溢出保护
        /// </summary>
        private (double Linearity, double Slope, double Intercept) AnalyzeMappingLinearity(Dictionary<int, int> mapping)
        {
            if (mapping.Count < 2) return (0, 0, 0);

            try
            {
                var points = mapping.OrderBy(kvp => kvp.Key).ToList();

                // 计算线性回归 - 使用安全的方式
                long sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                
                foreach (var point in points)
                {
                    sumX += point.Key;
                    sumY += point.Value;
                    sumXY += (long)point.Key * point.Value;
                    sumX2 += (long)point.Key * point.Key;
                    
                    // 防止累加溢出
                    if (sumX > long.MaxValue / 1000 || sumY > long.MaxValue / 1000)
                        break;
                }

                int n = points.Count;
                double denominator = (double)n * sumX2 - (double)sumX * sumX;
                
                // 防止除零错误
                if (Math.Abs(denominator) < 1e-10)
                    return (0, 0, 0);
                
                double slope = ((double)n * sumXY - (double)sumX * sumY) / denominator;
                double intercept = ((double)sumY - slope * sumX) / n;

                // 计算R²来评估线性度 - 使用安全的方式
                double meanY = (double)sumY / n;
                double ssTotal = 0, ssRes = 0;
                
                foreach (var point in points)
                {
                    double predicted = slope * point.Key + intercept;
                    double residual = point.Value - predicted;
                    double totalDiff = point.Value - meanY;
                    
                    // 使用安全的平方计算
                    ssTotal += totalDiff * totalDiff;
                    ssRes += residual * residual;
                    
                    // 防止溢出
                    if (ssTotal > double.MaxValue / 10 || ssRes > double.MaxValue / 10)
                        break;
                }

                // 防止除零错误
                double rSquared = (ssTotal < 1e-10) ? 1.0 : Math.Max(0, Math.Min(1.0, 1.0 - ssRes / ssTotal));

                return (rSquared, slope, intercept);
            }
            catch
            {
                // 发生异常时返回默认值
                return (0, 0, 0);
            }
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
        /// 计算相关系数 - 添加溢出保护
        /// </summary>
        private double CalculateCorrelation(List<double> x, List<double> y)
        {
            if (x.Count != y.Count || x.Count == 0) return 0;

            try
            {
                double meanX = x.Average();
                double meanY = y.Average();

                double numerator = 0;
                double sumX2 = 0;
                double sumY2 = 0;

                for (int i = 0; i < x.Count; i++)
                {
                    double diffX = x[i] - meanX;
                    double diffY = y[i] - meanY;
                    
                    numerator += diffX * diffY;
                    sumX2 += diffX * diffX;
                    sumY2 += diffY * diffY;
                    
                    // 防止溢出
                    if (Math.Abs(numerator) > double.MaxValue / 10 || 
                        Math.Abs(sumX2) > double.MaxValue / 10 || 
                        Math.Abs(sumY2) > double.MaxValue / 10)
                    {
                        break;
                    }
                }

                double denominator = Math.Sqrt(sumX2 * sumY2);
                return denominator == 0 ? 0 : Math.Max(-1, Math.Min(1, numerator / denominator));
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 计算标准差 - 添加溢出保护
        /// </summary>
        private double CalculateStandardDeviation(List<int> values)
        {
            if (values.Count == 0) return 0;

            try
            {
                double mean = values.Average();
                double sumSquaredDifferences = 0;
                
                foreach (var v in values)
                {
                    double difference = v - mean;
                    // 防止大数平方导致溢出
                    if (Math.Abs(difference) > 1000000)
                    {
                        // 对于极大差异，使用近似值
                        sumSquaredDifferences += Math.Abs(difference) * 1000000;
                    }
                    else
                    {
                        sumSquaredDifferences += difference * difference;
                    }
                    
                    // 防止累加时溢出
                    if (sumSquaredDifferences > double.MaxValue / 10)
                    {
                        sumSquaredDifferences = double.MaxValue / 10;
                        break;
                    }
                }
                
                return Math.Sqrt(sumSquaredDifferences / values.Count);
            }
            catch
            {
                // 发生异常时返回一个合理的默认值
                return 0;
            }
        }
    }
}
