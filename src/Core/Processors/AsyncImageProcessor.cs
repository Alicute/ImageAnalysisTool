using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;
using NLog;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 异步图像处理器 - 支持进度报告和非阻塞处理
    /// </summary>
    public class AsyncImageProcessor
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly PixelProcessor pixelProcessor;
        private CancellationTokenSource cancellationTokenSource;
        private readonly object progressLock = new object();

        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 处理完成事件
        /// </summary>
        public event EventHandler<ProcessingCompleteEventArgs> ProcessingCompleted;

        /// <summary>
        /// 处理错误事件
        /// </summary>
        public event EventHandler<ProcessingErrorEventArgs> ProcessingError;

        public AsyncImageProcessor()
        {
            pixelProcessor = new PixelProcessor();
        }

        /// <summary>
        /// 异步执行直接映射处理（快速版本）
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="target">目标图</param>
        /// <returns>处理任务</returns>
        public async Task<Mat> DirectMappingAsync(Mat original, Mat target)
        {
            var result = await DirectMappingInternalAsync(original, target, false);
            return result.ResultImage;
        }

        /// <summary>
        /// 异步执行直接映射处理（完整数据版本）
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="target">目标图</param>
        /// <returns>处理任务，包含完整750万行数据文件</returns>
        public async Task<(Mat ResultImage, string CompleteDataFile)> DirectMappingWithCompleteDataAsync(Mat original, Mat target)
        {
            return await Task.Run<(Mat ResultImage, string CompleteDataFile)>(async () =>
            {
                var result = await DirectMappingInternalAsync(original, target, true);
                string dataFile = null;
                
                // 如果有完整数据，导出到文件
                if (result.Item2?.PixelDetails?.Count > 0)
                {
                    ReportProgress(result.Item2.PixelDetails.Count, result.Item2.PixelDetails.Count, "正在导出完整750万行数据文件...");
                    dataFile = pixelProcessor.ExportCompletePixelData(result.Item2.PixelDetails, "txt");
                }
                
                return (result.Item1, dataFile);
            });
        }

        /// <summary>
        /// 内部直接映射处理逻辑
        /// </summary>
        private async Task<(Mat ResultImage, ProcessingRule Rule)> DirectMappingInternalAsync(Mat original, Mat target, bool exportCompleteData)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 验证图像类型
                    bool is16Bit = original.Type() == MatType.CV_16UC1 || original.Type() == MatType.CV_16SC1;
                    if (!is16Bit)
                    {
                        throw new ArgumentException("只支持16位DICOM灰度图像，当前图像类型不符合要求");
                    }

                    cancellationTokenSource = new CancellationTokenSource();
                    
                    string mode = exportCompleteData ? "完整数据版本" : "快速版本";
                    logger.Info($"开始异步16位DICOM图像直接映射处理（{mode}）");

                    // 使用带进度报告的像素三元组获取
                    var pixels = GetPixelTriplesWithProgress(original, null, target, cancellationTokenSource.Token);
                    int totalPixels = pixels.Count;
                    
                    // 分析映射规律
                    ReportProgress(totalPixels, totalPixels, "正在分析像素映射关系...");
                    var mapping = pixelProcessor.AnalyzePixelMapping(pixels);
                    
                    // 获取像素详细信息
                    ReportProgress(totalPixels, totalPixels, "正在生成像素映射报告...");
                    var pixelDetails = pixelProcessor.GetPixelMappingDetails(pixels);
                    
                    // 创建处理规则
                    var rule = ProcessingRule.CreateDirectMapping("直接映射", mapping);
                    rule.PixelDetails = pixelDetails;
                    
                    // 应用处理规则到原图
                    var result = pixelProcessor.ApplyProcessingRule(original, rule);
                    
                    // 生成报告并导出到桌面
                    ReportProgress(totalPixels, totalPixels, "正在生成处理报告...");
                    var report = pixelProcessor.GenerateProcessingReport(new List<ProcessingRule> { rule });
                    ReportProgress(totalPixels, totalPixels, "正在导出报告到桌面...");
                    
                    // 触发完成事件
                    OnProcessingCompleted(new ProcessingCompleteEventArgs(result, report));
                    
                    return (result, rule);
                }
                catch (OperationCanceledException)
                {
                    logger.Info("处理已被用户取消");
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "异步直接映射处理失败");
                    OnProcessingError(new ProcessingErrorEventArgs(ex));
                    throw;
                }
                finally
                {
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
            });
        }

        /// <summary>
        /// 带进度报告的像素三元组获取
        /// </summary>
        private List<PixelTriple> GetPixelTriplesWithProgress(Mat original, Mat myEnhanced, Mat target, CancellationToken cancellationToken)
        {
            if (original == null || original.Empty())
                throw new ArgumentException("原图不能为空");

            var triples = new List<PixelTriple>();
            
            try
            {
                int rows = original.Rows;
                int cols = original.Cols;
                int totalPixels = rows * cols;
                
                logger.Info($"开始带进度报告的16位DICOM图像逐像素处理 - 图像尺寸: {cols}x{rows}, 总像素: {totalPixels:N0}");

                var stopwatch = Stopwatch.StartNew();
                triples.Capacity = totalPixels;

                // 报告开始
                ReportProgress(0, totalPixels, "开始处理...");

                int processedCount = 0;
                int progressReportInterval = Math.Max(1, totalPixels / 1000); // 每0.1%报告一次

                for (int y = 0; y < rows; y++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    for (int x = 0; x < cols; x++)
                    {
                        var triple = new PixelTriple
                        {
                            Position = new System.Drawing.Point(x, y),
                            OriginalValue = original.At<ushort>(y, x)
                        };

                        if (myEnhanced != null && !myEnhanced.Empty())
                        {
                            triple.MyEnhancedValue = myEnhanced.At<ushort>(y, x);
                        }

                        if (target != null && !target.Empty())
                        {
                            triple.TargetValue = target.At<ushort>(y, x);
                        }

                        triples.Add(triple);
                        processedCount++;

                        // 定期报告进度
                        if (processedCount % progressReportInterval == 0)
                        {
                            double progressPercent = processedCount * 100.0 / totalPixels;
                            ReportProgress(processedCount, totalPixels, $"处理进度: {progressPercent:F1}%");
                        }
                    }
                }

                stopwatch.Stop();
                logger.Info($"16位DICOM图像逐像素处理完成 - 实际处理数: {triples.Count:N0}, 耗时: {stopwatch.ElapsedMilliseconds}ms");

                // 报告完成
                ReportProgress(totalPixels, totalPixels, "处理完成");

                return triples;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "带进度报告的像素三元组获取失败");
                throw;
            }
        }

        /// <summary>
        /// 报告进度
        /// </summary>
        private void ReportProgress(int processed, int total, string status)
        {
            lock (progressLock)
            {
                ProgressChanged?.Invoke(this, new ProgressEventArgs(processed, total, status));
            }
        }

        /// <summary>
        /// 触发处理完成事件
        /// </summary>
        private void OnProcessingCompleted(ProcessingCompleteEventArgs e)
        {
            ProcessingCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// 触发处理错误事件
        /// </summary>
        private void OnProcessingError(ProcessingErrorEventArgs e)
        {
            ProcessingError?.Invoke(this, e);
        }

        /// <summary>
        /// 取消当前处理
        /// </summary>
        public void Cancel()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                logger.Info("用户取消了处理");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "取消处理时发生错误");
            }
        }

        /// <summary>
        /// 导出完整750万行像素数据（独立方法）
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="target">目标图</param>
        /// <param name="format">导出格式：txt或csv</param>
        /// <returns>完整数据文件路径</returns>
        public async Task<string> ExportCompletePixelDataAsync(Mat original, Mat target, string format = "txt")
        {
            return await Task.Run(() =>
            {
                try
                {
                    logger.Info($"开始导出完整像素数据 - 格式: {format}");

                    // 获取像素数据（带进度报告）
                    var pixels = GetPixelTriplesWithProgress(original, null, target, CancellationToken.None);
                    
                    // 报告进度：正在分析像素映射
                    int totalPixels = pixels.Count;
                    ReportProgress(totalPixels, totalPixels, "正在分析像素映射关系...");
                    
                    var pixelDetails = pixelProcessor.GetPixelMappingDetails(pixels);
                    
                    // 报告进度：正在导出完整数据
                    ReportProgress(totalPixels, totalPixels, $"正在导出完整{totalPixels:N0}行像素数据到{format.ToUpper()}文件...");
                    
                    // 导出完整数据
                    string result = pixelProcessor.ExportCompletePixelData(pixelDetails, format);
                    
                    // 报告进度：导出完成
                    ReportProgress(totalPixels, totalPixels, "完整数据导出完成");
                    
                    return result;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "导出完整像素数据失败");
                    throw;
                }
            });
        }
    }

    /// <summary>
    /// 进度事件参数
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public int Processed { get; }
        public int Total { get; }
        public double ProgressPercentage => Total > 0 ? (double)Processed / Total * 100 : 0;
        public string Status { get; }

        public ProgressEventArgs(int processed, int total, string status)
        {
            Processed = processed;
            Total = total;
            Status = status;
        }
    }

    /// <summary>
    /// 处理完成事件参数
    /// </summary>
    public class ProcessingCompleteEventArgs : EventArgs
    {
        public Mat ResultImage { get; }
        public string Report { get; }

        public ProcessingCompleteEventArgs(Mat resultImage, string report)
        {
            ResultImage = resultImage;
            Report = report;
        }
    }

    /// <summary>
    /// 处理错误事件参数
    /// </summary>
    public class ProcessingErrorEventArgs : EventArgs
    {
        public Exception Error { get; }

        public ProcessingErrorEventArgs(Exception error)
        {
            Error = error;
        }
    }
}