using System;
using System.Threading.Tasks;
using DcmGUI.Core.Interfaces;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Algorithms.Filters
{
    /// <summary>
    /// 亮度对比度调整滤波器
    /// </summary>
    public class BrightnessContrastFilter : IImageFilter
    {
        public string Name => "亮度对比度";
        public string Description => "调整图像的亮度和对比度";

        public async Task<ImageData> ApplyAsync(ImageData input, object? parameters = null)
        {
            if (input == null || !input.IsValid())
                throw new ArgumentException("输入图像无效");

            var param = parameters as BrightnessContrastParameters ?? new BrightnessContrastParameters();
            
            return await Task.Run(() =>
            {
                if (input.BitsPerPixel == 16 && input.PixelData16 != null)
                {
                    return ApplyBrightnessContrast16(input, param.Brightness, param.Contrast);
                }
                else if (input.PixelData8 != null)
                {
                    return ApplyBrightnessContrast8(input, param.Brightness, param.Contrast);
                }
                else
                {
                    throw new InvalidOperationException("不支持的图像格式");
                }
            });
        }

        /// <summary>
        /// 16位亮度对比度调整
        /// </summary>
        private ImageData ApplyBrightnessContrast16(ImageData input, float brightness, float contrast)
        {
            var result = input.Clone();
            var srcData = input.PixelData16!;
            var dstData = result.PixelData16!;

            // 计算调整参数
            float contrastFactor = (100.0f + contrast) / 100.0f;
            float brightnessFactor = brightness * 655.35f; // 对于16位图像

            Parallel.For(0, srcData.Length, i =>
            {
                float pixel = srcData[i];
                
                // 应用对比度调整
                pixel = (pixel - 32768) * contrastFactor + 32768;
                
                // 应用亮度调整
                pixel += brightnessFactor;
                
                // 限制范围
                dstData[i] = (ushort)Math.Clamp(pixel, 0, 65535);
            });

            return result;
        }

        /// <summary>
        /// 8位亮度对比度调整
        /// </summary>
        private ImageData ApplyBrightnessContrast8(ImageData input, float brightness, float contrast)
        {
            var result = input.Clone();
            var srcData = input.PixelData8!;
            var dstData = result.PixelData8!;

            // 计算调整参数
            float contrastFactor = (100.0f + contrast) / 100.0f;
            float brightnessFactor = brightness * 2.55f; // 对于8位图像

            Parallel.For(0, srcData.Length, i =>
            {
                float pixel = srcData[i];
                
                // 应用对比度调整
                pixel = (pixel - 128) * contrastFactor + 128;
                
                // 应用亮度调整
                pixel += brightnessFactor;
                
                // 限制范围
                dstData[i] = (byte)Math.Clamp(pixel, 0, 255);
            });

            return result;
        }
    }
}
