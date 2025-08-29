using NUnit.Framework;
using OpenCvSharp;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageAnalysisTool.Core.Processors;
using ImageAnalysisTool.Core.Models;

namespace ImageAnalysisTool.Core.Tests
{
    [TestFixture]
    public class AsyncImageProcessorTests
    {
        private AsyncImageProcessor processor;
        private Mat testImage;
        private Mat targetImage;

        [SetUp]
        public void Setup()
        {
            processor = new AsyncImageProcessor();
            
            // 创建测试图像 (100x100 16位灰度图)
            testImage = new Mat(100, 100, MatType.CV_16UC1);
            targetImage = new Mat(100, 100, MatType.CV_16UC1);
            
            // 填充测试数据
            for (int y = 0; y < testImage.Height; y++)
            {
                for (int x = 0; x < testImage.Width; x++)
                {
                    testImage.Set<ushort>(y, x, (ushort)((y * testImage.Width + x) % 65536));
                    targetImage.Set<ushort>(y, x, (ushort)(((y * testImage.Width + x) * 2) % 65536));
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            testImage?.Dispose();
            targetImage?.Dispose();
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_Success_ReturnsValidResult()
        {
            // Arrange
            string format = "txt";
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                testImage, targetImage, format);
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Cancelled);
            Assert.IsNotNull(result.FilePath);
            Assert.AreEqual(10000, result.TotalPixels);
            Assert.AreEqual(100, result.ProcessedRows);
            Assert.AreEqual(format, result.Format);
            Assert.IsTrue(File.Exists(result.FilePath));
            Assert.Greater(result.Duration.TotalMilliseconds, 0);
            Assert.Greater(result.FileSize, 0);
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_CSVFormat_ReturnsCSVFile()
        {
            // Arrange
            string format = "csv";
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                testImage, targetImage, format);
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.FilePath.EndsWith(".csv"));
            
            // 验证CSV格式
            var lines = await File.ReadAllLinesAsync(result.FilePath);
            Assert.IsTrue(lines[0].Contains("序号,位置X,位置Y,原始值,目标值,变化量,变化百分比"));
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_NullOriginal_ReturnsFailure()
        {
            // Arrange
            Mat nullImage = null;
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                nullImage, targetImage);
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Cancelled);
            Assert.AreEqual("原图不能为空", result.ErrorMessage);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_SizeMismatch_ReturnsFailure()
        {
            // Arrange
            var differentSizeImage = new Mat(50, 100, MatType.CV_16UC1);
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                testImage, differentSizeImage);
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual("原图和目标图尺寸不匹配", result.ErrorMessage);
            
            // 清理
            differentSizeImage.Dispose();
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_WithCancellation_ReturnsCancelled()
        {
            // Arrange
            var cts = new CancellationTokenSource();
            
            // 启动任务并立即取消
            var task = processor.ExportCompletePixelDataOptimizedAsync(
                testImage, targetImage, "txt", cts.Token);
            
            cts.Cancel();
            
            // Act
            var result = await task;
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Cancelled);
            Assert.AreEqual("导出已被用户取消", result.ErrorMessage);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_ProgressReporting_ReportsProgress()
        {
            // Arrange
            var progressEvents = new List<int>();
            processor.ProgressChanged += (sender, e) => {
                progressEvents.Add(e.Processed);
            };
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                testImage, targetImage);
            
            // Assert
            Assert.IsTrue(progressEvents.Count > 0);
            Assert.IsTrue(progressEvents[progressEvents.Count - 1] == 10000);
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_CustomChunkSize_WorksCorrectly()
        {
            // Arrange
            int customChunkSize = 50000;
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                testImage, targetImage, "txt", default, customChunkSize);
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10000, result.TotalPixels);
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        [Test]
        public async Task ExportCompletePixelDataOptimizedAsync_LargeImage_PerformanceTest()
        {
            // Arrange - 创建较大的测试图像 (1000x1000)
            using var largeImage = new Mat(1000, 1000, MatType.CV_16UC1);
            using var largeTarget = new Mat(1000, 1000, MatType.CV_16UC1);
            
            // 填充数据
            for (int y = 0; y < largeImage.Height; y++)
            {
                for (int x = 0; x < largeImage.Width; x++)
                {
                    largeImage.Set<ushort>(y, x, (ushort)((y * largeImage.Width + x) % 65536));
                    largeTarget.Set<ushort>(y, x, (ushort)(((y * largeImage.Width + x) * 2) % 65536));
                }
            }
            
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                largeImage, largeTarget, "txt");
            stopwatch.Stop();
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1000000, result.TotalPixels);
            Assert.Less(stopwatch.ElapsedMilliseconds, 10000); // 应该在10秒内完成
            
            // 记录性能数据
            TestContext.Out.WriteLine($"Large image export completed in {stopwatch.ElapsedMilliseconds}ms");
            TestContext.Out.WriteLine($"File size: {result.GetFormattedFileSize()}");
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        [Test]
        public void ExportResult_GetFormattedFileSize_ReturnsCorrectFormat()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("0 B", ExportResult.CreateSuccess("test.txt", 1, TimeSpan.Zero, 0, "txt", 1).GetFormattedFileSize());
            Assert.AreEqual("1.00 KB", ExportResult.CreateSuccess("test.txt", 1, TimeSpan.Zero, 1024, "txt", 1).GetFormattedFileSize());
            Assert.AreEqual("1.00 MB", ExportResult.CreateSuccess("test.txt", 1, TimeSpan.Zero, 1024 * 1024, "txt", 1).GetFormattedFileSize());
            Assert.AreEqual("1.00 GB", ExportResult.CreateSuccess("test.txt", 1, TimeSpan.Zero, 1024 * 1024 * 1024, "txt", 1).GetFormattedFileSize());
        }

        [Test]
        public void ExportResult_GetStatusDescription_ReturnsCorrectStatus()
        {
            // Arrange & Act & Assert
            Assert.AreEqual("成功", ExportResult.CreateSuccess("test.txt", 1, TimeSpan.Zero, 1, "txt", 1).GetStatusDescription());
            Assert.AreEqual("已取消", ExportResult.CreateCancelled(1, TimeSpan.Zero).GetStatusDescription());
            Assert.AreEqual("失败", ExportResult.CreateFailure("error", TimeSpan.Zero).GetStatusDescription());
        }
    }
}