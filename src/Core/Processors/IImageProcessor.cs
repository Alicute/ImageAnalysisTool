using System.Collections.Generic;
using OpenCvSharp;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 图像处理器接口 - 为Phase 2扩展预留
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// 处理器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 处理器描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 支持的处理类型
        /// </summary>
        ProcessingType[] SupportedTypes { get; }

        /// <summary>
        /// 处理图像
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="parameters">处理参数</param>
        /// <returns>处理后的图像</returns>
        Mat ProcessImage(Mat input, Dictionary<string, object> parameters);

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="parameters">参数字典</param>
        /// <returns>验证结果</returns>
        bool ValidateParameters(Dictionary<string, object> parameters);

        /// <summary>
        /// 获取默认参数
        /// </summary>
        /// <returns>默认参数字典</returns>
        Dictionary<string, object> GetDefaultParameters();
    }

    /// <summary>
    /// 区域处理器接口 - Phase 2实现
    /// </summary>
    public interface IRegionProcessor : IImageProcessor
    {
        /// <summary>
        /// 处理指定区域
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="region">处理区域</param>
        /// <param name="parameters">处理参数</param>
        /// <returns>处理后的图像</returns>
        Mat ProcessRegion(Mat input, System.Drawing.Rectangle region, Dictionary<string, object> parameters);
    }

    /// <summary>
    /// 算法调参处理器接口 - Phase 2实现
    /// </summary>
    public interface IAlgorithmTuner : IImageProcessor
    {
        /// <summary>
        /// 支持的算法列表
        /// </summary>
        string[] SupportedAlgorithms { get; }

        /// <summary>
        /// 获取算法参数定义
        /// </summary>
        /// <param name="algorithmName">算法名称</param>
        /// <returns>参数定义</returns>
        Dictionary<string, ParameterDefinition> GetAlgorithmParameters(string algorithmName);

        /// <summary>
        /// 应用算法
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="algorithmName">算法名称</param>
        /// <param name="parameters">算法参数</param>
        /// <returns>处理后的图像</returns>
        Mat ApplyAlgorithm(Mat input, string algorithmName, Dictionary<string, object> parameters);
    }

    /// <summary>
    /// 参数定义
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// 参数名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数类型
        /// </summary>
        public System.Type Type { get; set; }

        /// <summary>
        /// 默认值
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// 最小值（数值类型）
        /// </summary>
        public object MinValue { get; set; }

        /// <summary>
        /// 最大值（数值类型）
        /// </summary>
        public object MaxValue { get; set; }

        /// <summary>
        /// 参数描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 是否必需
        /// </summary>
        public bool IsRequired { get; set; }
    }
}
