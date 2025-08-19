using System.Collections.Generic;

namespace DcmGUI.Core.Models
{
    /// <summary>
    /// 滤波器参数基类
    /// </summary>
    public abstract class FilterParameters
    {
        /// <summary>
        /// 获取参数字典
        /// </summary>
        /// <returns>参数字典</returns>
        public abstract Dictionary<string, object> GetParameters();

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <param name="parameters">参数字典</param>
        public abstract void SetParameters(Dictionary<string, object> parameters);
    }

    /// <summary>
    /// 高斯模糊参数
    /// </summary>
    public class GaussianBlurParameters : FilterParameters
    {
        /// <summary>
        /// 模糊半径
        /// </summary>
        public float Radius { get; set; } = 1.0f;

        /// <summary>
        /// 是否使用可分离滤波器
        /// </summary>
        public bool SeparableFilter { get; set; } = true;

        /// <summary>
        /// 核大小（0表示自动计算）
        /// </summary>
        public int KernelSize { get; set; } = 0;

        public override Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                { nameof(Radius), Radius },
                { nameof(SeparableFilter), SeparableFilter },
                { nameof(KernelSize), KernelSize }
            };
        }

        public override void SetParameters(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue(nameof(Radius), out var radius))
                Radius = (float)radius;
            
            if (parameters.TryGetValue(nameof(SeparableFilter), out var separable))
                SeparableFilter = (bool)separable;
            
            if (parameters.TryGetValue(nameof(KernelSize), out var kernelSize))
                KernelSize = (int)kernelSize;
        }
    }

    /// <summary>
    /// 中值滤波参数
    /// </summary>
    public class MedianFilterParameters : FilterParameters
    {
        /// <summary>
        /// 滤波器大小
        /// </summary>
        public int Size { get; set; } = 3;

        public override Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                { nameof(Size), Size }
            };
        }

        public override void SetParameters(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue(nameof(Size), out var size))
                Size = (int)size;
        }
    }

    /// <summary>
    /// 亮度对比度调整参数
    /// </summary>
    public class BrightnessContrastParameters : FilterParameters
    {
        /// <summary>
        /// 亮度调整值 (-100 到 100)
        /// </summary>
        public float Brightness { get; set; } = 0.0f;

        /// <summary>
        /// 对比度调整值 (-100 到 100)
        /// </summary>
        public float Contrast { get; set; } = 0.0f;

        public override Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                { nameof(Brightness), Brightness },
                { nameof(Contrast), Contrast }
            };
        }

        public override void SetParameters(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue(nameof(Brightness), out var brightness))
                Brightness = (float)brightness;
            
            if (parameters.TryGetValue(nameof(Contrast), out var contrast))
                Contrast = (float)contrast;
        }
    }
}
