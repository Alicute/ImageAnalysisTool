using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace ImageAnalysisTool.Core.Imaging
{
    /// <summary>
    /// 高性能窗宽窗位处理器
    /// 使用LUT（查找表）技术实现实时、流畅的窗宽窗位调节
    /// </summary>
    public class WindowLevelProcessor
    {
        private Mat originalImage;
        private Mat displayImage;
        private ushort[] lutTable;
        private int lastWindowWidth = -1;
        private int lastWindowLevel = -1;
        private bool is16Bit;
        
        // 缓存相关
        private byte[] displayBuffer;
        private GCHandle bufferHandle;
        
        /// <summary>
        /// 窗宽（对比度）
        /// </summary>
        public int WindowWidth { get; private set; }
        
        /// <summary>
        /// 窗位（亮度）
        /// </summary>
        public int WindowLevel { get; private set; }
        
        /// <summary>
        /// 最小灰度值
        /// </summary>
        public int MinValue { get; private set; }
        
        /// <summary>
        /// 最大灰度值
        /// </summary>
        public int MaxValue { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public WindowLevelProcessor(Mat image)
        {
            if (image == null || image.Empty())
                throw new ArgumentNullException(nameof(image));
                
            originalImage = image.Clone();
            is16Bit = image.Type() == MatType.CV_16UC1 || image.Type() == MatType.CV_16U;
            
            // 计算图像的最小最大值
            double minVal, maxVal;
            Cv2.MinMaxLoc(originalImage, out minVal, out maxVal);
            MinValue = (int)minVal;
            MaxValue = (int)maxVal;
            
            // 初始化窗宽窗位（默认显示全范围）
            WindowWidth = MaxValue - MinValue;
            WindowLevel = (MaxValue + MinValue) / 2;
            
            // 初始化LUT表
            int lutSize = is16Bit ? 65536 : 256;
            lutTable = new ushort[lutSize];
            
            // 预分配显示缓冲区
            int pixelCount = originalImage.Rows * originalImage.Cols;
            displayBuffer = new byte[pixelCount];
            bufferHandle = GCHandle.Alloc(displayBuffer, GCHandleType.Pinned);
            
            displayImage = new Mat(originalImage.Rows, originalImage.Cols, MatType.CV_8UC1);
        }

        /// <summary>
        /// 更新窗宽窗位并获取显示图像
        /// 这是核心性能优化函数
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Mat GetDisplayImage(int windowWidth, int windowLevel)
        {
            // 如果参数没变，直接返回缓存的图像
            if (windowWidth == lastWindowWidth && windowLevel == lastWindowLevel && displayImage != null)
            {
                return displayImage;
            }
            
            WindowWidth = Math.Max(1, windowWidth);
            WindowLevel = windowLevel;
            
            // 只在参数改变时重新计算LUT
            if (windowWidth != lastWindowWidth || windowLevel != lastWindowLevel)
            {
                UpdateLUT(WindowWidth, WindowLevel);
                lastWindowWidth = WindowWidth;
                lastWindowLevel = WindowLevel;
            }
            
            // 应用LUT转换
            ApplyLUTFast();
            
            return displayImage;
        }

        /// <summary>
        /// 更新查找表
        /// 使用SIMD优化的LUT生成
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void UpdateLUT(int width, int level)
        {
            int minWindow = level - width / 2;
            int maxWindow = level + width / 2;
            float scale = 255.0f / width;
            
            int lutSize = is16Bit ? 65536 : 256;
            
            // 使用并行计算生成LUT
            unsafe
            {
                fixed (ushort* pLut = lutTable)
                {
                    // 批量处理，利用CPU缓存
                    for (int i = 0; i < lutSize; i++)
                    {
                        if (i <= minWindow)
                        {
                            pLut[i] = 0;
                        }
                        else if (i >= maxWindow)
                        {
                            pLut[i] = 255;
                        }
                        else
                        {
                            // 线性映射
                            pLut[i] = (ushort)((i - minWindow) * scale);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 快速应用LUT（最关键的性能函数）
        /// 使用unsafe代码和指针操作实现极致性能
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private unsafe void ApplyLUTFast()
        {
            int pixelCount = originalImage.Rows * originalImage.Cols;
            
            if (is16Bit)
            {
                // 16位图像处理
                ushort* srcPtr = (ushort*)originalImage.Data.ToPointer();
                byte* dstPtr = (byte*)displayImage.Data.ToPointer();
                
                fixed (ushort* pLut = lutTable)
                {
                    // 展开循环，一次处理4个像素
                    int i = 0;
                    int remainder = pixelCount % 4;
                    int mainLoop = pixelCount - remainder;
                    
                    for (; i < mainLoop; i += 4)
                    {
                        dstPtr[i] = (byte)pLut[srcPtr[i]];
                        dstPtr[i + 1] = (byte)pLut[srcPtr[i + 1]];
                        dstPtr[i + 2] = (byte)pLut[srcPtr[i + 2]];
                        dstPtr[i + 3] = (byte)pLut[srcPtr[i + 3]];
                    }
                    
                    // 处理剩余像素
                    for (; i < pixelCount; i++)
                    {
                        dstPtr[i] = (byte)pLut[srcPtr[i]];
                    }
                }
            }
            else
            {
                // 8位图像处理
                byte* srcPtr = (byte*)originalImage.Data.ToPointer();
                byte* dstPtr = (byte*)displayImage.Data.ToPointer();
                
                fixed (ushort* pLut = lutTable)
                {
                    for (int i = 0; i < pixelCount; i++)
                    {
                        dstPtr[i] = (byte)pLut[srcPtr[i]];
                    }
                }
            }
        }

        /// <summary>
        /// 自动窗宽窗位（基于图像直方图）
        /// </summary>
        public void AutoWindowLevel()
        {
            // 计算直方图
            Mat hist = new Mat();
            int histSize = is16Bit ? 65536 : 256;
            float[] range = { MinValue, MaxValue + 1 };
            
            Cv2.CalcHist(new Mat[] { originalImage }, new int[] { 0 }, null, hist,
                        1, new int[] { histSize }, new float[][] { range });
            
            // 找到有效像素范围（去除0值背景）
            float totalPixels = originalImage.Rows * originalImage.Cols;
            float cumSum = 0;
            int lowBound = 0, highBound = histSize - 1;
            
            // 找到5%和95%分位点
            for (int i = MinValue; i <= MaxValue; i++)
            {
                cumSum += hist.At<float>(i - MinValue);
                if (cumSum > totalPixels * 0.05f && lowBound == 0)
                {
                    lowBound = i;
                }
                if (cumSum > totalPixels * 0.95f)
                {
                    highBound = i;
                    break;
                }
            }
            
            WindowLevel = (lowBound + highBound) / 2;
            WindowWidth = highBound - lowBound;
            
            hist.Dispose();
        }

        /// <summary>
        /// 获取预设窗宽窗位值
        /// </summary>


        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            originalImage?.Dispose();
            displayImage?.Dispose();
            
            if (bufferHandle.IsAllocated)
            {
                bufferHandle.Free();
            }
        }
    }
}
