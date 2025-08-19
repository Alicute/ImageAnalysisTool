using System;
using System.Threading.Tasks;
using DcmGUI.Core.Interfaces;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Algorithms.Filters
{
    /// <summary>
    /// 高斯模糊滤波器 - 支持16位DICOM图像
    /// </summary>
    public class GaussianBlurFilter : IImageFilter
    {
        public string Name => "高斯模糊";
        public string Description => "使用高斯核进行图像模糊处理，支持16位DICOM图像";

        public async Task<ImageData> ApplyAsync(ImageData input, object? parameters = null)
        {
            if (input == null || !input.IsValid())
                throw new ArgumentException("输入图像无效");

            var param = parameters as GaussianBlurParameters ?? new GaussianBlurParameters();
            
            return await Task.Run(() =>
            {
                if (input.BitsPerPixel == 16 && input.PixelData16 != null)
                {
                    return ApplyGaussianBlur16(input, param.Radius);
                }
                else if (input.PixelData8 != null)
                {
                    return ApplyGaussianBlur8(input, param.Radius);
                }
                else
                {
                    throw new InvalidOperationException("不支持的图像格式");
                }
            });
        }

        /// <summary>
        /// 16位高斯模糊处理
        /// </summary>
        private ImageData ApplyGaussianBlur16(ImageData input, float radius)
        {
            var kernel = GenerateGaussianKernel1D(radius);
            var result = input.Clone();
            int kernelSize = kernel.Length;
            int offset = kernelSize / 2;

            var srcData = input.PixelData16!;
            var tempData = new ushort[input.Width * input.Height];
            var dstData = result.PixelData16!;

            // 水平方向滤波
            Parallel.For(0, input.Height, y =>
            {
                for (int x = 0; x < input.Width; x++)
                {
                    float sum = 0;
                    
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int px = x + k - offset;
                        
                        // 边界处理：镜像扩展
                        if (px < 0) px = -px;
                        if (px >= input.Width) px = 2 * input.Width - px - 2;
                        
                        int srcIndex = y * input.Width + px;
                        sum += srcData[srcIndex] * kernel[k];
                    }
                    
                    int dstIndex = y * input.Width + x;
                    tempData[dstIndex] = (ushort)Math.Clamp(sum, 0, 65535);
                }
            });

            // 垂直方向滤波
            Parallel.For(0, input.Width, x =>
            {
                for (int y = 0; y < input.Height; y++)
                {
                    float sum = 0;
                    
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int py = y + k - offset;
                        
                        // 边界处理：镜像扩展
                        if (py < 0) py = -py;
                        if (py >= input.Height) py = 2 * input.Height - py - 2;
                        
                        int srcIndex = py * input.Width + x;
                        sum += tempData[srcIndex] * kernel[k];
                    }
                    
                    int dstIndex = y * input.Width + x;
                    dstData[dstIndex] = (ushort)Math.Clamp(sum, 0, 65535);
                }
            });

            return result;
        }

        /// <summary>
        /// 8位高斯模糊处理
        /// </summary>
        private ImageData ApplyGaussianBlur8(ImageData input, float radius)
        {
            var kernel = GenerateGaussianKernel1D(radius);
            var result = input.Clone();
            int kernelSize = kernel.Length;
            int offset = kernelSize / 2;

            var srcData = input.PixelData8!;
            var tempData = new byte[input.Width * input.Height];
            var dstData = result.PixelData8!;

            // 水平方向滤波
            Parallel.For(0, input.Height, y =>
            {
                for (int x = 0; x < input.Width; x++)
                {
                    float sum = 0;
                    
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int px = x + k - offset;
                        
                        // 边界处理：镜像扩展
                        if (px < 0) px = -px;
                        if (px >= input.Width) px = 2 * input.Width - px - 2;
                        
                        int srcIndex = y * input.Width + px;
                        sum += srcData[srcIndex] * kernel[k];
                    }
                    
                    int dstIndex = y * input.Width + x;
                    tempData[dstIndex] = (byte)Math.Clamp(sum, 0, 255);
                }
            });

            // 垂直方向滤波
            Parallel.For(0, input.Width, x =>
            {
                for (int y = 0; y < input.Height; y++)
                {
                    float sum = 0;
                    
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int py = y + k - offset;
                        
                        // 边界处理：镜像扩展
                        if (py < 0) py = -py;
                        if (py >= input.Height) py = 2 * input.Height - py - 2;
                        
                        int srcIndex = py * input.Width + x;
                        sum += tempData[srcIndex] * kernel[k];
                    }
                    
                    int dstIndex = y * input.Width + x;
                    dstData[dstIndex] = (byte)Math.Clamp(sum, 0, 255);
                }
            });

            return result;
        }

        /// <summary>
        /// 生成1D高斯核
        /// </summary>
        private float[] GenerateGaussianKernel1D(float radius)
        {
            float sigma = radius / 3.0f;
            int size = (int)(6 * sigma) | 1;
            if (size < 3) size = 3;
            if (size > 101) size = 101;
            
            var kernel = new float[size];
            int center = size / 2;
            float sum = 0;

            for (int i = 0; i < size; i++)
            {
                int x = i - center;
                kernel[i] = (float)Math.Exp(-(x * x) / (2 * sigma * sigma));
                sum += kernel[i];
            }

            for (int i = 0; i < size; i++)
            {
                kernel[i] /= sum;
            }

            return kernel;
        }
    }
}
