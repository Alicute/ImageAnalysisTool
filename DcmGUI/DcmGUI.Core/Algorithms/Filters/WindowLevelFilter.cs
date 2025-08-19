using System;
using System.Threading.Tasks;
using DcmGUI.Core.Interfaces;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Algorithms.Filters
{
    /// <summary>
    /// 窗宽窗位调整参数
    /// </summary>
    public class WindowLevelParameters : FilterParameters
    {
        public double WindowWidth { get; set; } = 4096;
        public double WindowCenter { get; set; } = 2048;

        public override Dictionary<string, object> GetParameters()
        {
            return new Dictionary<string, object>
            {
                { nameof(WindowWidth), WindowWidth },
                { nameof(WindowCenter), WindowCenter }
            };
        }

        public override void SetParameters(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue(nameof(WindowWidth), out var width))
                WindowWidth = (double)width;
            
            if (parameters.TryGetValue(nameof(WindowCenter), out var center))
                WindowCenter = (double)center;
        }
    }

    /// <summary>
    /// 窗宽窗位调整滤波器
    /// </summary>
    public class WindowLevelFilter : IImageFilter
    {
        public string Name => "窗宽窗位";
        public string Description => "调整DICOM图像的窗宽窗位显示";

        public async Task<ImageData> ApplyAsync(ImageData input, object? parameters = null)
        {
            if (input == null || !input.IsValid())
                throw new ArgumentException("输入图像无效");

            var param = parameters as WindowLevelParameters ?? new WindowLevelParameters();
            
            return await Task.Run(() =>
            {
                var result = input.Clone();
                result.WindowWidth = param.WindowWidth;
                result.WindowCenter = param.WindowCenter;
                return result;
            });
        }
    }
}
