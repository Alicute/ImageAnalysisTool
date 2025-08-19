using System.Threading.Tasks;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Interfaces
{
    /// <summary>
    /// 图像管理器接口
    /// </summary>
    public interface IImageManager
    {
        /// <summary>
        /// 异步加载图像
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>图像数据</returns>
        Task<ImageData> LoadImageAsync(string filePath);

        /// <summary>
        /// 异步保存图像
        /// </summary>
        /// <param name="image">图像数据</param>
        /// <param name="filePath">保存路径</param>
        /// <returns>是否成功</returns>
        Task<bool> SaveImageAsync(ImageData image, string filePath);

        /// <summary>
        /// 创建图像备份
        /// </summary>
        /// <param name="image">原始图像</param>
        /// <returns>备份图像</returns>
        ImageData? CreateBackup(ImageData? image);

        /// <summary>
        /// 从备份恢复图像
        /// </summary>
        /// <param name="backup">备份图像</param>
        /// <returns>恢复的图像</returns>
        ImageData? RestoreFromBackup(ImageData? backup);
    }
}
