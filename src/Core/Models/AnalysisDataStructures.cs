using System;

namespace ImageAnalysisTool.Core.Models
{
    /// <summary>
    /// 图像质量评估指标
    /// </summary>
    public struct ImageQualityMetrics
    {
        /// <summary>
        /// 结构相似性指数 (0-1, 越接近1越好)
        /// </summary>
        public double SSIM { get; set; }

        /// <summary>
        /// 峰值信噪比 (dB, 通常>20dB为可接受)
        /// </summary>
        public double PSNR { get; set; }

        /// <summary>
        /// 视觉信息保真度 (0-1, 越接近1越好)
        /// </summary>
        public double VIF { get; set; }

        /// <summary>
        /// 过度增强评分 (0-100%, 越低越好)
        /// </summary>
        public double OverEnhancementScore { get; set; }

        /// <summary>
        /// 噪声放大程度 (倍数, 1.0为无放大)
        /// </summary>
        public double NoiseAmplification { get; set; }

        /// <summary>
        /// 边缘质量评分 (0-100, 越高越好)
        /// </summary>
        public double EdgeQuality { get; set; }

        /// <summary>
        /// 光晕效应检测 (0-100%, 越低越好)
        /// </summary>
        public double HaloEffect { get; set; }
    }

    /// <summary>
    /// 医学影像专用评估指标
    /// </summary>
    public struct MedicalImageMetrics
    {
        /// <summary>
        /// 信息保持度 (0-100, 越高越好) - 评估增强后诊断信息的保持程度
        /// </summary>
        public double InformationPreservation { get; set; }

        /// <summary>
        /// 动态范围利用率 (0-100, 越高越好) - 评估灰度范围的充分利用程度
        /// </summary>
        public double DynamicRangeUtilization { get; set; }

        /// <summary>
        /// 局部对比度增强效果 (0-100, 越高越好) - 评估局部区域对比度提升
        /// </summary>
        public double LocalContrastEnhancement { get; set; }

        /// <summary>
        /// 细节保真度 (0-100, 越高越好) - 评估细微结构的保持程度
        /// </summary>
        public double DetailFidelity { get; set; }

        /// <summary>
        /// 窗宽窗位适应性 (0-100, 越高越好) - 评估在不同窗宽窗位下的表现稳定性
        /// </summary>
        public double WindowLevelAdaptability { get; set; }

        /// <summary>
        /// 医学影像质量综合评分 (0-100, 越高越好)
        /// </summary>
        public double OverallMedicalQuality { get; set; }
    }

    /// <summary>
    /// 缺陷检测友好度评估指标
    /// </summary>
    public struct DefectDetectionMetrics
    {
        /// <summary>
        /// 细线缺陷可见性提升 (百分比, 正值表示提升)
        /// </summary>
        public double ThinLineVisibility { get; set; }

        /// <summary>
        /// 背景噪声抑制效果 (0-100%, 越高越好)
        /// </summary>
        public double BackgroundNoiseReduction { get; set; }

        /// <summary>
        /// 缺陷与背景对比度提升 (倍数, >1.0表示提升)
        /// </summary>
        public double DefectBackgroundContrast { get; set; }

        /// <summary>
        /// 假阳性风险评估 (0-100%, 越低越好)
        /// </summary>
        public double FalsePositiveRisk { get; set; }

        /// <summary>
        /// 缺陷检测适用性综合评分 (0-100, 越高越好)
        /// </summary>
        public double OverallSuitability { get; set; }
    }

    /// <summary>
    /// 问题诊断结果
    /// </summary>
    public struct DiagnosticResult
    {
        /// <summary>
        /// 问题类型
        /// </summary>
        public string ProblemType { get; set; }

        /// <summary>
        /// 问题描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 严重程度 (1-5, 5最严重)
        /// </summary>
        public int Severity { get; set; }

        /// <summary>
        /// 建议解决方案
        /// </summary>
        public string Suggestion { get; set; }
    }

    /// <summary>
    /// 参数优化建议
    /// </summary>
    public struct ParameterOptimizationSuggestion
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 当前值
        /// </summary>
        public double CurrentValue { get; set; }

        /// <summary>
        /// 建议值
        /// </summary>
        public double SuggestedValue { get; set; }

        /// <summary>
        /// 调整原因
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// 预期改善效果
        /// </summary>
        public string ExpectedImprovement { get; set; }
    }

    /// <summary>
    /// 综合分析结果
    /// </summary>
    public struct ComprehensiveAnalysisResult
    {
        /// <summary>
        /// ROI区域图像质量指标
        /// </summary>
        public ImageQualityMetrics ROIQualityMetrics { get; set; }

        /// <summary>
        /// ROI区域医学影像专用指标
        /// </summary>
        public MedicalImageMetrics ROIMedicalMetrics { get; set; }

        /// <summary>
        /// ROI区域缺陷检测友好度指标
        /// </summary>
        public DefectDetectionMetrics ROIDetectionMetrics { get; set; }

        /// <summary>
        /// 全图图像质量指标（用于对比）
        /// </summary>
        public ImageQualityMetrics FullImageQualityMetrics { get; set; }

        /// <summary>
        /// 全图医学影像专用指标（用于对比）
        /// </summary>
        public MedicalImageMetrics FullImageMedicalMetrics { get; set; }

        /// <summary>
        /// 全图缺陷检测友好度指标（用于对比）
        /// </summary>
        public DefectDetectionMetrics FullImageDetectionMetrics { get; set; }

        /// <summary>
        /// 问题诊断列表
        /// </summary>
        public DiagnosticResult[] DiagnosticResults { get; set; }

        /// <summary>
        /// 参数优化建议列表
        /// </summary>
        public ParameterOptimizationSuggestion[] OptimizationSuggestions { get; set; }

        /// <summary>
        /// ROI技术指标综合评分 (0-100)
        /// </summary>
        public double ROITechnicalScore { get; set; }

        /// <summary>
        /// ROI医学影像质量综合评分 (0-100)
        /// </summary>
        public double ROIMedicalScore { get; set; }

        /// <summary>
        /// ROI缺陷检测适用性评分 (0-100)
        /// </summary>
        public double ROIDetectionScore { get; set; }

        /// <summary>
        /// ROI综合推荐度评分 (0-100)
        /// </summary>
        public double ROIOverallRecommendation { get; set; }

        /// <summary>
        /// 全图技术指标综合评分 (0-100)
        /// </summary>
        public double FullImageTechnicalScore { get; set; }

        /// <summary>
        /// 全图医学影像质量综合评分 (0-100)
        /// </summary>
        public double FullImageMedicalScore { get; set; }

        /// <summary>
        /// 全图缺陷检测适用性评分 (0-100)
        /// </summary>
        public double FullImageDetectionScore { get; set; }

        /// <summary>
        /// 全图综合推荐度评分 (0-100)
        /// </summary>
        public double FullImageOverallRecommendation { get; set; }
    }

    /// <summary>
    /// 分析配置参数
    /// </summary>
    public struct AnalysisConfiguration
    {
        /// <summary>
        /// 是否启用SSIM计算 (计算量较大)
        /// </summary>
        public bool EnableSSIM { get; set; }

        /// <summary>
        /// 是否启用VIF计算 (计算量很大)
        /// </summary>
        public bool EnableVIF { get; set; }

        /// <summary>
        /// 是否启用详细的边缘质量分析
        /// </summary>
        public bool EnableDetailedEdgeAnalysis { get; set; }

        /// <summary>
        /// 是否启用噪声分析
        /// </summary>
        public bool EnableNoiseAnalysis { get; set; }

        /// <summary>
        /// 分析精度级别 (1-3, 3最高精度但最慢)
        /// </summary>
        public int AccuracyLevel { get; set; }

        /// <summary>
        /// 默认配置
        /// </summary>
        public static AnalysisConfiguration Default => new AnalysisConfiguration
        {
            EnableSSIM = true,
            EnableVIF = false,  // 默认关闭，计算量太大
            EnableDetailedEdgeAnalysis = true,
            EnableNoiseAnalysis = true,
            AccuracyLevel = 2   // 平衡精度和速度
        };
    }

    /// <summary>
    /// 双图像对比分析结果
    /// </summary>
    public struct ComparisonAnalysisResult
    {
        /// <summary>
        /// 增强图1的分析结果
        /// </summary>
        public ComprehensiveAnalysisResult Enhanced1Result { get; set; }

        /// <summary>
        /// 增强图2的分析结果
        /// </summary>
        public ComprehensiveAnalysisResult Enhanced2Result { get; set; }

        /// <summary>
        /// 对比总结
        /// </summary>
        public ComparisonSummary Summary { get; set; }
    }

    /// <summary>
    /// 对比总结
    /// </summary>
    public struct ComparisonSummary
    {
        /// <summary>
        /// 推荐的增强图（1或2）
        /// </summary>
        public int RecommendedImage { get; set; }

        /// <summary>
        /// 推荐理由
        /// </summary>
        public string RecommendationReason { get; set; }

        /// <summary>
        /// 图像质量对比差异
        /// </summary>
        public double QualityDifference { get; set; }

        /// <summary>
        /// 医学影像指标对比差异
        /// </summary>
        public double MedicalDifference { get; set; }

        /// <summary>
        /// 缺陷检测适用性对比差异
        /// </summary>
        public double DetectionDifference { get; set; }

        /// <summary>
        /// 综合优势评分差异
        /// </summary>
        public double OverallDifference { get; set; }
    }
}
