using NUnit.Framework;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ImageAnalysisTool.Core.Processors;
using ImageAnalysisTool.Core.Models;

namespace ImageAnalysisTool.Core.Tests
{
    [TestFixture]
    public class ExportPerformanceComparisonTests
    {
        private AsyncImageProcessor processor;
        private const int TestImageSize = 2000; // 2000x2000 = 4M pixels

        [SetUp]
        public void Setup()
        {
            processor = new AsyncImageProcessor();
        }

        [Test]
        public async Task CompareExportMethods_PerformanceAndMemory()
        {
            // Arrange - 创建大测试图像
            using var original = CreateTestImage(TestImageSize);
            using var target = CreateTargetImage(original);
            
            var memoryBefore = GC.GetTotalMemory(true);
            
            // 测试原始方法
            var originalStopwatch = Stopwatch.StartNew();
            var originalResult = await TestOriginalMethod(original, target);
            originalStopwatch.Stop();
            
            var memoryAfterOriginal = GC.GetTotalMemory(true);
            
            // 强制垃圾回收
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var memoryBeforeOptimized = GC.GetTotalMemory(true);
            
            // 测试优化方法
            var optimizedStopwatch = Stopwatch.StartNew();
            var optimizedResult = await processor.ExportCompletePixelDataOptimizedAsync(
                original, target, "txt");
            optimizedStopwatch.Stop();
            
            var memoryAfterOptimized = GC.GetTotalMemory(true);
            
            // 输出结果
            Console.WriteLine($"=== 性能对比结果 ===");
            Console.WriteLine($"图像尺寸: {TestImageSize}x{TestImageSize} ({TestImageSize * TestImageSize:N0} 像素)");
            Console.WriteLine();
            Console.WriteLine($"原始方法:");
            Console.WriteLine($"  - 耗时: {originalStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  - 内存增长: {(memoryAfterOriginal - memoryBefore) / 1024 / 1024:F1}MB");
            Console.WriteLine($"  - 文件大小: {new FileInfo(originalResult).Length / 1024 / 1024:F1}MB");
            Console.WriteLine();
            Console.WriteLine($"优化方法:");
            Console.WriteLine($"  - 耗时: {optimizedStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  - 内存增长: {(memoryAfterOptimized - memoryBeforeOptimized) / 1024 / 1024:F1}MB");
            Console.WriteLine($"  - 文件大小: {optimizedResult.GetFormattedFileSize()}");
            Console.WriteLine();
            Console.WriteLine($"性能提升:");
            Console.WriteLine($"  - 速度提升: {(double)originalStopwatch.ElapsedMilliseconds / optimizedStopwatch.ElapsedMilliseconds:F2}x");
            Console.WriteLine($"  - 内存节省: {((memoryAfterOriginal - memoryBefore) - (memoryAfterOptimized - memoryBeforeOptimized)) / 1024 / 1024:F1}MB");
            
            // 清理
            if (File.Exists(originalResult))
                File.Delete(originalResult);
            if (File.Exists(optimizedResult.FilePath))
                File.Delete(optimizedResult.FilePath);
        }

        [Test]
        public async Task TestCancellationBehavior()
        {
            // Arrange
            using var original = CreateTestImage(1000);
            using var target = CreateTargetImage(original);
            
            var cts = new CancellationTokenSource();
            
            // 启动任务
            var task = processor.ExportCompletePixelDataOptimizedAsync(
                original, target, "txt", cts.Token);
            
            // 等待一段时间后取消
            await Task.Delay(100);
            cts.Cancel();
            
            // Act
            var result = await task;
            
            // Assert
            Assert.IsTrue(result.Cancelled);
            Assert.IsFalse(result.Success);
            Assert.Greater(result.ProcessedRows, 0);
            Assert.Less(result.ProcessedRows, 1000);
            
            Console.WriteLine($"取消时已处理: {result.ProcessedRows} 行");
            Console.WriteLine($"取消时耗时: {result.Duration.TotalMilliseconds:F1}ms");
        }

        [Test]
        public async Task TestProgressReportingAccuracy()
        {
            // Arrange
            using var original = CreateTestImage(500);
            using var target = CreateTargetImage(original);
            
            var progressReports = new List<(int Processed, int Total, string Status)>();
            processor.ProgressChanged += (sender, e) => {
                progressReports.Add((e.Processed, e.Total, e.Status));
            };
            
            // Act
            var result = await processor.ExportCompletePixelDataOptimizedAsync(
                original, target, "txt");
            
            // Assert
            Assert.IsTrue(progressReports.Count > 0);
            
            // 验证进度是递增的
            for (int i = 1; i < progressReports.Count; i++)
            {
                Assert.GreaterOrEqual(progressReports[i].Processed, progressReports[i - 1].Processed);
            }
            
            // 验证最后报告100%
            var lastReport = progressReports.Last();
            Assert.AreEqual(500 * 500, lastReport.Processed);
            Assert.AreEqual(500 * 500, lastReport.Total);
            
            Console.WriteLine($"收到 {progressReports.Count} 次进度更新");
            
            // 清理
            if (File.Exists(result.FilePath))
                File.Delete(result.FilePath);
        }

        private Mat CreateTestImage(int size)
        {
            var mat = new Mat(size, size, MatType.CV_16UC1);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    mat.Set<ushort>(y, x, (ushort)((y * size + x) % 65536));
                }
            }
            return mat;
        }

        private Mat CreateTargetImage(Mat original)
        {
            var target = new Mat(original.Size(), original.Type());
            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    var originalValue = original.At<ushort>(y, x);
                    target.Set<ushort>(y, x, (ushort)((originalValue * 1.5) % 65536));
                }
            }
            return target;
        }

        private async Task<string> TestOriginalMethod(Mat original, Mat target)
        {
            // 使用原始的导出方法
            var pixelProcessor = new PixelProcessor();
            var pixels = pixelProcessor.GetPixelTriples(original, null, target);
            var pixelDetails = pixelProcessor.GetPixelMappingDetails(pixels);
            return pixelProcessor.ExportCompletePixelData(pixelDetails, "txt");
        }
    }
}