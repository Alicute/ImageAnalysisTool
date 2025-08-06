using System;
using System.Drawing;
using System.Windows.Forms;

namespace ImageAnalysisTool.UI.Controls
{
    /// <summary>
    /// 简单的进度条控件 - 显示处理进度百分比
    /// </summary>
    public class SimpleProgressBar : UserControl
    {
        private int _value;
        private int _maximum = 100;
        private string _status = "准备就绪";

        public int Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = Math.Max(0, Math.Min(value, _maximum));
                    Invalidate();
                }
            }
        }

        public int Maximum
        {
            get => _maximum;
            set
            {
                if (_maximum != value)
                {
                    _maximum = Math.Max(1, value);
                    _value = Math.Min(_value, _maximum);
                    Invalidate();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    Invalidate();
                }
            }
        }

        public double Percentage => Maximum > 0 ? (double)Value / Maximum * 100 : 0;

        public SimpleProgressBar()
        {
            Size = new Size(300, 60);
            BackColor = Color.White;
            ForeColor = Color.Black;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }

            // 绘制边框
            using (var pen = new Pen(Color.Gray, 1))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
            }

            // 绘制进度条背景
            var progressBarRect = new Rectangle(10, 10, ClientSize.Width - 20, 20);
            using (var brush = new SolidBrush(Color.LightGray))
            {
                e.Graphics.FillRectangle(brush, progressBarRect);
            }

            // 绘制进度条
            if (Value > 0)
            {
                var progressWidth = (int)(progressBarRect.Width * Percentage / 100);
                var progressRect = new Rectangle(progressBarRect.X, progressBarRect.Y, progressWidth, progressBarRect.Height);
                using (var brush = new SolidBrush(Color.Green))
                {
                    e.Graphics.FillRectangle(brush, progressRect);
                }
            }

            // 绘制百分比文本
            using (var font = new Font("Arial", 10, FontStyle.Bold))
            using (var brush = new SolidBrush(ForeColor))
            {
                var percentageText = $"{Percentage:F1}%";
                var textSize = e.Graphics.MeasureString(percentageText, font);
                var textX = progressBarRect.X + (progressBarRect.Width - textSize.Width) / 2;
                var textY = progressBarRect.Y + (progressBarRect.Height - textSize.Height) / 2;
                e.Graphics.DrawString(percentageText, font, brush, textX, textY);
            }

            // 绘制状态文本
            using (var font = new Font("Arial", 9))
            using (var brush = new SolidBrush(ForeColor))
            {
                var statusRect = new Rectangle(10, 35, ClientSize.Width - 20, 20);
                var statusFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                e.Graphics.DrawString(_status, font, brush, statusRect, statusFormat);
            }
        }

        public void Reset()
        {
            Value = 0;
            Status = "准备就绪";
        }

        public void SetProgress(int value, int maximum, string status)
        {
            Maximum = maximum;
            Value = value;
            Status = status;
        }
    }
}