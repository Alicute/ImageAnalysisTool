using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ImageAnalysisTool.UI.Helpers
{
    /// <summary>
    /// 日志文件帮助类 - 用于打开桌面上的日志文件
    /// </summary>
    public static class LogFileHelper
    {
        /// <summary>
        /// 打开最新的日志文件
        /// </summary>
        public static void OpenLatestLogFile()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var logFiles = Directory.GetFiles(desktopPath, "图像处理报告_*.txt");
                
                if (logFiles.Length == 0)
                {
                    MessageBox.Show("桌面上没有找到图像处理报告文件。", "提示", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 获取最新的文件
                var latestFile = logFiles.OrderByDescending(f => f).First();
                
                // 用记事本打开文件
                Process.Start(new ProcessStartInfo
                {
                    FileName = latestFile,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志文件失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 检查是否存在日志文件
        /// </summary>
        public static bool HasLogFiles()
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var logFiles = Directory.GetFiles(desktopPath, "图像处理报告_*.txt");
                return logFiles.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }
}