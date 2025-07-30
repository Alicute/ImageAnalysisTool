using System;
using System.Windows.Forms;
using ImageAnalysisTool.UI.Forms;

namespace ImageAnalysisTool
{
    /// <summary>
    /// 图像增强分析工具主程序
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            try
            {
                // 启动图像增强分析窗口
                Application.Run(new EnhancementAnalysisForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"程序启动失败: {ex.Message}", "错误", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}