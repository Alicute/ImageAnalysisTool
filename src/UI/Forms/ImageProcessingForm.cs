using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using ImageAnalysisTool.Core.Processors;
using ImageAnalysisTool.UI.Controls;
using ImageAnalysisTool.UI.Helpers;
using NLog;

namespace ImageAnalysisTool.UI.Forms
{
    /// <summary>
    /// 图像处理窗口 - 像素级图像处理工具
    /// </summary>
    public partial class ImageProcessingForm : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // 图像数据
        private Mat originalImage;
        private Mat targetImage;
        private Mat processedImage;
        private string originalImageName;
        private string targetImageName;

        // 处理器和历史
        private PixelProcessor processor;
        private AsyncImageProcessor asyncProcessor;
        private ProcessingHistory history;
        private SimpleProgressBar progressBar;

        // UI控件 - 图像显示区域
        private PictureBox originalPictureBox;
        private PictureBox targetPictureBox;
        private PictureBox resultPictureBox;
        private Label originalLabel;
        private Label targetLabel;
        private Label resultLabel;

        // UI控件 - 处理控制面板
        private TabControl processingModeTab;
        private TabPage pixelProcessingTab;
        private TabPage regionProcessingTab;  // Phase 2预留
        private TabPage algorithmTuningTab;   // Phase 2预留

        // UI控件 - 像素级处理
        private GroupBox pixelModeGroup;
        private RadioButton directMappingRadio;
        private RadioButton mathOperationRadio;
        private RadioButton lookupTableRadio;
        
        private GroupBox pixelInfoGroup;
        private Label positionLabel;
        private Label originalValueLabel;
        private Label targetValueLabel;
        private Label differenceLabel;

        private GroupBox mathOperationGroup;
        private ComboBox operationComboBox;
        private NumericUpDown valueNumeric;
        private Button applyMathBtn;

        private Button startPixelProcessingBtn;
        private Button batchApplyBtn;
        private Button exportCompleteDataBtn;

        // UI控件 - 历史记录
        private ListBox historyListBox;
        private Button undoBtn;
        private Button redoBtn;
        private Button clearHistoryBtn;

        // UI控件 - 工具栏
        private Button saveBtn;
        private Button exportRulesBtn;
        private Button applyToOriginalBtn;
        private Label statusLabel;

        // 当前选中的像素信息
        private System.Drawing.Point currentPixelPosition;
        private bool isPixelProcessingMode = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="original">原图</param>
        /// <param name="enhanced1">增强图1</param>
        /// <param name="enhanced2">增强图2</param>
        public ImageProcessingForm(Mat original, Mat enhanced1, Mat enhanced2)
        {
            if (original == null)
                throw new ArgumentNullException(nameof(original));

            // 初始化数据
            originalImage = original.Clone();
            
            // 选择目标图（优先使用enhanced2，如果没有则使用enhanced1）
            if (enhanced2 != null && !enhanced2.Empty())
            {
                targetImage = enhanced2.Clone();
                targetImageName = "增强图2";
            }
            else if (enhanced1 != null && !enhanced1.Empty())
            {
                targetImage = enhanced1.Clone();
                targetImageName = "增强图1";
            }
            else
            {
                throw new ArgumentException("至少需要一个增强图作为目标图");
            }

            originalImageName = "原图";
            processedImage = originalImage.Clone();

            // 初始化处理器
            processor = new PixelProcessor();
            asyncProcessor = new AsyncImageProcessor();
            history = new ProcessingHistory();
            history.HistoryChanged += History_HistoryChanged;
            
            // 订阅异步处理器事件
            asyncProcessor.ProgressChanged += OnProgressChanged;
            asyncProcessor.ProcessingCompleted += OnProcessingCompleted;
            asyncProcessor.ProcessingError += OnProcessingError;

            // 初始化UI
            InitializeComponent();
            InitializeImageDisplays();
            InitializeProcessingControls();
            InitializeHistoryControls();
            InitializeToolbar();

            // 启用导出完整数据按钮
            exportCompleteDataBtn.Enabled = true;

            logger.Info($"图像处理窗口初始化完成 - 原图: {originalImage.Size()}, 目标图: {targetImage.Size()}");
        }

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "图像处理工具 - 像素级处理";
            this.Size = new System.Drawing.Size(1200, 800);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new System.Drawing.Size(1000, 600);

            // 创建主布局
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };

            // 设置列宽：左侧40%（图像显示），右侧60%（控制面板）
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            
            // 设置行高：顶部工具栏50px，其余空间给内容
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this.Controls.Add(mainLayout);
        }

        /// <summary>
        /// 初始化图像显示区域
        /// </summary>
        private void InitializeImageDisplays()
        {
            // 创建图像显示面板
            var imagePanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建图像布局
            var imageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };

            imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
            imageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));

            // 原图显示
            originalLabel = new Label
            {
                Text = originalImageName,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightBlue
            };

            originalPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            originalPictureBox.MouseClick += PictureBox_MouseClick;

            var originalContainer = new Panel { Dock = DockStyle.Fill };
            originalContainer.Controls.Add(originalPictureBox);
            originalContainer.Controls.Add(originalLabel);

            // 目标图显示
            targetLabel = new Label
            {
                Text = targetImageName,
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGreen
            };

            targetPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            targetPictureBox.MouseClick += PictureBox_MouseClick;

            var targetContainer = new Panel { Dock = DockStyle.Fill };
            targetContainer.Controls.Add(targetPictureBox);
            targetContainer.Controls.Add(targetLabel);

            // 处理结果显示
            resultLabel = new Label
            {
                Text = "处理结果",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightCoral
            };

            resultPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            resultPictureBox.MouseClick += PictureBox_MouseClick;

            var resultContainer = new Panel { Dock = DockStyle.Fill };
            resultContainer.Controls.Add(resultPictureBox);
            resultContainer.Controls.Add(resultLabel);

            // 添加到布局
            imageLayout.Controls.Add(originalContainer, 0, 0);
            imageLayout.Controls.Add(targetContainer, 0, 1);
            imageLayout.Controls.Add(resultContainer, 0, 2);

            imagePanel.Controls.Add(imageLayout);

            // 添加到主布局
            var mainLayout = (TableLayoutPanel)this.Controls[0];
            mainLayout.Controls.Add(imagePanel, 0, 1);

            // 显示初始图像
            UpdateImageDisplays();
        }

        /// <summary>
        /// 更新图像显示
        /// </summary>
        private void UpdateImageDisplays()
        {
            try
            {
                if (originalImage != null && !originalImage.Empty())
                {
                    originalPictureBox.Image?.Dispose();
                    originalPictureBox.Image = ConvertMatToBitmap(originalImage);
                }

                if (targetImage != null && !targetImage.Empty())
                {
                    targetPictureBox.Image?.Dispose();
                    targetPictureBox.Image = ConvertMatToBitmap(targetImage);
                }

                if (processedImage != null && !processedImage.Empty())
                {
                    resultPictureBox.Image?.Dispose();
                    resultPictureBox.Image = ConvertMatToBitmap(processedImage);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "更新图像显示失败");
            }
        }

        /// <summary>
        /// 将OpenCV Mat转换为Bitmap
        /// </summary>
        private Bitmap ConvertMatToBitmap(Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // 转换为8位用于显示
                Mat display = new Mat();
                if (mat.Type() == MatType.CV_16U)
                {
                    mat.ConvertTo(display, MatType.CV_8U, 255.0 / 65535.0);
                }
                else
                {
                    display = mat.Clone();
                }

                var bitmap = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(display);
                display.Dispose();
                return bitmap;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Mat转Bitmap失败");
                return null;
            }
        }

        /// <summary>
        /// 图像点击事件处理
        /// </summary>
        private void PictureBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (!isPixelProcessingMode) return;

            var pictureBox = sender as PictureBox;
            if (pictureBox?.Image == null) return;

            try
            {
                // 计算实际图像坐标
                var imageSize = new System.Drawing.Size(originalImage.Cols, originalImage.Rows);
                var displaySize = pictureBox.Size;
                
                // 计算缩放比例
                float scaleX = (float)imageSize.Width / displaySize.Width;
                float scaleY = (float)imageSize.Height / displaySize.Height;
                float scale = Math.Max(scaleX, scaleY);

                // 计算图像在PictureBox中的实际显示区域
                var scaledImageSize = new System.Drawing.Size(
                    (int)(imageSize.Width / scale),
                    (int)(imageSize.Height / scale)
                );

                var offset = new System.Drawing.Point(
                    (displaySize.Width - scaledImageSize.Width) / 2,
                    (displaySize.Height - scaledImageSize.Height) / 2
                );

                // 转换点击坐标到图像坐标
                int imageX = (int)((e.X - offset.X) * scale);
                int imageY = (int)((e.Y - offset.Y) * scale);

                // 确保坐标在有效范围内
                imageX = Math.Max(0, Math.Min(imageSize.Width - 1, imageX));
                imageY = Math.Max(0, Math.Min(imageSize.Height - 1, imageY));

                currentPixelPosition = new System.Drawing.Point(imageX, imageY);
                UpdatePixelInfo();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "处理图像点击事件失败");
            }
        }

        /// <summary>
        /// 更新像素信息显示
        /// </summary>
        private void UpdatePixelInfo()
        {
            try
            {
                int x = currentPixelPosition.X;
                int y = currentPixelPosition.Y;

                if (x < 0 || y < 0 || x >= originalImage.Cols || y >= originalImage.Rows)
                    return;

                ushort originalValue = originalImage.At<ushort>(y, x);
                ushort targetValue = targetImage.At<ushort>(y, x);
                int difference = targetValue - originalValue;

                positionLabel.Text = $"位置: ({x}, {y})";
                originalValueLabel.Text = $"原图值: {originalValue}";
                targetValueLabel.Text = $"目标值: {targetValue}";
                differenceLabel.Text = $"差值: {(difference >= 0 ? "+" : "")}{difference}";
            }
            catch (Exception ex)
            {
                logger.Error(ex, "更新像素信息失败");
            }
        }

        /// <summary>
        /// 历史记录变化事件处理
        /// </summary>
        private void History_HistoryChanged(object sender, EventArgs e)
        {
            UpdateHistoryDisplay();
            UpdateToolbarButtons();
        }

        /// <summary>
        /// 更新历史记录显示
        /// </summary>
        private void UpdateHistoryDisplay()
        {
            try
            {
                historyListBox.Items.Clear();
                var historyList = history.GetHistory();
                
                for (int i = 0; i < historyList.Count; i++)
                {
                    var rule = historyList[i];
                    var item = $"{i + 1}. {rule.RuleName} ({rule.Type}) - {rule.CreateTime:HH:mm:ss}";
                    historyListBox.Items.Add(item);
                }

                // 选中最后一项
                if (historyListBox.Items.Count > 0)
                {
                    historyListBox.SelectedIndex = historyListBox.Items.Count - 1;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "更新历史记录显示失败");
            }
        }

        /// <summary>
        /// 更新工具栏按钮状态
        /// </summary>
        private void UpdateToolbarButtons()
        {
            undoBtn.Enabled = history.CanUndo;
            redoBtn.Enabled = history.CanRedo;
            clearHistoryBtn.Enabled = history.Count > 0;
            saveBtn.Enabled = processedImage != null;
            exportRulesBtn.Enabled = history.Count > 0;
            applyToOriginalBtn.Enabled = processedImage != null;
        }

        /// <summary>
        /// 初始化处理控制面板
        /// </summary>
        private void InitializeProcessingControls()
        {
            // 创建控制面板
            var controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            // 创建Tab控件
            processingModeTab = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // 像素级处理Tab
            pixelProcessingTab = new TabPage("像素级处理");
            InitializePixelProcessingTab();
            processingModeTab.TabPages.Add(pixelProcessingTab);

            // 区域处理Tab
            regionProcessingTab = new TabPage("区域处理");
            var openRegionProcessingBtn = new Button
            {
                Text = "打开高级区域处理工具",
                Dock = DockStyle.None,
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(200, 50),
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.CornflowerBlue,
                ForeColor = Color.White
            };
            openRegionProcessingBtn.Click += OpenRegionProcessingBtn_Click;
            regionProcessingTab.Controls.Add(openRegionProcessingBtn);
            processingModeTab.TabPages.Add(regionProcessingTab);

            algorithmTuningTab = new TabPage("算法调参 (Phase 2)");
            algorithmTuningTab.Controls.Add(new Label
            {
                Text = "算法调参功能将在Phase 2实现",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray
            });
            processingModeTab.TabPages.Add(algorithmTuningTab);

            controlPanel.Controls.Add(processingModeTab);

            // 添加到主布局
            var mainLayout = (TableLayoutPanel)this.Controls[0];
            mainLayout.Controls.Add(controlPanel, 1, 1);
        }

        /// <summary>
        /// 初始化像素级处理Tab
        /// </summary>
        private void InitializePixelProcessingTab()
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F)); // 处理模式选择
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F)); // 像素信息
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F)); // 数学运算
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // 操作按钮

            // 1. 处理模式选择
            pixelModeGroup = new GroupBox
            {
                Text = "处理模式选择",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            directMappingRadio = new RadioButton
            {
                Text = "直接映射 (原图值→目标图值)",
                Location = new System.Drawing.Point(10, 25),
                Size = new System.Drawing.Size(250, 20),
                Checked = true
            };

            mathOperationRadio = new RadioButton
            {
                Text = "数学运算 (加减乘除)",
                Location = new System.Drawing.Point(10, 50),
                Size = new System.Drawing.Size(200, 20)
            };

            lookupTableRadio = new RadioButton
            {
                Text = "查找表映射",
                Location = new System.Drawing.Point(10, 75),
                Size = new System.Drawing.Size(150, 20)
            };

            pixelModeGroup.Controls.AddRange(new Control[] {
                directMappingRadio, mathOperationRadio, lookupTableRadio
            });

            // 2. 像素信息显示
            pixelInfoGroup = new GroupBox
            {
                Text = "当前像素信息",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            positionLabel = new Label
            {
                Text = "位置: 点击图像选择像素",
                Location = new System.Drawing.Point(10, 25),
                Size = new System.Drawing.Size(200, 20)
            };

            originalValueLabel = new Label
            {
                Text = "原图值: --",
                Location = new System.Drawing.Point(10, 45),
                Size = new System.Drawing.Size(150, 20)
            };

            targetValueLabel = new Label
            {
                Text = "目标值: --",
                Location = new System.Drawing.Point(10, 65),
                Size = new System.Drawing.Size(150, 20)
            };

            differenceLabel = new Label
            {
                Text = "差值: --",
                Location = new System.Drawing.Point(170, 45),
                Size = new System.Drawing.Size(100, 20)
            };

            pixelInfoGroup.Controls.AddRange(new Control[] {
                positionLabel, originalValueLabel, targetValueLabel, differenceLabel
            });

            // 3. 数学运算控件
            mathOperationGroup = new GroupBox
            {
                Text = "数学运算设置",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var operationLabel = new Label
            {
                Text = "运算类型:",
                Location = new System.Drawing.Point(10, 25),
                Size = new System.Drawing.Size(80, 20)
            };

            operationComboBox = new ComboBox
            {
                Location = new System.Drawing.Point(100, 23),
                Size = new System.Drawing.Size(120, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            operationComboBox.Items.AddRange(new object[] {
                "加法", "减法", "乘法", "除法", "幂运算", "对数", "Gamma校正"
            });
            operationComboBox.SelectedIndex = 0;

            var valueLabel = new Label
            {
                Text = "数值:",
                Location = new System.Drawing.Point(10, 55),
                Size = new System.Drawing.Size(50, 20)
            };

            valueNumeric = new NumericUpDown
            {
                Location = new System.Drawing.Point(70, 53),
                Size = new System.Drawing.Size(100, 25),
                DecimalPlaces = 2,
                Minimum = -10000,
                Maximum = 10000,
                Value = 1
            };

            applyMathBtn = new Button
            {
                Text = "应用运算",
                Location = new System.Drawing.Point(10, 85),
                Size = new System.Drawing.Size(100, 30),
                BackColor = Color.LightBlue
            };
            applyMathBtn.Click += ApplyMathBtn_Click;

            mathOperationGroup.Controls.AddRange(new Control[] {
                operationLabel, operationComboBox, valueLabel, valueNumeric, applyMathBtn
            });

            // 4. 操作按钮和进度条
            var buttonPanel = new Panel { Dock = DockStyle.Fill };

            startPixelProcessingBtn = new Button
            {
                Text = "开始逐像素处理",
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(150, 35),
                BackColor = Color.LightGreen
            };
            startPixelProcessingBtn.Click += StartPixelProcessingBtn_Click;

            batchApplyBtn = new Button
            {
                Text = "批量应用规律",
                Location = new System.Drawing.Point(170, 10),
                Size = new System.Drawing.Size(120, 35),
                BackColor = Color.Orange,
                Enabled = false
            };
            batchApplyBtn.Click += BatchApplyBtn_Click;

            exportCompleteDataBtn = new Button
            {
                Text = "导出完整数据",
                Location = new System.Drawing.Point(300, 10),
                Size = new System.Drawing.Size(120, 35),
                BackColor = Color.LightBlue,
                Enabled = false
            };
            exportCompleteDataBtn.Click += ExportCompleteDataBtn_Click;
            
            // 添加进度条
            progressBar = new SimpleProgressBar
            {
                Location = new System.Drawing.Point(10, 55),
                Visible = false
            };

            buttonPanel.Controls.AddRange(new Control[] {
                startPixelProcessingBtn, batchApplyBtn, exportCompleteDataBtn, progressBar
            });

            // 添加到布局
            layout.Controls.Add(pixelModeGroup, 0, 0);
            layout.Controls.Add(pixelInfoGroup, 0, 1);
            layout.Controls.Add(mathOperationGroup, 0, 2);
            layout.Controls.Add(buttonPanel, 0, 3);

            pixelProcessingTab.Controls.Add(layout);
        }

        /// <summary>
        /// 初始化历史记录控件
        /// </summary>
        private void InitializeHistoryControls()
        {
            // 历史记录面板将添加到处理控制面板的底部
            var controlPanel = ((TableLayoutPanel)this.Controls[0]).GetControlFromPosition(1, 1) as Panel;
            var processingTab = controlPanel.Controls[0] as TabControl;

            // 在像素处理Tab中添加历史记录区域
            var pixelTab = processingTab.TabPages[0];
            var existingLayout = pixelTab.Controls[0] as TableLayoutPanel;

            // 修改布局以添加历史记录区域
            existingLayout.RowCount = 5;
            existingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F)); // 历史记录区域

            // 创建历史记录组
            var historyGroup = new GroupBox
            {
                Text = "处理历史记录",
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            var historyLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };

            historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            historyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));

            // 历史记录列表
            historyListBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 8),
                SelectionMode = SelectionMode.One
            };
            historyListBox.DoubleClick += HistoryListBox_DoubleClick;

            // 历史记录按钮
            var historyButtonPanel = new Panel { Dock = DockStyle.Fill };

            undoBtn = new Button
            {
                Text = "撤销",
                Location = new System.Drawing.Point(5, 5),
                Size = new System.Drawing.Size(60, 25),
                Enabled = false
            };
            undoBtn.Click += UndoBtn_Click;

            redoBtn = new Button
            {
                Text = "重做",
                Location = new System.Drawing.Point(70, 5),
                Size = new System.Drawing.Size(60, 25),
                Enabled = false
            };
            redoBtn.Click += RedoBtn_Click;

            clearHistoryBtn = new Button
            {
                Text = "清空",
                Location = new System.Drawing.Point(5, 35),
                Size = new System.Drawing.Size(60, 25),
                Enabled = false
            };
            clearHistoryBtn.Click += ClearHistoryBtn_Click;

            historyButtonPanel.Controls.AddRange(new Control[] {
                undoBtn, redoBtn, clearHistoryBtn
            });

            // 添加到历史记录布局
            historyLayout.Controls.Add(historyListBox, 0, 0);
            historyLayout.SetRowSpan(historyListBox, 2);
            historyLayout.Controls.Add(historyButtonPanel, 1, 0);

            historyGroup.Controls.Add(historyLayout);

            // 添加到主布局
            existingLayout.Controls.Add(historyGroup, 0, 4);
        }

        /// <summary>
        /// 初始化工具栏
        /// </summary>
        private void InitializeToolbar()
        {
            var mainLayout = (TableLayoutPanel)this.Controls[0];

            // 创建工具栏面板
            var toolbarPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray,
                Padding = new Padding(5)
            };

            var toolbarLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            // 保存按钮
            saveBtn = new Button
            {
                Text = "保存结果",
                Size = new System.Drawing.Size(80, 30),
                Margin = new Padding(5),
                BackColor = Color.LightBlue,
                Enabled = false
            };
            saveBtn.Click += SaveBtn_Click;

            // 导出规律按钮
            exportRulesBtn = new Button
            {
                Text = "导出规律",
                Size = new System.Drawing.Size(80, 30),
                Margin = new Padding(5),
                BackColor = Color.LightGreen,
                Enabled = false
            };
            exportRulesBtn.Click += ExportRulesBtn_Click;

            // 应用到原图按钮
            applyToOriginalBtn = new Button
            {
                Text = "应用到原图",
                Size = new System.Drawing.Size(100, 30),
                Margin = new Padding(5),
                BackColor = Color.Orange,
                Enabled = false
            };
            applyToOriginalBtn.Click += ApplyToOriginalBtn_Click;

            // 添加分隔符
            var separator = new Label
            {
                Text = "|",
                Size = new System.Drawing.Size(10, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                Margin = new Padding(5)
            };

            // 状态标签
            statusLabel = new Label
            {
                Text = "就绪 - 点击图像选择像素开始处理",
                AutoSize = true,
                Margin = new Padding(10, 8, 5, 5),
                ForeColor = Color.DarkBlue
            };

            toolbarLayout.Controls.AddRange(new Control[] {
                saveBtn, exportRulesBtn, applyToOriginalBtn, separator, statusLabel
            });

            toolbarPanel.Controls.Add(toolbarLayout);

            // 添加到主布局，跨两列
            mainLayout.Controls.Add(toolbarPanel, 0, 0);
            mainLayout.SetColumnSpan(toolbarPanel, 2);
        }

        #region 事件处理方法

        /// <summary>
        /// 数学运算应用按钮点击事件
        /// </summary>
        private void ApplyMathBtn_Click(object sender, EventArgs e)
        {
            try
            {
                if (!mathOperationRadio.Checked)
                {
                    MessageBox.Show("请先选择数学运算模式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // 获取运算参数
                var operationText = operationComboBox.SelectedItem?.ToString();
                var operationType = GetMathOperationType(operationText);
                var value = (double)valueNumeric.Value;

                // 应用数学运算
                var newProcessedImage = processor.MathOperation(processedImage, operationType, value);

                // 更新处理结果
                processedImage.Dispose();
                processedImage = newProcessedImage;

                // 创建并记录处理规则
                var rule = ProcessingRule.CreateMathOperation($"{operationText}_{value}", operationType, value);
                history.AddStep(rule);

                // 更新显示
                UpdateImageDisplays();

                logger.Info($"应用数学运算: {operationText} {value}");
                MessageBox.Show($"数学运算应用成功: {operationText} {value}", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "应用数学运算失败");
                MessageBox.Show($"应用数学运算失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出完整数据按钮点击事件
        /// </summary>
        private async void ExportCompleteDataBtn_Click(object sender, EventArgs e)
        {
            try
            {
                if (originalImage == null || targetImage == null)
                {
                    MessageBox.Show("请先加载原图和目标图", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    "导出完整数据将生成包含750万行像素详细信息的文件，\n" +
                    "文件大小约400-500MB，可能需要30-60秒时间。\n\n" +
                    "是否继续导出？",
                    "确认导出完整数据",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 显示进度
                    exportCompleteDataBtn.Enabled = false;
                    statusLabel.Text = "正在导出完整750万行数据...";
                    statusLabel.Refresh();

                    // 选择导出格式
                    var formatResult = MessageBox.Show(
                        "请选择导出格式：\n\n" +
                        "是(Y) - TXT格式（适合AI阅读）\n" +
                        "否(N) - CSV格式（适合数据分析）",
                        "选择导出格式",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    string format = formatResult == DialogResult.Yes ? "txt" : "csv";

                    // 异步导出
                    string dataFile = await asyncProcessor.ExportCompletePixelDataAsync(originalImage, targetImage, format);

                    // 恢复UI
                    exportCompleteDataBtn.Enabled = true;
                    statusLabel.Text = "完整数据导出完成";
                    UpdateImageDisplays();

                    logger.Info($"完整数据导出完成: {dataFile}");
                    MessageBox.Show($"完整数据导出成功！\n\n文件位置: {dataFile}\n文件大小: {new FileInfo(dataFile).Length / 1024 / 1024:F1}MB", 
                        "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                exportCompleteDataBtn.Enabled = true;
                statusLabel.Text = "导出失败";
                logger.Error(ex, "导出完整数据失败");
                MessageBox.Show($"导出完整数据失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 开始逐像素处理按钮点击事件
        /// </summary>
        private async void StartPixelProcessingBtn_Click(object sender, EventArgs e)
        {
            try
            {
                if (directMappingRadio.Checked)
                {
                    // 直接映射模式
                    var result = MessageBox.Show(
                        "直接映射将分析原图和目标图的像素对应关系，并应用到处理结果。\n\n" +
                        "这个过程将进行逐像素处理，可能需要15-20秒时间，是否继续？",
                        "确认直接映射",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
                        // 显示进度条
                        progressBar.Reset();
                        progressBar.Visible = true;
                        startPixelProcessingBtn.Enabled = false;
                        statusLabel.Text = "正在分析像素映射关系...";
                        statusLabel.Refresh();

                        // 使用异步处理器进行直接映射
                        var newProcessedImage = await asyncProcessor.DirectMappingAsync(originalImage, targetImage);

                        // 更新处理结果
                        processedImage.Dispose();
                        processedImage = newProcessedImage;

                        // 更新显示
                        UpdateImageDisplays();

                        // 隐藏进度条
                        progressBar.Visible = false;
                        startPixelProcessingBtn.Enabled = true;
                        exportCompleteDataBtn.Enabled = true;
                        statusLabel.Text = "直接映射处理完成 - 可查看处理结果或导出完整数据";

                        logger.Info("直接映射处理完成");
                        MessageBox.Show("直接映射处理完成！报告已自动导出到桌面。", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    // 启用像素点击模式
                    isPixelProcessingMode = !isPixelProcessingMode;

                    if (isPixelProcessingMode)
                    {
                        startPixelProcessingBtn.Text = "停止像素处理";
                        startPixelProcessingBtn.BackColor = Color.LightCoral;
                        MessageBox.Show("像素处理模式已启用，点击图像选择要处理的像素", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        startPixelProcessingBtn.Text = "开始逐像素处理";
                        startPixelProcessingBtn.BackColor = Color.LightGreen;
                    }
                }
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                startPixelProcessingBtn.Enabled = true;
                logger.Error(ex, "开始像素处理失败");
                MessageBox.Show($"开始像素处理失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 批量应用规律按钮点击事件
        /// </summary>
        private void BatchApplyBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var historyList = history.GetHistory();
                if (historyList.Count == 0)
                {
                    MessageBox.Show("没有可应用的处理规律", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"将应用最近的 {historyList.Count} 个处理规律到原图，是否继续？",
                    "确认批量应用",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    this.Cursor = Cursors.WaitCursor;

                    // 从原图开始重新应用所有规律
                    var tempImage = originalImage.Clone();

                    foreach (var rule in historyList)
                    {
                        var newTempImage = processor.ApplyProcessingRule(tempImage, rule);
                        tempImage.Dispose();
                        tempImage = newTempImage;
                    }

                    // 更新处理结果
                    processedImage.Dispose();
                    processedImage = tempImage;

                    // 更新显示
                    UpdateImageDisplays();

                    this.Cursor = Cursors.Default;

                    logger.Info($"批量应用 {historyList.Count} 个处理规律完成");
                    MessageBox.Show($"批量应用 {historyList.Count} 个处理规律完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                logger.Error(ex, "批量应用规律失败");
                MessageBox.Show($"批量应用规律失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 撤销按钮点击事件
        /// </summary>
        private void UndoBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var undoRule = history.Undo();
                if (undoRule != null)
                {
                    // 重新应用所有剩余的规律
                    RebuildProcessedImage();
                    logger.Info($"撤销操作: {undoRule.RuleName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "撤销操作失败");
                MessageBox.Show($"撤销操作失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 重做按钮点击事件
        /// </summary>
        private void RedoBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var redoRule = history.Redo();
                if (redoRule != null)
                {
                    // 重新应用所有规律
                    RebuildProcessedImage();
                    logger.Info($"重做操作: {redoRule.RuleName}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "重做操作失败");
                MessageBox.Show($"重做操作失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 清空历史记录按钮点击事件
        /// </summary>
        private void ClearHistoryBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要清空所有处理历史记录吗？此操作不可撤销。",
                    "确认清空",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    history.Clear();
                    logger.Info("清空处理历史记录");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "清空历史记录失败");
                MessageBox.Show($"清空历史记录失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 历史记录列表双击事件 - 打开对应的报告文件
        /// </summary>
        private void HistoryListBox_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (historyListBox.SelectedIndex >= 0)
                {
                    var selectedItem = historyListBox.SelectedItem;
                    if (selectedItem != null)
                    {
                        // 尝试从项目文本中提取时间戳
                        string itemText = selectedItem.ToString();
                        
                        // 格式通常是: "12:34:56 直接映射"
                        // 我们需要提取完整的时间戳来匹配文件名
                        LogFileHelper.OpenLatestLogFile();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "双击历史记录打开文件失败");
                MessageBox.Show($"打开报告文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 保存结果按钮点击事件
        /// </summary>
        private void SaveBtn_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "TIFF图像|*.tiff;*.tif|PNG图像|*.png|所有文件|*.*";
                    dialog.DefaultExt = "tiff";
                    dialog.FileName = $"处理结果_{DateTime.Now:yyyyMMdd_HHmmss}";

                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        processedImage.SaveImage(dialog.FileName);
                        logger.Info($"保存处理结果: {dialog.FileName}");
                        MessageBox.Show("处理结果保存成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "保存处理结果失败");
                MessageBox.Show($"保存处理结果失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 导出规律按钮点击事件 - 现在改为打开最新的日志文件
        /// </summary>
        private void ExportRulesBtn_Click(object sender, EventArgs e)
        {
            try
            {
                // 使用LogFileHelper打开最新的日志文件
                LogFileHelper.OpenLatestLogFile();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "打开日志文件失败");
                MessageBox.Show($"打开日志文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 应用到原图按钮点击事件
        /// </summary>
        private void ApplyToOriginalBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "将当前处理结果应用到原图，这将替换当前的处理结果。是否继续？",
                    "确认应用",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 将处理结果复制到原图
                    originalImage.Dispose();
                    originalImage = processedImage.Clone();

                    // 重置处理结果为新的原图
                    processedImage.Dispose();
                    processedImage = originalImage.Clone();

                    // 清空历史记录
                    history.Clear();

                    // 更新显示
                    UpdateImageDisplays();

                    logger.Info("应用处理结果到原图");
                    MessageBox.Show("处理结果已应用到原图！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "应用到原图失败");
                MessageBox.Show($"应用到原图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 打开区域处理窗口
        /// </summary>
        private void OpenRegionProcessingBtn_Click(object sender, EventArgs e)
        {
            try
            {
                logger.Info("准备打开区域处理窗口...");
                using (var form = new RegionProcessingForm(this.originalImage))
                {
                    form.ShowDialog(this);
                }
            }
            catch(Exception ex)
            {
                logger.Error(ex, "打开区域处理窗口失败");
                MessageBox.Show($"打开区域处理窗口失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取数学运算类型
        /// </summary>
        private MathOperationType GetMathOperationType(string operationText)
        {
            return operationText switch
            {
                "加法" => MathOperationType.Add,
                "减法" => MathOperationType.Subtract,
                "乘法" => MathOperationType.Multiply,
                "除法" => MathOperationType.Divide,
                "幂运算" => MathOperationType.Power,
                "对数" => MathOperationType.Log,
                "Gamma校正" => MathOperationType.Gamma,
                _ => MathOperationType.Add
            };
        }

        /// <summary>
        /// 重建处理后的图像（用于撤销/重做）
        /// </summary>
        private void RebuildProcessedImage()
        {
            try
            {
                // 从原图开始重新应用所有有效的规律
                processedImage.Dispose();
                processedImage = originalImage.Clone();

                var activeRules = history.GetUndoStack().ToList();
                activeRules.Reverse();

                foreach (var rule in activeRules)
                {
                    var newProcessedImage = processor.ApplyProcessingRule(processedImage, rule);
                    processedImage.Dispose();
                    processedImage = newProcessedImage;
                }

                // 更新显示
                UpdateImageDisplays();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "重建处理图像失败");
                throw;
            }
        }

        #endregion

        #region 异步处理事件处理方法

        /// <summary>
        /// 进度更新事件处理
        /// </summary>
        private void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            if (progressBar.InvokeRequired)
            {
                progressBar.Invoke(new Action(() => 
                {
                    progressBar.SetProgress(e.Processed, e.Total, e.Status);
                }));
            }
            else
            {
                progressBar.SetProgress(e.Processed, e.Total, e.Status);
            }
        }

        /// <summary>
        /// 处理完成事件处理
        /// </summary>
        private void OnProcessingCompleted(object sender, ProcessingCompleteEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => 
                {
                    // 显示处理结果图像
                    if (e.ResultImage != null && !e.ResultImage.Empty())
                    {
                        processedImage = e.ResultImage.Clone();
                        resultPictureBox.Image = ConvertMatToBitmap(processedImage);
                        resultPictureBox.Refresh();
                        
                        // 更新图像显示
                        UpdateImageDisplays();
                        
                        statusLabel.Text = "处理完成！报告已自动导出到桌面。";
                        logger.Info("异步处理完成，处理结果图像已更新");
                        
                        // 注意：这里可以添加切换到结果Tab的逻辑
                    }
                    else
                    {
                        statusLabel.Text = "处理完成！但无法显示结果图像。";
                        logger.Warn("异步处理完成，但结果图像为空");
                    }
                }));
            }
            else
            {
                // 显示处理结果图像
                if (e.ResultImage != null && !e.ResultImage.Empty())
                {
                    processedImage = e.ResultImage.Clone();
                    resultPictureBox.Image = ConvertMatToBitmap(processedImage);
                    resultPictureBox.Refresh();
                    
                    // 更新图像显示
                    UpdateImageDisplays();
                    
                    statusLabel.Text = "处理完成！报告已自动导出到桌面。";
                    logger.Info("异步处理完成，处理结果图像已更新");
                    
                    // 注意：这里可以添加切换到结果Tab的逻辑
                }
                else
                {
                    statusLabel.Text = "处理完成！但无法显示结果图像。";
                    logger.Warn("异步处理完成，但结果图像为空");
                }
            }
        }

        /// <summary>
        /// 处理错误事件处理
        /// </summary>
        private void OnProcessingError(object sender, ProcessingErrorEventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => 
                {
                    MessageBox.Show($"处理失败: {e.Error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    logger.Error(e.Error, "异步处理失败");
                }));
            }
            else
            {
                MessageBox.Show($"处理失败: {e.Error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                logger.Error(e.Error, "异步处理失败");
            }
        }

        #endregion

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                originalImage?.Dispose();
                targetImage?.Dispose();
                processedImage?.Dispose();

                originalPictureBox.Image?.Dispose();
                targetPictureBox.Image?.Dispose();
                resultPictureBox.Image?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "清理资源失败");
            }

            base.OnFormClosed(e);
        }
    }
}
