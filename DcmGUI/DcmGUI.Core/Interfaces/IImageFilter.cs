using System.Threading.Tasks;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Interfaces
{
    /// <summary>
    /// 图像滤波器接口
    /// </summary>
    public interface IImageFilter
    {
        /// <summary>
        /// 滤波器名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 滤波器描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 异步应用滤波器
        /// </summary>
        /// <param name="input">输入图像</param>
        /// <param name="parameters">滤波器参数</param>
        /// <returns>处理后的图像</returns>
        Task<ImageData> ApplyAsync(ImageData input, object? parameters = null);
    }
}
