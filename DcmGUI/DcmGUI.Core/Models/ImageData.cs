using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DcmGUI.Core.Models
{
    /// <summary>
    /// DICOM图像数据模型，专门用于16位医学图像处理
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// 图像宽度
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 图像高度
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 位深度（8位或16位）
        /// </summary>
        public int BitsPerPixel { get; set; } = 16;

        /// <summary>
        /// 是否为有符号数据
        /// </summary>
        public bool IsSigned { get; set; } = false;

        /// <summary>
        /// 16位像素数据数组（主要用于DICOM）
        /// </summary>
        public ushort[]? PixelData16 { get; set; }

        /// <summary>
        /// 8位像素数据数组（用于显示和兼容性）
        /// </summary>
        public byte[]? PixelData8 { get; set; }

        /// <summary>
        /// 窗宽（Window Width）
        /// </summary>
        public double WindowWidth { get; set; } = 4096;

        /// <summary>
        /// 窗位（Window Center）
        /// </summary>
        public double WindowCenter { get; set; } = 2048;

        /// <summary>
        /// 最小像素值
        /// </summary>
        public double MinPixelValue { get; set; }

        /// <summary>
        /// 最大像素值
        /// </summary>
        public double MaxPixelValue { get; set; }

        /// <summary>
        /// 像素格式
        /// </summary>
        public PixelFormat Format { get; set; } = PixelFormats.Gray16;

        /// <summary>
        /// 文件路径
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// DICOM标签信息
        /// </summary>
        public Dictionary<string, object> DicomTags { get; set; } = new();

        /// <summary>
        /// 每行字节数
        /// </summary>
        public int Stride => Width * (BitsPerPixel / 8);

        /// <summary>
        /// 构造函数（16位DICOM图像）
        /// </summary>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <param name="bitsPerPixel">位深度</param>
        public ImageData(int width, int height, int bitsPerPixel = 16)
        {
            Width = width;
            Height = height;
            BitsPerPixel = bitsPerPixel;

            if (bitsPerPixel == 16)
            {
                PixelData16 = new ushort[width * height];
                Format = PixelFormats.Gray16;
            }
            else
            {
                PixelData8 = new byte[width * height];
                Format = PixelFormats.Gray8;
            }
        }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public ImageData()
        {
        }

        /// <summary>
        /// 克隆图像数据
        /// </summary>
        /// <returns>克隆的图像数据</returns>
        public ImageData Clone()
        {
            var clone = new ImageData(Width, Height, BitsPerPixel)
            {
                FilePath = FilePath,
                Format = Format,
                WindowWidth = WindowWidth,
                WindowCenter = WindowCenter,
                MinPixelValue = MinPixelValue,
                MaxPixelValue = MaxPixelValue,
                IsSigned = IsSigned,
                DicomTags = new Dictionary<string, object>(DicomTags)
            };

            if (BitsPerPixel == 16 && PixelData16 != null)
            {
                clone.PixelData16 = new ushort[PixelData16.Length];
                Array.Copy(PixelData16, clone.PixelData16, PixelData16.Length);
            }
            else if (PixelData8 != null)
            {
                clone.PixelData8 = new byte[PixelData8.Length];
                Array.Copy(PixelData8, clone.PixelData8, PixelData8.Length);
            }

            return clone;
        }

        /// <summary>
        /// 转换为BitmapSource用于WPF显示（应用窗宽窗位）
        /// </summary>
        /// <returns>BitmapSource对象</returns>
        public BitmapSource? ToBitmapSource()
        {
            try
            {
                if (BitsPerPixel == 16 && PixelData16 != null)
                {
                    // 应用窗宽窗位转换为8位显示
                    var displayData = ApplyWindowLeveling();
                    return BitmapSource.Create(
                        Width, Height, 96, 96, PixelFormats.Gray8, null, displayData, Width);
                }
                else if (PixelData8 != null)
                {
                    return BitmapSource.Create(
                        Width, Height, 96, 96, PixelFormats.Gray8, null, PixelData8, Width);
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 应用窗宽窗位转换为8位显示数据
        /// </summary>
        /// <returns>8位显示数据</returns>
        public byte[] ApplyWindowLeveling()
        {
            if (PixelData16 == null)
                return new byte[0];

            var displayData = new byte[Width * Height];
            double windowMin = WindowCenter - WindowWidth / 2.0;
            double windowMax = WindowCenter + WindowWidth / 2.0;

            Parallel.For(0, PixelData16.Length, i =>
            {
                double pixelValue = PixelData16[i];

                // 应用窗宽窗位
                if (pixelValue <= windowMin)
                {
                    displayData[i] = 0;
                }
                else if (pixelValue >= windowMax)
                {
                    displayData[i] = 255;
                }
                else
                {
                    displayData[i] = (byte)((pixelValue - windowMin) / (windowMax - windowMin) * 255);
                }
            });

            return displayData;
        }

        /// <summary>
        /// 自动计算窗宽窗位
        /// </summary>
        public void AutoCalculateWindowLevel()
        {
            if (PixelData16 == null || PixelData16.Length == 0)
                return;

            // 计算直方图
            var histogram = new int[65536];
            foreach (var pixel in PixelData16)
            {
                histogram[pixel]++;
            }

            // 找到有效像素范围（排除0值）
            int minValue = 0, maxValue = 65535;
            for (int i = 1; i < histogram.Length; i++)
            {
                if (histogram[i] > 0)
                {
                    minValue = i;
                    break;
                }
            }

            for (int i = histogram.Length - 1; i >= 0; i--)
            {
                if (histogram[i] > 0)
                {
                    maxValue = i;
                    break;
                }
            }

            // 计算累积直方图，找到1%和99%分位点
            long totalPixels = PixelData16.Length;
            long cumulativeCount = 0;
            int percentile1 = minValue, percentile99 = maxValue;

            for (int i = minValue; i <= maxValue; i++)
            {
                cumulativeCount += histogram[i];
                double percentile = (double)cumulativeCount / totalPixels;

                if (percentile >= 0.01 && percentile1 == minValue)
                {
                    percentile1 = i;
                }
                if (percentile >= 0.99)
                {
                    percentile99 = i;
                    break;
                }
            }

            // 设置窗宽窗位
            WindowCenter = (percentile1 + percentile99) / 2.0;
            WindowWidth = percentile99 - percentile1;
            MinPixelValue = minValue;
            MaxPixelValue = maxValue;

            // 确保窗宽不为0
            if (WindowWidth <= 0)
            {
                WindowWidth = maxValue - minValue;
                if (WindowWidth <= 0)
                    WindowWidth = 4096;
            }
        }

        /// <summary>
        /// 获取指定位置的16位像素值
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>像素值</returns>
        public ushort GetPixel16(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || PixelData16 == null)
                return 0;

            int index = y * Width + x;
            return PixelData16[index];
        }

        /// <summary>
        /// 设置指定位置的16位像素值
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="value">像素值</param>
        public void SetPixel16(int x, int y, ushort value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || PixelData16 == null)
                return;

            int index = y * Width + x;
            PixelData16[index] = value;
        }

        /// <summary>
        /// 获取指定位置的8位像素值
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>像素值</returns>
        public byte GetPixel8(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || PixelData8 == null)
                return 0;

            int index = y * Width + x;
            return PixelData8[index];
        }

        /// <summary>
        /// 设置指定位置的8位像素值
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="value">像素值</param>
        public void SetPixel8(int x, int y, byte value)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height || PixelData8 == null)
                return;

            int index = y * Width + x;
            PixelData8[index] = value;
        }

        /// <summary>
        /// 获取图像信息字符串
        /// </summary>
        /// <returns>图像信息</returns>
        public string GetImageInfo()
        {
            var sizeInfo = $"{Width} x {Height}";
            var bitDepthInfo = $"{BitsPerPixel}位";
            var windowInfo = $"WW:{WindowWidth:F0} WC:{WindowCenter:F0}";
            var rangeInfo = $"范围:[{MinPixelValue:F0}-{MaxPixelValue:F0}]";

            return $"{sizeInfo} | {bitDepthInfo} | {windowInfo} | {rangeInfo}";
        }

        /// <summary>
        /// 验证图像数据是否有效
        /// </summary>
        /// <returns>是否有效</returns>
        public bool IsValid()
        {
            if (Width <= 0 || Height <= 0)
                return false;

            if (BitsPerPixel == 16)
            {
                return PixelData16 != null && PixelData16.Length == Width * Height;
            }
            else
            {
                return PixelData8 != null && PixelData8.Length == Width * Height;
            }
        }

        /// <summary>
        /// 从DICOM文件创建ImageData（静态工厂方法）
        /// </summary>
        /// <param name="dicomFile">DICOM文件路径</param>
        /// <returns>ImageData对象</returns>
        public static ImageData? FromDicomFile(string dicomFile)
        {
            // 这个方法将在DicomImageManager中实现
            throw new NotImplementedException("请使用DicomImageManager.LoadImageAsync方法");
        }
    }
}
