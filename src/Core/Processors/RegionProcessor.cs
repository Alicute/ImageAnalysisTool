using System;
using OpenCvSharp;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 提供基于区域的图像处理算法
    /// </summary>
    public static class RegionProcessor
    {
        /// <summary>
        /// 应用伽马校正
        /// </summary>
        /// <param name="source">源图像 (应为 CV_16UC1)</param>
        /// <param name="gamma">Gamma值 (通常在0.1到5.0之间)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyGammaCorrection(Mat source, double gamma)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (gamma <= 0)
                throw new ArgumentException("Gamma值必须为正数", nameof(gamma));

            // 对于16位图像，我们直接计算每个像素值而不是使用LUT
            Mat result = new Mat(source.Size(), MatType.CV_16UC1);
            
            // 遍历每个像素并应用伽马校正
            for (int y = 0; y < source.Rows; y++)
            {
                for (int x = 0; x < source.Cols; x++)
                {
                    ushort pixelValue = source.At<ushort>(y, x);
                    ushort correctedValue = (ushort)Math.Min(65535, Math.Pow(pixelValue / 65535.0, gamma) * 65535.0);
                    result.Set(y, x, correctedValue);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 应用对数变换 c * log(1 + r)
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyLogTransform(Mat source)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            // c = 65535 / log(1 + 65535)
            double c = 65535.0 / Math.Log(1 + 65535.0);

            // 对于16位图像，我们直接计算每个像素值而不是使用LUT
            Mat result = new Mat(source.Size(), MatType.CV_16UC1);
            
            // 遍历每个像素并应用对数变换
            for (int y = 0; y < source.Rows; y++)
            {
                for (int x = 0; x < source.Cols; x++)
                {
                    ushort pixelValue = source.At<ushort>(y, x);
                    ushort transformedValue = (ushort)Math.Min(65535, c * Math.Log(1 + pixelValue));
                    result.Set(y, x, transformedValue);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 应用对比度拉伸
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="low">输入灰度范围的下限</param>
        /// <param name="high">输入灰度范围的上限</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyContrastStretching(Mat source, int low, int high)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");
            
            if (low >= high)
                throw new ArgumentException("下限值必须小于上限值");

            // 对于16位图像，我们直接计算每个像素值而不是使用LUT
            Mat result = new Mat(source.Size(), MatType.CV_16UC1);
            
            double range = high - low;
            
            // 遍历每个像素并应用对比度拉伸
            for (int y = 0; y < source.Rows; y++)
            {
                for (int x = 0; x < source.Cols; x++)
                {
                    ushort pixelValue = source.At<ushort>(y, x);
                    ushort stretchedValue;
                    
                    if (pixelValue < low)
                    {
                        stretchedValue = 0;
                    }
                    else if (pixelValue > high)
                    {
                        stretchedValue = 65535;
                    }
                    else
                    {
                        stretchedValue = (ushort)Math.Min(65535, ((pixelValue - low) / range) * 65535.0);
                    }
                    
                    result.Set(y, x, stretchedValue);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 应用全局直方图均衡化
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyHistogramEqualization(Mat source)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            // 1. 转换为8位进行均衡化
            Mat source8u = new Mat();
            source.ConvertTo(source8u, MatType.CV_8UC1, 255.0 / 65535.0);

            // 2. 应用均衡化
            Mat equalized8u = new Mat();
            Cv2.EqualizeHist(source8u, equalized8u);

            // 3. 转换回16位
            Mat result = new Mat();
            equalized8u.ConvertTo(result, MatType.CV_16UC1, 65535.0 / 255.0);

            source8u.Dispose();
            equalized8u.Dispose();

            return result;
        }

        /// <summary>
        /// 应用限制对比度的自适应直方图均衡化 (CLAHE)
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="clipLimit">对比度限制阈值</param>
        /// <param name="tileGridSize">网格大小</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyCLAHE(Mat source, double clipLimit = 2.0, int tileGridSize = 8)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            // 1. 转换为8位进行处理
            Mat source8u = new Mat();
            source.ConvertTo(source8u, MatType.CV_8UC1, 255.0 / 65535.0);

            // 2. 创建并应用CLAHE
            var clahe = Cv2.CreateCLAHE(clipLimit, new Size(tileGridSize, tileGridSize));
            Mat clahe8u = new Mat();
            clahe.Apply(source8u, clahe8u);

            // 3. 转换回16位
            Mat result = new Mat();
            clahe8u.ConvertTo(result, MatType.CV_16UC1, 65535.0 / 255.0);

            source8u.Dispose();
            clahe.Dispose();
            clahe8u.Dispose();

            return result;
        }

        /// <summary>
        /// 应用均值滤波（盒式滤波）
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="kernelSize">滤波核大小 (必须为奇数)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyMeanFilter(Mat source, int kernelSize = 3)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (kernelSize <= 0 || kernelSize % 2 == 0)
                throw new ArgumentException("滤波核大小必须为正奇数", nameof(kernelSize));

            Mat result = new Mat();
            Cv2.Blur(source, result, new Size(kernelSize, kernelSize));
            return result;
        }

        /// <summary>
        /// 应用中值滤波
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="kernelSize">滤波核大小 (必须为奇数)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyMedianFilter(Mat source, int kernelSize = 3)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (kernelSize <= 0 || kernelSize % 2 == 0)
                throw new ArgumentException("滤波核大小必须为正奇数", nameof(kernelSize));

            // 对于16位图像，我们需要先转换为8位进行处理，然后再转换回16位
            Mat source8u = new Mat();
            source.ConvertTo(source8u, MatType.CV_8UC1, 255.0 / 65535.0);

            Mat result8u = new Mat();
            Cv2.MedianBlur(source8u, result8u, kernelSize);

            Mat result = new Mat();
            result8u.ConvertTo(result, MatType.CV_16UC1, 65535.0 / 255.0);

            source8u.Dispose();
            result8u.Dispose();

            return result;
        }

        /// <summary>
        /// 应用拉普拉斯锐化
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="kernelSize">拉普拉斯核大小 (3或5)</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyLaplacianSharpening(Mat source, int kernelSize = 3)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (kernelSize != 3 && kernelSize != 5)
                throw new ArgumentException("拉普拉斯核大小必须为3或5", nameof(kernelSize));

            // 1. 计算拉普拉斯算子
            Mat laplacian = new Mat();
            // 使用CV_16S深度类型来支持负值
            Cv2.Laplacian(source, laplacian, MatType.CV_16S, kernelSize);

            // 2. 将拉普拉斯结果转换为与原图相同的类型
            Mat laplacian16u = new Mat();
            Cv2.ConvertScaleAbs(laplacian, laplacian16u);

            // 3. 将拉普拉斯结果加到原图上实现锐化
            Mat result = new Mat();
            // 显式指定输出数组类型
            Cv2.Add(source, laplacian16u, result, null, MatType.CV_16UC1);

            laplacian.Dispose();
            laplacian16u.Dispose();

            return result;
        }

        /// <summary>
        /// 应用频域高斯低通滤波器
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="sigma">高斯核的标准差</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyFrequencyDomainLowPassFilter(Mat source, double sigma)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (sigma <= 0)
                throw new ArgumentException("Sigma值必须为正数", nameof(sigma));

            // 1. 转换为浮点型
            Mat floatImage = new Mat();
            source.ConvertTo(floatImage, MatType.CV_32F);

            // 2. 执行FFT
            Mat complexImage = CreateComplexImage(floatImage);
            Mat dftResult = new Mat();
            Cv2.Dft(complexImage, dftResult, DftFlags.ComplexOutput);

            // 3. 创建高斯低通滤波器
            Mat filter = CreateGaussianLowPassFilter(dftResult.Rows, dftResult.Cols, sigma);

            // 4. 应用滤波器
            Mat[] channels = Cv2.Split(dftResult);
            Cv2.Multiply(channels[0], filter, channels[0]);  // 实部
            Cv2.Multiply(channels[1], filter, channels[1]);  // 虚部
            Cv2.Merge(channels, dftResult);

            channels[0].Dispose();
            channels[1].Dispose();
            filter.Dispose();

            // 5. 执行逆FFT
            Mat idftResult = new Mat();
            Cv2.Idft(dftResult, idftResult, DftFlags.Scale | DftFlags.RealOutput);

            // 6. 转换回16位
            Mat result = new Mat();
            idftResult.ConvertTo(result, MatType.CV_16UC1);

            // 释放资源
            floatImage.Dispose();
            complexImage.Dispose();
            dftResult.Dispose();
            idftResult.Dispose();

            return result;
        }

        /// <summary>
        /// 应用频域高斯高通滤波器
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="sigma">高斯核的标准差</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyFrequencyDomainHighPassFilter(Mat source, double sigma)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (sigma <= 0)
                throw new ArgumentException("Sigma值必须为正数", nameof(sigma));

            // 1. 转换为浮点型
            Mat floatImage = new Mat();
            source.ConvertTo(floatImage, MatType.CV_32F);

            // 2. 执行FFT
            Mat complexImage = CreateComplexImage(floatImage);
            Mat dftResult = new Mat();
            Cv2.Dft(complexImage, dftResult, DftFlags.ComplexOutput);

            // 3. 创建高斯高通滤波器 (1 - 低通滤波器)
            Mat lowPassFilter = CreateGaussianLowPassFilter(dftResult.Rows, dftResult.Cols, sigma);
            Mat filter = new Mat();
            Cv2.Subtract(Scalar.All(1.0), lowPassFilter, filter);

            // 4. 应用滤波器
            Mat[] channels = Cv2.Split(dftResult);
            Cv2.Multiply(channels[0], filter, channels[0]);  // 实部
            Cv2.Multiply(channels[1], filter, channels[1]);  // 虚部
            Cv2.Merge(channels, dftResult);

            channels[0].Dispose();
            channels[1].Dispose();
            lowPassFilter.Dispose();
            filter.Dispose();

            // 5. 执行逆FFT
            Mat idftResult = new Mat();
            Cv2.Idft(dftResult, idftResult, DftFlags.Scale | DftFlags.RealOutput);

            // 6. 转换回16位
            Mat result = new Mat();
            idftResult.ConvertTo(result, MatType.CV_16UC1);

            // 释放资源
            floatImage.Dispose();
            complexImage.Dispose();
            dftResult.Dispose();
            idftResult.Dispose();

            return result;
        }

        /// <summary>
        /// 创建用于FFT的复数图像
        /// </summary>
        private static Mat CreateComplexImage(Mat realImage)
        {
            Mat zeroMat = Mat.Zeros(realImage.Size(), MatType.CV_32F);
            Mat complexImage = new Mat();
            Cv2.Merge(new Mat[] { realImage, zeroMat }, complexImage);
            zeroMat.Dispose();
            return complexImage;
        }

        /// <summary>
        /// 创建高斯低通滤波器
        /// </summary>
        private static Mat CreateGaussianLowPassFilter(int rows, int cols, double sigma)
        {
            Mat filter = new Mat(rows, cols, MatType.CV_32F);
            int cx = cols / 2;
            int cy = rows / 2;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    double d = Math.Pow(i - cy, 2) + Math.Pow(j - cx, 2);
                    double value = Math.Exp(-d / (2 * Math.Pow(sigma, 2)));
                    filter.Set(i, j, (float)value);
                }
            }

            // 交换象限，使原点在中心
            SwapQuadrants(filter);

            return filter;
        }

        /// <summary>
        /// 交换图像象限
        /// </summary>
        private static void SwapQuadrants(Mat image)
        {
            int cx = image.Cols / 2;
            int cy = image.Rows / 2;

            Mat q0 = new Mat(image, new Rect(0, 0, cx, cy));   // 左上
            Mat q1 = new Mat(image, new Rect(cx, 0, cx, cy));  // 右上
            Mat q2 = new Mat(image, new Rect(0, cy, cx, cy));  // 左下
            Mat q3 = new Mat(image, new Rect(cx, cy, cx, cy)); // 右下

            Mat tmp = new Mat();
            q0.CopyTo(tmp);
            q3.CopyTo(q0);
            tmp.CopyTo(q3);

            q1.CopyTo(tmp);
            q2.CopyTo(q1);
            tmp.CopyTo(q2);

            tmp.Dispose();
            q0.Dispose();
            q1.Dispose();
            q2.Dispose();
            q3.Dispose();
        }

        /// <summary>
        /// 应用噪声抑制与边缘增强的组合处理
        /// </summary>
        /// <param name="source">源图像 (CV_16UC1)</param>
        /// <param name="blurKernelSize">高斯模糊核大小</param>
        /// <param name="laplacianKernelSize">拉普拉斯核大小</param>
        /// <returns>处理后的新图像</returns>
        public static Mat ApplyNoiseSuppressionAndEdgeEnhancement(Mat source, int blurKernelSize = 5, int laplacianKernelSize = 3)
        {
            if (source.Type() != MatType.CV_16UC1)
                throw new ArgumentException("输入图像必须是16位单通道灰度图 (CV_16UC1)");

            if (blurKernelSize <= 0 || blurKernelSize % 2 == 0)
                throw new ArgumentException("模糊核大小必须为正奇数", nameof(blurKernelSize));

            if (laplacianKernelSize != 3 && laplacianKernelSize != 5)
                throw new ArgumentException("拉普拉斯核大小必须为3或5", nameof(laplacianKernelSize));

            // 1. 高斯模糊去噪
            Mat blurred = new Mat();
            Cv2.GaussianBlur(source, blurred, new Size(blurKernelSize, blurKernelSize), 0);

            // 2. 拉普拉斯锐化增强边缘
            Mat laplacian = new Mat();
            // 使用CV_16S深度类型来支持负值
            Cv2.Laplacian(blurred, laplacian, MatType.CV_16S, laplacianKernelSize);

            // 3. 将拉普拉斯结果转换为与原图相同的类型
            Mat laplacian16u = new Mat();
            // 使用Scale方法手动转换，避免ConvertScaleAbs可能的问题
            laplacian.ConvertTo(laplacian16u, MatType.CV_16UC1, 1, 0);
            // 确保值在有效范围内
            Cv2.Min(laplacian16u, Scalar.All(65535), laplacian16u);
            
            // 4. 将边缘增强结果加到去噪后的图像上
            Mat result = new Mat();
            // 使用饱和加法避免溢出
            Cv2.Add(blurred, laplacian16u, result, null, MatType.CV_16UC1);

            // 释放资源
            blurred.Dispose();
            laplacian.Dispose();
            laplacian16u.Dispose();

            return result;
        }
    }
}
