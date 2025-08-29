using System;

namespace ImageAnalysisTool.Core.Models
{
    /// <summary>
    /// 像素数据导出结果
    /// </summary>
    public class ExportResult
    {
        /// <summary>
        /// 导出是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 是否被用户取消
        /// </summary>
        public bool Cancelled { get; set; }

        /// <summary>
        /// 导出文件路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 导出的像素总数
        /// </summary>
        public int TotalPixels { get; set; }

        /// <summary>
        /// 导出耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 导出格式
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// 处理的像素行数
        /// </summary>
        public int ProcessedRows { get; set; }

        /// <summary>
        /// 创建成功的导出结果
        /// </summary>
        public static ExportResult CreateSuccess(
            string filePath, 
            int totalPixels, 
            TimeSpan duration, 
            long fileSize, 
            string format,
            int processedRows)
        {
            return new ExportResult
            {
                Success = true,
                FilePath = filePath,
                TotalPixels = totalPixels,
                Duration = duration,
                FileSize = fileSize,
                Format = format,
                ProcessedRows = processedRows
            };
        }

        /// <summary>
        /// 创建取消的导出结果
        /// </summary>
        public static ExportResult CreateCancelled(
            int processedPixels, 
            TimeSpan duration)
        {
            return new ExportResult
            {
                Success = false,
                Cancelled = true,
                TotalPixels = processedPixels,
                Duration = duration,
                ErrorMessage = "导出已被用户取消"
            };
        }

        /// <summary>
        /// 创建失败的导出结果
        /// </summary>
        public static ExportResult CreateFailure(
            string errorMessage, 
            TimeSpan duration, 
            int processedPixels = 0)
        {
            return new ExportResult
            {
                Success = false,
                TotalPixels = processedPixels,
                Duration = duration,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        public string GetFormattedFileSize()
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = FileSize;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string GetStatusDescription()
        {
            if (Cancelled)
                return "已取消";
            if (!Success)
                return "失败";
            return "成功";
        }
    }
}