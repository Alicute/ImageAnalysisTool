using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using DcmGUI.Core.Interfaces;
using DcmGUI.Core.Models;

namespace DcmGUI.Core.Utils
{
    /// <summary>
    /// DICOM图像管理器，专门处理DICOM文件的加载和保存
    /// </summary>
    public class DicomImageManager : Interfaces.IImageManager
    {
        /// <summary>
        /// 异步加载DICOM图像
        /// </summary>
        /// <param name="filePath">DICOM文件路径</param>
        /// <returns>ImageData对象</returns>
        public async Task<ImageData> LoadImageAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("DICOM文件不存在", filePath);

            return await Task.Run(() =>
            {
                try
                {
                    // 加载DICOM文件
                    var dicomFile = DicomFile.Open(filePath);
                    var dataset = dicomFile.Dataset;

                    // 获取图像基本信息
                    var width = dataset.GetSingleValue<int>(DicomTag.Columns);
                    var height = dataset.GetSingleValue<int>(DicomTag.Rows);
                    var bitsAllocated = dataset.GetSingleValueOrDefault(DicomTag.BitsAllocated, (ushort)16);
                    var bitsStored = dataset.GetSingleValueOrDefault(DicomTag.BitsStored, (ushort)16);
                    var highBit = dataset.GetSingleValueOrDefault(DicomTag.HighBit, (ushort)(bitsStored - 1));
                    var pixelRepresentation = dataset.GetSingleValueOrDefault(DicomTag.PixelRepresentation, (ushort)0);
                    var isSigned = pixelRepresentation == 1;

                    // 创建ImageData对象
                    var imageData = new ImageData(width, height, bitsAllocated)
                    {
                        FilePath = filePath,
                        IsSigned = isSigned
                    };

                    // 提取DICOM标签信息
                    ExtractDicomTags(dataset, imageData);

                    // 获取像素数据
                    var pixelData = DicomPixelData.Create(dataset);

                    // 检查是否有像素数据
                    if (pixelData.NumberOfFrames == 0)
                    {
                        throw new InvalidOperationException("DICOM文件中没有找到像素数据");
                    }

                    var frame = pixelData.GetFrame(0);

                    if (bitsAllocated == 16)
                    {
                        // 16位数据处理
                        if (isSigned)
                        {
                            var signedData = new short[frame.Data.Length / 2];
                            Buffer.BlockCopy(frame.Data, 0, signedData, 0, frame.Data.Length);
                            imageData.PixelData16 = signedData.Select(x => (ushort)(x + 32768)).ToArray();
                        }
                        else
                        {
                            var unsignedData = new ushort[frame.Data.Length / 2];
                            Buffer.BlockCopy(frame.Data, 0, unsignedData, 0, frame.Data.Length);
                            imageData.PixelData16 = unsignedData;
                        }
                    }
                    else
                    {
                        // 8位数据处理
                        imageData.PixelData8 = frame.Data;
                        imageData.BitsPerPixel = 8;
                    }

                    // 获取窗宽窗位信息
                    ExtractWindowLevelInfo(dataset, imageData);

                    // 如果没有窗宽窗位信息，自动计算
                    if (imageData.WindowWidth <= 0 || imageData.WindowCenter <= 0)
                    {
                        imageData.AutoCalculateWindowLevel();
                    }

                    return imageData;
                }
                catch (Exception ex)
                {
                    // 提供更详细的错误信息
                    var errorMessage = $"加载DICOM文件失败: {ex.Message}";
                    if (ex.InnerException != null)
                    {
                        errorMessage += $"\n内部错误: {ex.InnerException.Message}";
                    }
                    throw new InvalidOperationException(errorMessage, ex);
                }
            });
        }

        /// <summary>
        /// 异步保存DICOM图像
        /// </summary>
        /// <param name="image">图像数据</param>
        /// <param name="filePath">保存路径</param>
        /// <returns>是否成功</returns>
        public async Task<bool> SaveImageAsync(ImageData image, string filePath)
        {
            if (image == null || !image.IsValid())
                throw new ArgumentException("图像数据无效");

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径无效");

            return await Task.Run(() =>
            {
                try
                {
                    // 创建新的DICOM数据集
                    var dataset = new DicomDataset();

                    // 设置基本的DICOM标签
                    SetBasicDicomTags(dataset, image);

                    // 设置像素数据
                    if (image.BitsPerPixel == 16 && image.PixelData16 != null)
                    {
                        var pixelData = DicomPixelData.Create(dataset, true);
                        var byteData = new byte[image.PixelData16.Length * 2];
                        Buffer.BlockCopy(image.PixelData16, 0, byteData, 0, byteData.Length);
                        var buffer = new MemoryByteBuffer(byteData);
                        pixelData.AddFrame(buffer);
                    }
                    else if (image.PixelData8 != null)
                    {
                        var pixelData = DicomPixelData.Create(dataset, true);
                        var buffer = new MemoryByteBuffer(image.PixelData8);
                        pixelData.AddFrame(buffer);
                    }

                    // 创建DICOM文件并保存
                    var dicomFile = new DicomFile(dataset);
                    dicomFile.Save(filePath);

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// 创建图像备份
        /// </summary>
        /// <param name="image">原始图像</param>
        /// <returns>备份图像</returns>
        public ImageData? CreateBackup(ImageData? image)
        {
            return image?.Clone();
        }

        /// <summary>
        /// 从备份恢复图像
        /// </summary>
        /// <param name="backup">备份图像</param>
        /// <returns>恢复的图像</returns>
        public ImageData? RestoreFromBackup(ImageData? backup)
        {
            return backup?.Clone();
        }

        /// <summary>
        /// 提取DICOM标签信息
        /// </summary>
        /// <param name="dataset">DICOM数据集</param>
        /// <param name="imageData">图像数据对象</param>
        private void ExtractDicomTags(DicomDataset dataset, ImageData imageData)
        {
            // 患者信息
            imageData.DicomTags["PatientName"] = dataset.GetSingleValueOrDefault(DicomTag.PatientName, "");
            imageData.DicomTags["PatientID"] = dataset.GetSingleValueOrDefault(DicomTag.PatientID, "");
            imageData.DicomTags["PatientBirthDate"] = dataset.GetSingleValueOrDefault(DicomTag.PatientBirthDate, "");
            imageData.DicomTags["PatientSex"] = dataset.GetSingleValueOrDefault(DicomTag.PatientSex, "");

            // 检查信息
            imageData.DicomTags["StudyDate"] = dataset.GetSingleValueOrDefault(DicomTag.StudyDate, "");
            imageData.DicomTags["StudyTime"] = dataset.GetSingleValueOrDefault(DicomTag.StudyTime, "");
            imageData.DicomTags["StudyDescription"] = dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "");
            imageData.DicomTags["SeriesDescription"] = dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "");

            // 设备信息
            imageData.DicomTags["Manufacturer"] = dataset.GetSingleValueOrDefault(DicomTag.Manufacturer, "");
            imageData.DicomTags["ManufacturerModelName"] = dataset.GetSingleValueOrDefault(DicomTag.ManufacturerModelName, "");
            imageData.DicomTags["Modality"] = dataset.GetSingleValueOrDefault(DicomTag.Modality, "");

            // 图像信息
            imageData.DicomTags["SliceThickness"] = dataset.GetSingleValueOrDefault(DicomTag.SliceThickness, 0.0);

            // 安全获取可能不存在的标签
            try
            {
                imageData.DicomTags["PixelSpacing"] = dataset.GetValues<double>(DicomTag.PixelSpacing);
            }
            catch
            {
                imageData.DicomTags["PixelSpacing"] = new double[] { 1.0, 1.0 };
            }

            try
            {
                imageData.DicomTags["ImagePosition"] = dataset.GetValues<double>(DicomTag.ImagePositionPatient);
            }
            catch
            {
                imageData.DicomTags["ImagePosition"] = new double[] { 0.0, 0.0, 0.0 };
            }

            try
            {
                imageData.DicomTags["ImageOrientation"] = dataset.GetValues<double>(DicomTag.ImageOrientationPatient);
            }
            catch
            {
                imageData.DicomTags["ImageOrientation"] = new double[] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0 };
            }
        }

        /// <summary>
        /// 提取窗宽窗位信息
        /// </summary>
        /// <param name="dataset">DICOM数据集</param>
        /// <param name="imageData">图像数据对象</param>
        private void ExtractWindowLevelInfo(DicomDataset dataset, ImageData imageData)
        {
            try
            {
                // 尝试获取窗宽窗位
                try
                {
                    var windowCenter = dataset.GetValues<double>(DicomTag.WindowCenter);
                    var windowWidth = dataset.GetValues<double>(DicomTag.WindowWidth);

                    if (windowCenter?.Length > 0 && windowWidth?.Length > 0)
                    {
                        imageData.WindowCenter = windowCenter[0];
                        imageData.WindowWidth = windowWidth[0];
                    }
                }
                catch
                {
                    // 窗宽窗位标签不存在，使用默认值
                    imageData.WindowCenter = 2048;
                    imageData.WindowWidth = 4096;
                }

                // 获取像素值范围
                var rescaleIntercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);
                var rescaleSlope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);

                imageData.DicomTags["RescaleIntercept"] = rescaleIntercept;
                imageData.DicomTags["RescaleSlope"] = rescaleSlope;
            }
            catch (Exception)
            {
                // 如果获取失败，使用默认值
                imageData.WindowCenter = 2048;
                imageData.WindowWidth = 4096;
                imageData.DicomTags["RescaleIntercept"] = 0.0;
                imageData.DicomTags["RescaleSlope"] = 1.0;
            }
        }

        /// <summary>
        /// 设置基本的DICOM标签
        /// </summary>
        /// <param name="dataset">DICOM数据集</param>
        /// <param name="image">图像数据</param>
        private void SetBasicDicomTags(DicomDataset dataset, ImageData image)
        {
            // SOP Class UID
            dataset.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
            dataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());

            // 图像信息
            dataset.AddOrUpdate(DicomTag.Columns, (ushort)image.Width);
            dataset.AddOrUpdate(DicomTag.Rows, (ushort)image.Height);
            dataset.AddOrUpdate(DicomTag.BitsAllocated, (ushort)image.BitsPerPixel);
            dataset.AddOrUpdate(DicomTag.BitsStored, (ushort)image.BitsPerPixel);
            dataset.AddOrUpdate(DicomTag.HighBit, (ushort)(image.BitsPerPixel - 1));
            dataset.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)(image.IsSigned ? 1 : 0));
            dataset.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)1);
            dataset.AddOrUpdate(DicomTag.PhotometricInterpretation, "MONOCHROME2");

            // 窗宽窗位
            dataset.AddOrUpdate(DicomTag.WindowCenter, image.WindowCenter);
            dataset.AddOrUpdate(DicomTag.WindowWidth, image.WindowWidth);

            // 基本患者和检查信息
            dataset.AddOrUpdate(DicomTag.PatientName, "Anonymous");
            dataset.AddOrUpdate(DicomTag.PatientID, "000000");
            dataset.AddOrUpdate(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.AddOrUpdate(DicomTag.StudyDate, DateTime.Now.ToString("yyyyMMdd"));
            dataset.AddOrUpdate(DicomTag.StudyTime, DateTime.Now.ToString("HHmmss"));
            dataset.AddOrUpdate(DicomTag.Modality, "OT");
        }

        /// <summary>
        /// 获取支持的DICOM文件格式过滤器字符串
        /// </summary>
        /// <returns>文件过滤器字符串</returns>
        public static string GetSupportedFormatsFilter()
        {
            return "DICOM文件|*.dcm;*.dicom;*.dic|所有文件|*.*";
        }

        /// <summary>
        /// 获取保存DICOM文件格式过滤器字符串
        /// </summary>
        /// <returns>保存文件过滤器字符串</returns>
        public static string GetSaveFormatsFilter()
        {
            return "DICOM文件|*.dcm";
        }
    }
}
