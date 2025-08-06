using System;
using System.Collections.Generic;
using System.Linq;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 处理类型枚举
    /// </summary>
    public enum ProcessingType
    {
        /// <summary>
        /// 直接映射 (原图值→目标图值)
        /// </summary>
        DirectMapping,
        
        /// <summary>
        /// 数学运算 (加减乘除)
        /// </summary>
        MathOperation,
        
        /// <summary>
        /// 查找表映射
        /// </summary>
        LookupTable,
        
        /// <summary>
        /// 区域处理
        /// </summary>
        RegionProcessing,
        
        /// <summary>
        /// 算法调参
        /// </summary>
        AlgorithmTuning
    }

    /// <summary>
    /// 数学运算类型
    /// </summary>
    public enum MathOperationType
    {
        Add,        // 加法
        Subtract,   // 减法
        Multiply,   // 乘法
        Divide,     // 除法
        Power,      // 幂运算
        Log,        // 对数
        Gamma       // Gamma校正
    }

    /// <summary>
    /// 处理规则类 - 记录每个像素处理操作的详细信息
    /// </summary>
    public class ProcessingRule
    {
        /// <summary>
        /// 规则唯一标识
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 规则名称
        /// </summary>
        public string RuleName { get; set; }

        /// <summary>
        /// 处理类型
        /// </summary>
        public ProcessingType Type { get; set; }

        /// <summary>
        /// 变换函数 - 用于实际的像素值转换
        /// </summary>
        public Func<ushort, ushort> Transform { get; set; }

        /// <summary>
        /// 参数字典 - 记录所有相关参数
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 规则描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 应用区域 (可选，null表示全图)
        /// </summary>
        public System.Drawing.Rectangle? ApplyRegion { get; set; }

        /// <summary>
        /// 处理前后的统计信息
        /// </summary>
        public ProcessingStatistics Statistics { get; set; }

        /// <summary>
        /// 像素详细信息列表 (可选，用于详细报告)
        /// </summary>
        public List<PixelMappingDetail> PixelDetails { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ProcessingRule()
        {
            Id = Guid.NewGuid().ToString();
            Parameters = new Dictionary<string, object>();
            CreateTime = DateTime.Now;
            Statistics = new ProcessingStatistics();
            PixelDetails = new List<PixelMappingDetail>();
        }

        /// <summary>
        /// 创建直接映射规则
        /// </summary>
        public static ProcessingRule CreateDirectMapping(string name, Dictionary<int, int> mapping)
        {
            var rule = new ProcessingRule
            {
                RuleName = name,
                Type = ProcessingType.DirectMapping,
                Description = "基于像素值直接映射的处理规则"
            };

            rule.Parameters["Mapping"] = mapping;
            
            // 创建变换函数 - 优化处理16位图像的分组映射
            rule.Transform = (originalValue) =>
            {
                int searchKey = originalValue;
                
                // 对于16位图像，尝试找到对应的分组
                if (!mapping.ContainsKey(searchKey))
                {
                    // 检查是否是16位图像的分组映射（分组大小通常是64）
                    int binSize = 64;
                    int binnedKey = (searchKey / binSize) * binSize;
                    
                    if (mapping.ContainsKey(binnedKey))
                    {
                        return (ushort)mapping[binnedKey];
                    }
                }
                
                // 如果有直接映射，使用直接映射
                if (mapping.ContainsKey(searchKey))
                    return (ushort)mapping[searchKey];
                
                // 如果没有直接映射，使用最近邻插值
                var nearestKey = FindNearestKey(mapping, searchKey);
                return (ushort)mapping[nearestKey];
            };

            return rule;
        }

        /// <summary>
        /// 创建数学运算规则
        /// </summary>
        public static ProcessingRule CreateMathOperation(string name, MathOperationType operation, double value)
        {
            var rule = new ProcessingRule
            {
                RuleName = name,
                Type = ProcessingType.MathOperation,
                Description = $"数学运算: {operation} {value}"
            };

            rule.Parameters["Operation"] = operation;
            rule.Parameters["Value"] = value;

            // 创建变换函数 - 添加溢出保护
            rule.Transform = (originalValue) =>
            {
                try
                {
                    double result = originalValue;
                    
                    switch (operation)
                    {
                        case MathOperationType.Add:
                            result = originalValue + value;
                            break;
                        case MathOperationType.Subtract:
                            result = originalValue - value;
                            break;
                        case MathOperationType.Multiply:
                            result = originalValue * value;
                            break;
                        case MathOperationType.Divide:
                            result = value != 0 ? originalValue / value : originalValue;
                            break;
                        case MathOperationType.Power:
                            // 防止幂运算溢出 - 限制base和exponent的范围
                            if (originalValue > 1000 && value > 3)
                                result = Math.Pow(1000, 3); // 限制最大值
                            else if (originalValue > 100 && value > 5)
                                result = Math.Pow(100, 5); // 限制最大值
                            else
                                result = Math.Pow(originalValue, value);
                            break;
                        case MathOperationType.Log:
                            result = Math.Log(originalValue + 1) * value; // +1避免log(0)
                            break;
                        case MathOperationType.Gamma:
                            // 归一化处理避免溢出
                            double normalized = originalValue / 65535.0;
                            result = Math.Pow(Math.Max(0.001, Math.Min(1.0, normalized)), value) * 65535.0;
                            break;
                    }

                    // 确保结果在有效范围内，防止溢出
                    if (double.IsInfinity(result) || double.IsNaN(result))
                        return originalValue; // 发生溢出时返回原值
                    
                    result = Math.Max(0, Math.Min(65535, result));
                    return (ushort)Math.Round(result);
                }
                catch
                {
                    // 发生任何异常时返回原值
                    return originalValue;
                }
            };

            return rule;
        }

        /// <summary>
        /// 查找最近的键值
        /// </summary>
        private static int FindNearestKey(Dictionary<int, int> mapping, int target)
        {
            int nearestKey = mapping.Keys.First();
            int minDistance = Math.Abs(target - nearestKey);

            foreach (var key in mapping.Keys)
            {
                int distance = Math.Abs(target - key);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestKey = key;
                }
            }

            return nearestKey;
        }

        /// <summary>
        /// 获取规则的详细信息字符串
        /// </summary>
        public string GetDetailedInfo()
        {
            var info = $"规则: {RuleName}\n";
            info += $"类型: {Type}\n";
            info += $"描述: {Description}\n";
            info += $"创建时间: {CreateTime:yyyy-MM-dd HH:mm:ss}\n";
            
            if (ApplyRegion.HasValue)
            {
                var region = ApplyRegion.Value;
                info += $"应用区域: ({region.X}, {region.Y}) - ({region.Right}, {region.Bottom})\n";
            }
            else
            {
                info += "应用区域: 全图\n";
            }

            info += "参数:\n";
            foreach (var param in Parameters)
            {
                info += $"  {param.Key}: {param.Value}\n";
            }

            if (Statistics != null)
            {
                info += Statistics.GetSummary();
            }

            return info;
        }
    }

    /// <summary>
    /// 处理统计信息
    /// </summary>
    public class ProcessingStatistics
    {
        /// <summary>
        /// 处理的像素数量
        /// </summary>
        public int ProcessedPixelCount { get; set; }

        /// <summary>
        /// 平均变化量
        /// </summary>
        public double AverageChange { get; set; }

        /// <summary>
        /// 最大变化量
        /// </summary>
        public int MaxChange { get; set; }

        /// <summary>
        /// 最小变化量
        /// </summary>
        public int MinChange { get; set; }

        /// <summary>
        /// 变化量标准差
        /// </summary>
        public double ChangeStandardDeviation { get; set; }

        /// <summary>
        /// 处理前平均灰度值
        /// </summary>
        public double BeforeAverageGray { get; set; }

        /// <summary>
        /// 处理后平均灰度值
        /// </summary>
        public double AfterAverageGray { get; set; }

        /// <summary>
        /// 处理耗时(毫秒)
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// 获取统计摘要
        /// </summary>
        public string GetSummary()
        {
            var summary = "\n统计信息:\n";
            summary += $"  处理像素数: {ProcessedPixelCount:N0}\n";
            summary += $"  平均变化量: {AverageChange:F2}\n";
            summary += $"  变化范围: {MinChange} ~ {MaxChange}\n";
            summary += $"  变化标准差: {ChangeStandardDeviation:F2}\n";
            summary += $"  处理前平均灰度: {BeforeAverageGray:F1}\n";
            summary += $"  处理后平均灰度: {AfterAverageGray:F1}\n";
            summary += $"  处理耗时: {ProcessingTimeMs}ms\n";
            
            return summary;
        }
    }
}
