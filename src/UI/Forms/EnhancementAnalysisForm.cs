using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using FellowOakDicom;
using ImageAnalysisTool.Core.Analyzers;
using ImageAnalysisTool.Core.Models;
using ImageAnalysisTool.Core.Enhancers;

namespace ImageAnalysisTool.UI.Forms
{


    /// <summary>
    /// 图像增强分析界面（优化版：支持双增强图像对比）
    /// </summary>
    public partial class EnhancementAnalysisForm : Form
    {
        private ImageEnhancementAnalyzer analyzer;
        private Mat originalImage;
        private Mat enhancedImage;
        private ImageEnhancementAnalyzer.AnalysisResult analysisResult;

        // UI控件
        private ScrollableControl mainScrollPanel;  // 主滚动面板
        private TableLayoutPanel mainLayout;
        private Panel topControlPanel;              // 顶部控制面板
        private Panel imagePanel;
        private PictureBox originalPictureBox;
        private PictureBox enhancedPictureBox;
        private Button loadOriginalBtn;
        private Button loadEnhancedBtn;
        private Button analyzeBtn;
        private Button showROIButton;

        private Chart histogramChart;
        private Chart mappingChart;
        private RichTextBox resultTextBox;
        private RichTextBox grayValueAnalysisTextBox;
        private Label histogramLabel;               // 直方图说明标签（新增）
        private Label mappingLabel;                 // 映射图说明标签（新增）

        public EnhancementAnalysisForm()
        {
            analyzer = new ImageEnhancementAnalyzer();
            InitializeComponent();

            // 初始化完成后的设置
            InitializeAfterComponents();
        }

        /// <summary>
        /// 组件初始化完成后的额外设置
        /// </summary>
        private void InitializeAfterComponents()
        {
            try
            {
                // 设置窗口图标和其他属性
                this.MinimumSize = new System.Drawing.Size(1200, 800);
                this.WindowState = FormWindowState.Maximized;  // 默认最大化显示

                // 设置图表的初始状态
                if (histogramChart != null)
                {
                    histogramChart.BackColor = Color.White;
                    histogramChart.BorderlineColor = Color.Gray;
                    histogramChart.BorderlineWidth = 1;
                }

                if (mappingChart != null)
                {
                    mappingChart.BackColor = Color.White;
                    mappingChart.BorderlineColor = Color.Gray;
                    mappingChart.BorderlineWidth = 1;
                }

                // 设置文本框的初始内容
                if (resultTextBox != null)
                {
                    resultTextBox.Text = "欢迎使用图像增强算法分析工具（优化版）\n\n" +
                                        "功能说明：\n" +
                                        "1. 点击'加载原图'加载原始图像\n" +
                                        "2. 点击'加载增强图1'加载第一个增强图像\n" +
                                        "3. 点击'分析图1'进行详细分析\n" +
                                        "4. 查看下方的直方图对比和像素映射关系\n" +
                                        "5. 滚动查看完整的分析结果\n\n" +
                                        "图表说明：\n" +
                                        "• 直方图：显示图像亮度分布变化\n" +
                                        "• 映射图：显示每个像素的亮度变化关系（已修复16位支持）\n" +
                                        "• 支持完整的16位灰度值范围(0-65535)\n";
                }

                if (grayValueAnalysisTextBox != null)
                {
                    grayValueAnalysisTextBox.Text = "灰度值分析结果将在这里显示...\n\n" +
                                                   "包含内容：\n" +
                                                   "• 原图和增强图的统计信息\n" +
                                                   "• 边缘区域分析\n" +
                                                   "• 中心区域分析\n" +
                                                   "• ROI区域详细分析\n";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"界面初始化警告: {ex.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "图像增强算法分析工具（优化版）";
            this.Size = new System.Drawing.Size(1600, 1000);  // 增大窗口
            this.StartPosition = FormStartPosition.CenterScreen;

            // 创建主滚动面板
            mainScrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,  // 启用滚动
                BackColor = Color.White
            };

            // 创建主布局（放在滚动面板内）
            mainLayout = new TableLayoutPanel
            {
                Location = new System.Drawing.Point(0, 0),
                Size = new System.Drawing.Size(1580, 1200),  // 固定大小，允许滚动
                ColumnCount = 2,  // 2列：原图、增强图
                RowCount = 3      // 3行：顶部控制、图像、结果
            };

            // 设置列和行的大小
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 原图
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 增强图1
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 增强图2
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));         // 顶部控制面板
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));          // 图像显示区域
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));          // 结果显示区域

            // 创建顶部控制面板
            CreateTopControlPanel();

            // 创建图像显示面板
            CreateImagePanel();

            // 创建结果面板
            CreateResultPanel();

            mainScrollPanel.Controls.Add(mainLayout);
            this.Controls.Add(mainScrollPanel);
        }

        /// <summary>
        /// 创建顶部控制面板
        /// </summary>
        private void CreateTopControlPanel()
        {
            topControlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray,
                Padding = new Padding(10)
            };

            var buttonLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            // 创建按钮
            loadOriginalBtn = new Button
            {
                Text = "加载原图",
                Size = new System.Drawing.Size(100, 35),
                Margin = new Padding(5)
            };
            loadOriginalBtn.Click += LoadOriginalBtn_Click;

            loadEnhancedBtn = new Button
            {
                Text = "加载增强图",
                Size = new System.Drawing.Size(120, 35),
                Margin = new Padding(5)
            };
            loadEnhancedBtn.Click += LoadEnhancedBtn_Click;

            analyzeBtn = new Button
            {
                Text = "开始分析",
                Size = new System.Drawing.Size(100, 35),
                Margin = new Padding(5),
                BackColor = Color.LightBlue,
                Enabled = false
            };
            analyzeBtn.Click += AnalyzeBtn_Click;

            showROIButton = new Button
            {
                Text = "显示ROI区域",
                Size = new System.Drawing.Size(120, 35),
                Margin = new Padding(5)
            };
            showROIButton.Click += ShowROIButton_Click;

            // 添加按钮到布局
            buttonLayout.Controls.AddRange(new Control[] {
                loadOriginalBtn, loadEnhancedBtn, analyzeBtn, showROIButton
            });

            topControlPanel.Controls.Add(buttonLayout);



            // 添加到主布局，跨2列
            mainLayout.Controls.Add(topControlPanel, 0, 0);
            mainLayout.SetColumnSpan(topControlPanel, 2);
        }

        /// <summary>
        /// 创建图像显示面板
        /// </summary>
        private void CreateImagePanel()
        {
            // 原图显示
            var originalPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            var originalLabel = new Label
            {
                Text = "原图",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightBlue
            };
            originalPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            originalPanel.Controls.Add(originalPictureBox);
            originalPanel.Controls.Add(originalLabel);

            // 增强图显示
            var enhancedPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            var enhancedLabel = new Label
            {
                Text = "增强图",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGreen
            };
            enhancedPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White
            };
            enhancedPanel.Controls.Add(enhancedPictureBox);
            enhancedPanel.Controls.Add(enhancedLabel);

            // 添加到主布局 - 只使用前两列
            mainLayout.Controls.Add(originalPanel, 0, 1);
            mainLayout.Controls.Add(enhancedPanel, 1, 1);
        }

        private void CreateImagePanel_Old()
        {
            imagePanel = new Panel { Dock = DockStyle.Fill };

            var imageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            // 原图显示
            var originalPanel = new Panel { Dock = DockStyle.Fill };
            originalPanel.Controls.Add(new Label { Text = "原图", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter });
            originalPictureBox = new PictureBox 
            { 
                Dock = DockStyle.Fill, 
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            originalPanel.Controls.Add(originalPictureBox);

            // 增强图显示
            var enhancedPanel = new Panel { Dock = DockStyle.Fill };
            enhancedPanel.Controls.Add(new Label { Text = "增强后", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter });
            enhancedPictureBox = new PictureBox 
            { 
                Dock = DockStyle.Fill, 
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };
            enhancedPanel.Controls.Add(enhancedPictureBox);

            imageLayout.Controls.Add(originalPanel, 0, 0);
            imageLayout.Controls.Add(enhancedPanel, 1, 0);
            imagePanel.Controls.Add(imageLayout);
        }



        /// <summary>
        /// 创建结果面板（优化版：添加图表说明和改善布局）
        /// </summary>
        private void CreateResultPanel()
        {
            // 创建结果面板，跨3列
            var resultMainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            var resultLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3  // 增加一行用于图表标题
            };

            // 设置列宽
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); // 文本结果
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // 直方图
            resultLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // 映射图
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));       // 上半部分
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));      // 图表标题行
            resultLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));       // 下半部分

            // 创建文本结果显示
            resultTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.White,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            resultLayout.Controls.Add(resultTextBox, 0, 0);
            resultLayout.SetRowSpan(resultTextBox, 3);  // 跨3行

            // 创建直方图
            histogramChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            var histogramArea = new ChartArea("HistogramArea")
            {
                AxisX = { Title = "灰度值 (0-65535)", TitleFont = new Font("Arial", 10) },
                AxisY = { Title = "像素数量", TitleFont = new Font("Arial", 10) }
            };
            histogramChart.ChartAreas.Add(histogramArea);
            histogramChart.Titles.Add(new Title("直方图对比", Docking.Top, new Font("Arial", 12, FontStyle.Bold), Color.Black));
            resultLayout.Controls.Add(histogramChart, 1, 0);

            // 直方图说明标签
            histogramLabel = new Label
            {
                Text = "X轴：灰度值(0-65535) | Y轴：像素数量\n蓝色：原图 | 红色：增强图",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightBlue,
                Font = new Font("Arial", 8),
                BorderStyle = BorderStyle.FixedSingle
            };
            resultLayout.Controls.Add(histogramLabel, 1, 1);

            // 创建映射图
            mappingChart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };
            var mappingArea = new ChartArea("MappingArea")
            {
                AxisX = { Title = "原始灰度值", TitleFont = new Font("Arial", 10) },
                AxisY = { Title = "增强后灰度值", TitleFont = new Font("Arial", 10) }
            };
            mappingChart.ChartAreas.Add(mappingArea);
            mappingChart.Titles.Add(new Title("像素值映射关系（16位支持）", Docking.Top, new Font("Arial", 12, FontStyle.Bold), Color.Black));
            resultLayout.Controls.Add(mappingChart, 2, 0);

            // 映射图说明标签
            mappingLabel = new Label
            {
                Text = "X轴：原始灰度值 | Y轴：增强后灰度值\n显示每个像素的亮度变化映射关系\n支持16位图像完整范围(0-65535)\n对角线表示无变化",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGreen,
                Font = new Font("Arial", 8),
                BorderStyle = BorderStyle.FixedSingle
            };
            resultLayout.Controls.Add(mappingLabel, 2, 1);

            // 创建灰度值分析文本框
            grayValueAnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 8),
                BackColor = Color.LightYellow,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            resultLayout.Controls.Add(grayValueAnalysisTextBox, 1, 2);
            resultLayout.SetColumnSpan(grayValueAnalysisTextBox, 2);

            resultMainPanel.Controls.Add(resultLayout);

            // 添加到主布局，跨2列
            mainLayout.Controls.Add(resultMainPanel, 0, 2);
            mainLayout.SetColumnSpan(resultMainPanel, 2);
        }

        private void LoadOriginalBtn_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.dcm;*.dic;*.acr;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        originalImage?.Dispose();
                        originalImage = LoadImageFile(dialog.FileName);

                        originalPictureBox.Image = ConvertMatToBitmap(originalImage);
                        CheckCanAnalyze();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载原图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void LoadEnhancedBtn_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.dcm;*.dic;*.acr;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        enhancedImage?.Dispose();
                        enhancedImage = LoadImageFile(dialog.FileName);

                        enhancedPictureBox.Image = ConvertMatToBitmap(enhancedImage);
                        CheckCanAnalyze();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载增强图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CheckCanAnalyze()
        {
            analyzeBtn.Enabled = originalImage != null && enhancedImage != null &&
                                originalImage.Size() == enhancedImage.Size();
            showROIButton.Enabled = originalImage != null;
        }

        private void AnalyzeBtn_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                analyzeBtn.Text = "分析中...";
                analyzeBtn.Enabled = false;

                // 执行传统分析
                analysisResult = analyzer.AnalyzeEnhancement(originalImage, enhancedImage);

                // 执行增强分析（新增）
                var comprehensiveResult = PerformComprehensiveAnalysis(originalImage, enhancedImage);

                // 显示结果
                DisplayResults();
                DisplayComprehensiveResults(comprehensiveResult);

                analyzeBtn.Text = "重新分析";
                analyzeBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"分析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                analyzeBtn.Text = "开始分析";
                analyzeBtn.Enabled = true;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void DisplayResults()
        {
            // 显示直方图
            DisplayHistogram();

            // 显示像素映射
            DisplayPixelMapping();

            // 显示文本结果
            DisplayTextResults();

            // 显示灰度值分析
            DisplayGrayValueAnalysis();
        }

        /// <summary>
        /// 显示直方图（优化版：改善显示效果和性能）
        /// </summary>
        private void DisplayHistogram()
        {
            try
            {
                histogramChart.Series.Clear();
                histogramChart.Legends.Clear();

                // 添加图例
                var legend = new Legend("Legend")
                {
                    Docking = Docking.Top,
                    Alignment = StringAlignment.Center,
                    Font = new Font("Arial", 9)
                };
                histogramChart.Legends.Add(legend);

                var originalSeries = new Series("原图")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Blue,
                    BorderWidth = 2,
                    Legend = "Legend"
                };
                var enhancedSeries = new Series("增强后")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Red,
                    BorderWidth = 2,
                    Legend = "Legend"
                };

                // 修复X轴映射问题：确保显示完整的灰度值范围
                int histogramLength = analysisResult.OriginalHistogram.Length;
                int maxGrayValue = histogramLength - 1;  // 通常是255或65535

                // 降采样以提高性能（最多显示500个点）
                int step = Math.Max(1, histogramLength / 500);
                for (int i = 0; i < histogramLength; i += step)
                {
                    // 使用实际的灰度值作为X坐标，而不是数组索引
                    double actualGrayValue = (double)i * maxGrayValue / (histogramLength - 1);
                    originalSeries.Points.AddXY(actualGrayValue, analysisResult.OriginalHistogram[i]);
                    enhancedSeries.Points.AddXY(actualGrayValue, analysisResult.EnhancedHistogram[i]);
                }

                histogramChart.Series.Add(originalSeries);
                histogramChart.Series.Add(enhancedSeries);

                // 设置图表区域属性
                var chartArea = histogramChart.ChartAreas[0];
                chartArea.AxisX.MajorGrid.Enabled = true;
                chartArea.AxisY.MajorGrid.Enabled = true;
                chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
                chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;

                // 修复X轴范围：根据图像位深设置正确的范围（使用已定义的maxGrayValue）
                chartArea.AxisX.Minimum = 0;
                chartArea.AxisX.Maximum = maxGrayValue;

                // 设置X轴标签格式
                if (maxGrayValue > 1000)
                {
                    // 16位图像，使用千位分隔符
                    chartArea.AxisX.LabelStyle.Format = "#,##0";
                    chartArea.AxisX.Interval = maxGrayValue / 10;  // 显示10个主要刻度
                }
                else
                {
                    // 8位图像
                    chartArea.AxisX.LabelStyle.Format = "0";
                    chartArea.AxisX.Interval = 50;  // 每50个灰度值一个刻度
                }

                // 更新说明标签
                if (histogramLabel != null)
                {
                    string bitDepth = maxGrayValue > 1000 ? "16位" : "8位";
                    histogramLabel.Text = $"X轴：灰度值(0-{maxGrayValue}) {bitDepth}图像 | Y轴：像素数量\n蓝色：原图 | 红色：增强图\n显示图像中不同亮度值的像素分布情况";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示直方图失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 显示像素映射关系（优化版：修复16位图像灰度值显示问题）
        /// </summary>
        private void DisplayPixelMapping()
        {
            try
            {
                mappingChart.Series.Clear();
                mappingChart.Legends.Clear();

                // 检查是否有映射数据
                if (analysisResult?.PixelMapping == null || analysisResult.PixelMapping.Count == 0)
                {
                    // 显示提示信息
                    mappingChart.Titles.Clear();
                    mappingChart.Titles.Add(new Title("无映射数据", Docking.Top, new Font("Arial", 12), Color.Red));
                    return;
                }

                // 添加图例
                var legend = new Legend("Legend")
                {
                    Docking = Docking.Top,
                    Alignment = StringAlignment.Center,
                    Font = new Font("Arial", 9)
                };
                mappingChart.Legends.Add(legend);

                var mappingSeries = new Series("映射关系")
                {
                    ChartType = SeriesChartType.Point,
                    Color = Color.Green,
                    MarkerSize = 3,
                    Legend = "Legend"
                };

                // 降采样以提高性能（最多显示1000个点），同时保持16位图像的完整显示范围
                var sortedMapping = analysisResult.PixelMapping.OrderBy(x => x.Key).ToList();
                int step = Math.Max(1, sortedMapping.Count / 1000);

                for (int i = 0; i < sortedMapping.Count; i += step)
                {
                    // 直接使用实际的像素值，不进行额外转换
                    mappingSeries.Points.AddXY(sortedMapping[i].Key, sortedMapping[i].Value);
                }

                // 调试信息：输出映射范围到控制台
                Console.WriteLine($"像素映射范围 - 原始: {sortedMapping.Min(x => x.Key)}-{sortedMapping.Max(x => x.Key)}, 增强: {sortedMapping.Min(x => x.Value)}-{sortedMapping.Max(x => x.Value)}");

                // 计算实际的数据范围
                double minOriginal = sortedMapping.Min(x => x.Key);
                double maxOriginal = sortedMapping.Max(x => x.Key);
                double minEnhanced = sortedMapping.Min(x => x.Value);
                double maxEnhanced = sortedMapping.Max(x => x.Value);

                // 计算合适的显示范围（稍微扩展以便更好显示）
                double rangeOriginal = maxOriginal - minOriginal;
                double rangeEnhanced = maxEnhanced - minEnhanced;
                double paddingOriginal = rangeOriginal * 0.05;  // 5%的边距
                double paddingEnhanced = rangeEnhanced * 0.05;

                double displayMinOriginal = Math.Max(0, minOriginal - paddingOriginal);
                double displayMaxOriginal = maxOriginal + paddingOriginal;
                double displayMinEnhanced = Math.Max(0, minEnhanced - paddingEnhanced);
                double displayMaxEnhanced = maxEnhanced + paddingEnhanced;

                // 添加对角线参考线（表示无变化）
                var referenceSeries = new Series("无变化参考线")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Gray,
                    BorderDashStyle = ChartDashStyle.Dash,
                    BorderWidth = 1,
                    Legend = "Legend"
                };

                // 添加对角线数据点（使用显示范围）
                double refMin = Math.Min(displayMinOriginal, displayMinEnhanced);
                double refMax = Math.Max(displayMaxOriginal, displayMaxEnhanced);
                referenceSeries.Points.AddXY(refMin, refMin);
                referenceSeries.Points.AddXY(refMax, refMax);

                mappingChart.Series.Add(mappingSeries);
                mappingChart.Series.Add(referenceSeries);

                // 设置图表区域属性
                var chartArea = mappingChart.ChartAreas[0];
                chartArea.AxisX.Title = "原始灰度值";
                chartArea.AxisY.Title = "增强后灰度值";
                chartArea.AxisX.MajorGrid.Enabled = true;
                chartArea.AxisY.MajorGrid.Enabled = true;
                chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
                chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;

                // 设置轴的显示范围
                chartArea.AxisX.Minimum = displayMinOriginal;
                chartArea.AxisX.Maximum = displayMaxOriginal;
                chartArea.AxisY.Minimum = displayMinEnhanced;
                chartArea.AxisY.Maximum = displayMaxEnhanced;

                // 设置轴标签格式 - 修复16位图像检测逻辑
                if (displayMaxOriginal > 1000 || displayMaxEnhanced > 1000)
                {
                    // 16位图像，使用千位分隔符，并设置合适的刻度间隔
                    chartArea.AxisX.LabelStyle.Format = "#,##0";
                    chartArea.AxisY.LabelStyle.Format = "#,##0";
                    
                    // 为16位图像设置合适的刻度间隔
                    chartArea.AxisX.Interval = Math.Max(5000, (displayMaxOriginal - displayMinOriginal) / 10);
                    chartArea.AxisY.Interval = Math.Max(5000, (displayMaxEnhanced - displayMinEnhanced) / 10);
                }
                else
                {
                    // 8位图像
                    chartArea.AxisX.LabelStyle.Format = "0";
                    chartArea.AxisY.LabelStyle.Format = "0";
                    chartArea.AxisX.Interval = Math.Max(25, (displayMaxOriginal - displayMinOriginal) / 10);
                    chartArea.AxisY.Interval = Math.Max(25, (displayMaxEnhanced - displayMinEnhanced) / 10);
                }

                // 更新说明标签
                if (mappingLabel != null)
                {
                    string bitDepth = (displayMaxOriginal > 1000 || displayMaxEnhanced > 1000) ? "16位" : "8位";
                    string rangeInfo = bitDepth == "16位" ? "(0-65535)" : "(0-255)";
                    mappingLabel.Text = $"X轴：原始灰度值({displayMinOriginal:F0}-{displayMaxOriginal:F0}) | Y轴：增强后灰度值({displayMinEnhanced:F0}-{displayMaxEnhanced:F0})\n绿色点：实际映射关系 | 灰色虚线：无变化参考 | {bitDepth}图像{rangeInfo}\n点在参考线上方表示增强，下方表示减弱";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示像素映射失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayTextResults()
        {
            var result = analysisResult;
            var text = $@"=== 图像增强分析报告 ===

推断算法: {result.SuggestedAlgorithm}

=== 全局参数 ===
对比度变化倍数: {result.ContrastRatio:F2}
亮度变化倍数: {result.BrightnessChange:F2}
估算Gamma值: {result.GammaEstimate:F2}

=== 局部增强分析 ===
暗部增强倍数: {result.LocalInfo.DarkRegionChange:F2}
中间调增强倍数: {result.LocalInfo.MidRegionChange:F2}
亮部增强倍数: {result.LocalInfo.BrightRegionChange:F2}
边缘增强强度: {result.LocalInfo.EdgeEnhancement:F2}
是否有局部对比度增强: {(result.LocalInfo.HasLocalContrast ? "是" : "否")}

=== 建议的算法参数 ===";

            foreach (var param in result.EstimatedParameters)
            {
                text += $"\n{param.Key}: {param.Value:F2}";
            }

            text += $@"

=== 实现建议 ===
基于分析结果，建议的RetinexEnhancer参数:
- retinexStrength: {Math.Max(0.5, Math.Min(2.0, result.LocalInfo.DarkRegionChange - 1.0)):F1}
- localContrastStrength: {Math.Max(1.0, Math.Min(3.0, result.LocalInfo.EdgeEnhancement)):F1}  
- gammaCorrection: {Math.Max(0.5, Math.Min(1.5, result.GammaEstimate)):F1}
- contrastFactor: {Math.Max(1.0, Math.Min(2.5, result.ContrastRatio)):F1}";

            resultTextBox.Text = text;
        }

        /// <summary>
        /// 显示ROI区域按钮点击事件
        /// </summary>
        private void ShowROIButton_Click(object sender, EventArgs e)
        {
            if (originalImage == null)
            {
                MessageBox.Show("请先加载原图", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                // 创建ROI可视化图像
                Mat roiVisualization = CreateROIVisualization(originalImage);

                // 在新窗口中显示ROI可视化结果
                ShowROIVisualizationWindow(roiVisualization);

                roiVisualization.Dispose();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示ROI区域失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        /// <summary>
        /// 创建ROI区域可视化图像
        /// </summary>
        private Mat CreateROIVisualization(Mat originalImage)
        {
            // 创建彩色显示图像（转换为8位3通道）
            Mat display8bit = new Mat();
            originalImage.ConvertTo(display8bit, MatType.CV_8UC1, 255.0 / 65535.0);

            Mat colorDisplay = new Mat();
            Cv2.CvtColor(display8bit, colorDisplay, ColorConversionCodes.GRAY2BGR);

            // 使用OTSU阈值分割来区分工件区域和过曝区域
            Mat binary = new Mat();
            double threshold = Cv2.Threshold(originalImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // 转换为8位掩码
            Mat binary8 = new Mat();
            binary.ConvertTo(binary8, MatType.CV_8U);

            // 创建工件区域掩码（反转二值图像）
            Mat workpieceMask8 = new Mat();
            Cv2.BitwiseNot(binary8, workpieceMask8);

            // 查找工件轮廓
            OpenCvSharp.Point[][] contours;
            Cv2.FindContours(workpieceMask8, out contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 绘制工件轮廓（绿色）
            if (contours.Length > 0)
            {
                // 找到最大的轮廓（主要工件）
                double maxArea = 0;
                int maxContourIndex = 0;
                for (int i = 0; i < contours.Length; i++)
                {
                    double area = Cv2.ContourArea(contours[i]);
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxContourIndex = i;
                    }
                }

                // 绘制主要工件轮廓
                Cv2.DrawContours(colorDisplay, contours, maxContourIndex, new Scalar(0, 255, 0), 3); // 绿色，3像素宽

                // 绘制工件边界框（红色）
                Rect boundingRect = Cv2.BoundingRect(contours[maxContourIndex]);
                Cv2.Rectangle(colorDisplay, boundingRect, new Scalar(0, 0, 255), 2); // 红色，2像素宽

                // 添加文字标注
                string roiInfo = $"ROI: {boundingRect.Width}x{boundingRect.Height}";
                Cv2.PutText(colorDisplay, roiInfo, new OpenCvSharp.Point(boundingRect.X, boundingRect.Y - 10),
                           HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);

                string thresholdInfo = $"Threshold: {threshold:F0}";
                Cv2.PutText(colorDisplay, thresholdInfo, new OpenCvSharp.Point(10, 30),
                           HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 0), 2);
            }

            // 清理临时Mat对象
            display8bit.Dispose();
            binary.Dispose();
            binary8.Dispose();
            workpieceMask8.Dispose();

            return colorDisplay;
        }

        /// <summary>
        /// 在新窗口中显示ROI可视化结果
        /// </summary>
        private void ShowROIVisualizationWindow(Mat roiVisualization)
        {
            Form roiForm = new Form
            {
                Text = "ROI区域可视化",
                Size = new System.Drawing.Size(800, 600),
                StartPosition = FormStartPosition.CenterParent
            };

            PictureBox roiPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = roiVisualization.ToBitmap()
            };

            Label infoLabel = new Label
            {
                Text = "绿色线条：工件轮廓  |  红色框：工件边界框  |  黄色文字：阈值信息",
                Dock = DockStyle.Bottom,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGray
            };

            roiForm.Controls.Add(roiPictureBox);
            roiForm.Controls.Add(infoLabel);

            roiForm.ShowDialog(this);
        }

        /// <summary>
        /// 显示灰度值分布分析
        /// </summary>
        private void DisplayGrayValueAnalysis()
        {
            var text = "=== 灰度值分布分析 ===\n\n";

            // 分析原图灰度值分布
            text += "【原图灰度值分析】\n";
            text += AnalyzeImageGrayValues(originalImage, "原图");
            text += "\n";

            // 分析增强后图像灰度值分布
            text += "【增强后灰度值分析】\n";
            text += AnalyzeImageGrayValues(enhancedImage, "增强后");
            text += "\n";

            // 边缘区域特别分析
            text += "【边缘区域分析】\n";
            text += AnalyzeEdgeRegions();
            text += "\n";

            // 中心区域分析
            text += "【中心区域分析】\n";
            text += AnalyzeCenterRegions();
            text += "\n";

            // 工件区域检测分析
            text += "【工件区域检测分析】\n";
            text += AnalyzeWorkpieceRegion();
            text += "\n";

            // ROI区域内部灰度值分析
            text += "【ROI区域内部灰度值分析】\n";
            text += AnalyzeROIRegionGrayValues();

            grayValueAnalysisTextBox.Text = text;
        }

        /// <summary>
        /// 分析图像的灰度值分布
        /// </summary>
        private string AnalyzeImageGrayValues(Mat image, string imageName)
        {
            if (image == null) return $"{imageName}: 未加载\n";

            var result = "";

            // 基本统计信息
            Scalar mean, stddev;
            Cv2.MeanStdDev(image, out mean, out stddev);

            double minVal, maxVal;
            Cv2.MinMaxLoc(image, out minVal, out maxVal);

            result += $"平均值: {mean.Val0:F0}\n";
            result += $"标准差: {stddev.Val0:F0}\n";
            result += $"最小值: {minVal:F0}\n";
            result += $"最大值: {maxVal:F0}\n";
            result += $"动态范围: {maxVal - minVal:F0}\n";

            // 分位数分析
            Mat flattened = image.Reshape(1, image.Rows * image.Cols);
            Mat sorted = new Mat();
            Cv2.Sort(flattened, sorted, SortFlags.EveryRow | SortFlags.Ascending);

            int totalPixels = sorted.Rows;
            ushort p5 = sorted.At<ushort>(totalPixels * 5 / 100);
            ushort p25 = sorted.At<ushort>(totalPixels * 25 / 100);
            ushort p50 = sorted.At<ushort>(totalPixels * 50 / 100);
            ushort p75 = sorted.At<ushort>(totalPixels * 75 / 100);
            ushort p95 = sorted.At<ushort>(totalPixels * 95 / 100);

            result += $"5%分位数: {p5}\n";
            result += $"25%分位数: {p25}\n";
            result += $"中位数: {p50}\n";
            result += $"75%分位数: {p75}\n";
            result += $"95%分位数: {p95}\n";

            sorted.Dispose();
            flattened.Dispose();

            return result;
        }

        /// <summary>
        /// 分析边缘区域的灰度值变化
        /// </summary>
        private string AnalyzeEdgeRegions()
        {
            if (originalImage == null || enhancedImage == null) return "图像未加载\n";

            var result = "";
            int borderWidth = 20; // 边缘区域宽度

            // 分析四个边缘区域
            string[] regions = { "上边缘", "下边缘", "左边缘", "右边缘" };
            Rect[] rects = {
                new Rect(0, 0, originalImage.Cols, borderWidth), // 上
                new Rect(0, originalImage.Rows - borderWidth, originalImage.Cols, borderWidth), // 下
                new Rect(0, 0, borderWidth, originalImage.Rows), // 左
                new Rect(originalImage.Cols - borderWidth, 0, borderWidth, originalImage.Rows) // 右
            };

            for (int i = 0; i < regions.Length; i++)
            {
                Mat origRegion = new Mat(originalImage, rects[i]);
                Mat enhRegion = new Mat(enhancedImage, rects[i]);

                Scalar origMean = Cv2.Mean(origRegion);
                Scalar enhMean = Cv2.Mean(enhRegion);

                double ratio = enhMean.Val0 / Math.Max(origMean.Val0, 1.0);

                result += $"{regions[i]}: {origMean.Val0:F0} → {enhMean.Val0:F0} (x{ratio:F2})\n";

                origRegion.Dispose();
                enhRegion.Dispose();
            }

            return result;
        }

        /// <summary>
        /// 分析中心区域的灰度值变化
        /// </summary>
        private string AnalyzeCenterRegions()
        {
            if (originalImage == null || enhancedImage == null) return "图像未加载\n";

            var result = "";

            // 中心区域 (图像中心50%区域)
            int centerW = originalImage.Cols / 2;
            int centerH = originalImage.Rows / 2;
            int startX = originalImage.Cols / 4;
            int startY = originalImage.Rows / 4;

            Rect centerRect = new Rect(startX, startY, centerW, centerH);

            Mat origCenter = new Mat(originalImage, centerRect);
            Mat enhCenter = new Mat(enhancedImage, centerRect);

            Scalar origMean = Cv2.Mean(origCenter);
            Scalar enhMean = Cv2.Mean(enhCenter);

            double ratio = enhMean.Val0 / Math.Max(origMean.Val0, 1.0);

            result += $"中心区域: {origMean.Val0:F0} → {enhMean.Val0:F0} (x{ratio:F2})\n";

            // 分析中心区域的均匀性
            Scalar origStd, enhStd;
            Cv2.MeanStdDev(origCenter, out origMean, out origStd);
            Cv2.MeanStdDev(enhCenter, out enhMean, out enhStd);

            result += $"原图中心均匀性(CV): {(origStd.Val0 / origMean.Val0 * 100):F1}%\n";
            result += $"增强后中心均匀性(CV): {(enhStd.Val0 / enhMean.Val0 * 100):F1}%\n";

            origCenter.Dispose();
            enhCenter.Dispose();

            return result;
        }

        /// <summary>
        /// 分析工件区域检测
        /// </summary>
        private string AnalyzeWorkpieceRegion()
        {
            if (originalImage == null) return "原图未加载\n";

            var result = "";

            // 使用OTSU阈值分割来区分工件区域和过曝区域
            Mat binary = new Mat();
            double threshold = Cv2.Threshold(originalImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // 转换为8位掩码用于后续计算
            Mat binary8 = new Mat();
            binary.ConvertTo(binary8, MatType.CV_8U);

            result += $"OTSU自动阈值: {threshold:F0}\n";

            // 统计工件区域和过曝区域的像素数量
            int totalPixels = originalImage.Rows * originalImage.Cols;
            int overexposedPixels = Cv2.CountNonZero(binary8); // 使用8位掩码
            int workpiecePixels = totalPixels - overexposedPixels;

            double workpieceRatio = (double)workpiecePixels / totalPixels * 100;
            double overexposedRatio = (double)overexposedPixels / totalPixels * 100;

            result += $"工件区域像素占比: {workpieceRatio:F1}%\n";
            result += $"过曝区域像素占比: {overexposedRatio:F1}%\n";

            // 分析工件区域的灰度值分布
            Mat workpieceMask8 = new Mat();
            Cv2.BitwiseNot(binary8, workpieceMask8); // 反转8位掩码，工件区域为白色

            Scalar workpieceMean = Cv2.Mean(originalImage, workpieceMask8);
            result += $"工件区域平均灰度值: {workpieceMean.Val0:F0}\n";

            // 分析过曝区域的灰度值分布
            Scalar overexposedMean = Cv2.Mean(originalImage, binary8);
            result += $"过曝区域平均灰度值: {overexposedMean.Val0:F0}\n";

            // 建议的处理策略
            result += "\n【处理建议】\n";
            if (overexposedRatio > 30)
            {
                result += "⚠️ 过曝区域占比过高，建议:\n";
                result += "1. 使用ROI掩码，只处理工件区域\n";
                result += "2. 过曝区域保持原值或轻微处理\n";
                result += "3. 避免对高灰度值区域进行增强\n";
            }
            else
            {
                result += "✓ 过曝区域占比合理，可进行全图处理\n";
            }

            // 检测工件的大致轮廓
            result += "\n【工件轮廓分析】\n";
            result += AnalyzeWorkpieceContour(workpieceMask8);

            binary.Dispose();
            binary8.Dispose();
            workpieceMask8.Dispose();

            return result;
        }

        /// <summary>
        /// 分析工件轮廓
        /// </summary>
        private string AnalyzeWorkpieceContour(Mat workpieceMask)
        {
            var result = "";

            // 查找轮廓
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(workpieceMask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            if (contours.Length > 0)
            {
                // 找到最大的轮廓（主要工件）
                double maxArea = 0;
                int maxContourIndex = 0;
                for (int i = 0; i < contours.Length; i++)
                {
                    double area = Cv2.ContourArea(contours[i]);
                    if (area > maxArea)
                    {
                        maxArea = area;
                        maxContourIndex = i;
                    }
                }

                // 分析主要工件的边界框
                Rect boundingRect = Cv2.BoundingRect(contours[maxContourIndex]);
                result += $"工件边界框: ({boundingRect.X}, {boundingRect.Y}) - ({boundingRect.X + boundingRect.Width}, {boundingRect.Y + boundingRect.Height})\n";
                result += $"工件尺寸: {boundingRect.Width} x {boundingRect.Height}\n";

                // 计算工件在图像中的位置
                double centerX = boundingRect.X + boundingRect.Width / 2.0;
                double centerY = boundingRect.Y + boundingRect.Height / 2.0;
                double imageCenterX = originalImage.Cols / 2.0;
                double imageCenterY = originalImage.Rows / 2.0;

                result += $"工件中心: ({centerX:F0}, {centerY:F0})\n";
                result += $"图像中心: ({imageCenterX:F0}, {imageCenterY:F0})\n";

                double offsetX = centerX - imageCenterX;
                double offsetY = centerY - imageCenterY;
                result += $"工件偏移: ({offsetX:F0}, {offsetY:F0})\n";

                // 建议边缘处理策略
                int edgeDistance = Math.Min(Math.Min(boundingRect.X, boundingRect.Y),
                                          Math.Min(originalImage.Cols - boundingRect.X - boundingRect.Width,
                                                  originalImage.Rows - boundingRect.Y - boundingRect.Height));
                result += $"工件到图像边缘最小距离: {edgeDistance}像素\n";

                if (edgeDistance < 50)
                {
                    result += "⚠️ 工件靠近图像边缘，边缘增强算法可能受影响\n";
                }
            }
            else
            {
                result += "未检测到明显的工件轮廓\n";
            }

            return result;
        }

        /// <summary>
        /// 将Mat转换为Bitmap用于显示（处理16位到8位的转换）
        /// </summary>
        private Bitmap ConvertMatToBitmap(Mat mat)
        {
            if (mat.Type() == MatType.CV_16UC1)
            {
                // 16位转8位显示
                Mat displayMat = new Mat();
                mat.ConvertTo(displayMat, MatType.CV_8UC1, 255.0 / 65535.0);
                Bitmap bitmap = displayMat.ToBitmap();
                displayMat.Dispose();
                return bitmap;
            }
            else
            {
                // 8位直接转换
                return mat.ToBitmap();
            }
        }

        /// <summary>
        /// 加载图像文件（支持DICOM和常规图像格式）
        /// </summary>
        private Mat LoadImageFile(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".dcm" || extension == ".dic" || extension == ".acr")
            {
                // 加载DICOM文件
                return LoadDicomImage(filePath);
            }
            else
            {
                // 加载常规图像文件
                Mat image = Cv2.ImRead(filePath, ImreadModes.Grayscale);

                if (image.Type() == MatType.CV_8UC1)
                {
                    Mat temp = new Mat();
                    image.ConvertTo(temp, MatType.CV_16UC1, 256.0);
                    image.Dispose();
                    return temp;
                }

                return image;
            }
        }

        /// <summary>
        /// 分析ROI区域内部的灰度值分布
        /// </summary>
        private string AnalyzeROIRegionGrayValues()
        {
            if (originalImage == null || enhancedImage == null)
                return "需要加载原图和增强后图像\n";

            try
            {
                var result = "";

                // 创建ROI掩码（使用与RetinexEnhancer相同的方法）
                Mat originalROIMask = CreateROIMaskForAnalysis(originalImage);
                Mat enhancedROIMask = CreateROIMaskForAnalysis(enhancedImage);

                // 分析原图ROI区域
                result += "【原图ROI区域灰度值分析】\n";
                result += AnalyzeROIGrayValues(originalImage, originalROIMask, "原图ROI");
                result += "\n";

                // 分析增强后ROI区域
                result += "【增强后ROI区域灰度值分析】\n";
                result += AnalyzeROIGrayValues(enhancedImage, enhancedROIMask, "增强后ROI");
                result += "\n";

                // 对比分析
                result += "【ROI区域对比分析】\n";
                result += CompareROIRegions(originalImage, enhancedImage, originalROIMask, enhancedROIMask);

                // 清理资源
                originalROIMask.Dispose();
                enhancedROIMask.Dispose();

                return result;
            }
            catch (Exception ex)
            {
                return $"ROI分析失败: {ex.Message}\n";
            }
        }

        /// <summary>
        /// 创建ROI掩码用于分析（与RetinexEnhancer使用相同的算法）
        /// </summary>
        private Mat CreateROIMaskForAnalysis(Mat input)
        {
            try
            {
                // 使用OTSU阈值分割来区分工件区域和过曝区域
                Mat binary = new Mat();
                double threshold = Cv2.Threshold(input, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                // 转换为8位掩码
                Mat mask8 = new Mat();
                binary.ConvertTo(mask8, MatType.CV_8U);

                // 创建工件区域掩码（反转二值图像，因为工件区域灰度值较低）
                Mat roiMask = new Mat();
                Cv2.BitwiseNot(mask8, roiMask);

                // 形态学操作去除噪声
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
                Mat cleaned = new Mat();
                Cv2.MorphologyEx(roiMask, cleaned, MorphTypes.Close, kernel);
                Cv2.MorphologyEx(cleaned, roiMask, MorphTypes.Open, kernel);

                // 清理资源
                binary.Dispose();
                mask8.Dispose();
                cleaned.Dispose();
                kernel.Dispose();

                return roiMask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateROIMaskForAnalysis Error: {ex.Message}");
                // 返回全白掩码（处理所有区域）
                return Mat.Ones(input.Size(), MatType.CV_8U) * 255;
            }
        }

        /// <summary>
        /// 分析ROI区域的灰度值
        /// </summary>
        private string AnalyzeROIGrayValues(Mat image, Mat roiMask, string regionName)
        {
            try
            {
                var result = "";

                // 计算ROI区域的统计信息
                Scalar mean, stddev;
                Cv2.MeanStdDev(image, out mean, out stddev, roiMask);

                // 获取ROI区域的像素值
                Mat roiPixels = new Mat();
                image.CopyTo(roiPixels, roiMask);

                // 计算ROI区域的最小最大值
                double minVal, maxVal;
                OpenCvSharp.Point minLoc, maxLoc;
                Cv2.MinMaxLoc(roiPixels, out minVal, out maxVal, out minLoc, out maxLoc, roiMask);

                // 计算ROI区域像素数量
                int roiPixelCount = Cv2.CountNonZero(roiMask);
                int totalPixels = image.Rows * image.Cols;
                double roiRatio = (double)roiPixelCount / totalPixels * 100;

                result += $"ROI区域像素占比: {roiRatio:F1}%\n";
                result += $"ROI区域平均值: {mean.Val0:F0}\n";
                result += $"ROI区域标准差: {stddev.Val0:F0}\n";
                result += $"ROI区域最小值: {minVal:F0}\n";
                result += $"ROI区域最大值: {maxVal:F0}\n";
                result += $"ROI区域动态范围: {maxVal - minVal:F0}\n";
                result += $"ROI区域变异系数(CV): {(stddev.Val0 / mean.Val0 * 100):F1}%\n";

                roiPixels.Dispose();
                return result;
            }
            catch (Exception ex)
            {
                return $"{regionName}分析失败: {ex.Message}\n";
            }
        }

        /// <summary>
        /// 对比ROI区域的变化
        /// </summary>
        private string CompareROIRegions(Mat originalImage, Mat enhancedImage, Mat originalROIMask, Mat enhancedROIMask)
        {
            try
            {
                var result = "";

                // 计算原图ROI统计
                Scalar origMean, origStddev;
                Cv2.MeanStdDev(originalImage, out origMean, out origStddev, originalROIMask);

                // 计算增强后ROI统计
                Scalar enhMean, enhStddev;
                Cv2.MeanStdDev(enhancedImage, out enhMean, out enhStddev, enhancedROIMask);

                // 计算变化
                double meanChange = enhMean.Val0 - origMean.Val0;
                double meanChangeRatio = enhMean.Val0 / origMean.Val0;
                double stddevChange = enhStddev.Val0 - origStddev.Val0;
                double stddevChangeRatio = enhStddev.Val0 / origStddev.Val0;

                result += $"ROI平均值变化: {origMean.Val0:F0} → {enhMean.Val0:F0} (变化: {meanChange:+F0}, 比例: {meanChangeRatio:F2}x)\n";
                result += $"ROI标准差变化: {origStddev.Val0:F0} → {enhStddev.Val0:F0} (变化: {stddevChange:+F0}, 比例: {stddevChangeRatio:F2}x)\n";

                // 分析增强效果
                result += "\n【ROI增强效果评估】\n";
                if (meanChangeRatio > 1.1)
                    result += "✓ ROI区域亮度有效提升\n";
                else if (meanChangeRatio < 0.9)
                    result += "⚠ ROI区域亮度降低\n";
                else
                    result += "- ROI区域亮度基本保持\n";

                if (stddevChangeRatio > 1.2)
                    result += "✓ ROI区域对比度显著增强\n";
                else if (stddevChangeRatio > 1.05)
                    result += "✓ ROI区域对比度适度增强\n";
                else if (stddevChangeRatio < 0.9)
                    result += "⚠ ROI区域对比度降低\n";
                else
                    result += "- ROI区域对比度基本保持\n";

                return result;
            }
            catch (Exception ex)
            {
                return $"ROI对比分析失败: {ex.Message}\n";
            }
        }

        /// <summary>
        /// 加载DICOM图像
        /// </summary>
        private Mat LoadDicomImage(string dicomPath)
        {
            var dicomFile = DicomFile.Open(dicomPath);

            if (!dicomFile.Dataset.Contains(DicomTag.Columns) ||
                !dicomFile.Dataset.Contains(DicomTag.Rows) ||
                !dicomFile.Dataset.Contains(DicomTag.PixelData))
            {
                throw new InvalidOperationException("DICOM文件缺少必要的图像数据");
            }

            int width = dicomFile.Dataset.GetSingleValue<int>(DicomTag.Columns);
            int height = dicomFile.Dataset.GetSingleValue<int>(DicomTag.Rows);
            byte[] rawData = dicomFile.Dataset.GetValues<byte>(DicomTag.PixelData);

            // 创建16位灰度Mat
            Mat dicomMat = new Mat(height, width, MatType.CV_16UC1);

            // 复制像素数据
            int length = height * width * 2; // 16位 = 2字节
            Marshal.Copy(rawData, 0, dicomMat.Data, length);

            return dicomMat;
        }



        /// <summary>
        /// 执行综合分析
        /// </summary>
        private ComprehensiveAnalysisResult PerformComprehensiveAnalysis(Mat original, Mat enhanced)
        {
            var config = AnalysisConfiguration.Default;

            // 创建ROI掩码（使用现有的ROI检测逻辑）
            Mat roiMask = CreateROIMask(original);

            // 执行ROI区域分析
            var roiQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(original, enhanced, config, roiMask);
            var roiMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(original, enhanced, config, roiMask);
            var roiDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(original, enhanced, config, roiMask);

            // 执行全图分析（用于对比）
            var fullQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(original, enhanced, config, null);
            var fullMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(original, enhanced, config, null);
            var fullDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(original, enhanced, config, null);

            // 生成问题诊断（基于ROI分析）
            var diagnostics = GenerateDiagnostics(roiQualityMetrics, roiMedicalMetrics, roiDetectionMetrics);

            // 生成参数优化建议（基于ROI分析）
            var optimizations = GenerateOptimizationSuggestions(roiQualityMetrics, roiMedicalMetrics, roiDetectionMetrics);

            // 计算ROI综合评分
            double roiTechnicalScore = CalculateTechnicalScore(roiQualityMetrics);
            double roiMedicalScore = roiMedicalMetrics.OverallMedicalQuality;
            double roiDetectionScore = roiDetectionMetrics.OverallSuitability;
            double roiOverallRecommendation = (roiTechnicalScore + roiMedicalScore + roiDetectionScore) / 3;

            // 计算全图综合评分
            double fullTechnicalScore = CalculateTechnicalScore(fullQualityMetrics);
            double fullMedicalScore = fullMedicalMetrics.OverallMedicalQuality;
            double fullDetectionScore = fullDetectionMetrics.OverallSuitability;
            double fullOverallRecommendation = (fullTechnicalScore + fullMedicalScore + fullDetectionScore) / 3;

            // 清理资源
            roiMask?.Dispose();

            return new ComprehensiveAnalysisResult
            {
                ROIQualityMetrics = roiQualityMetrics,
                ROIMedicalMetrics = roiMedicalMetrics,
                ROIDetectionMetrics = roiDetectionMetrics,
                FullImageQualityMetrics = fullQualityMetrics,
                FullImageMedicalMetrics = fullMedicalMetrics,
                FullImageDetectionMetrics = fullDetectionMetrics,
                DiagnosticResults = diagnostics,
                OptimizationSuggestions = optimizations,
                ROITechnicalScore = roiTechnicalScore,
                ROIMedicalScore = roiMedicalScore,
                ROIDetectionScore = roiDetectionScore,
                ROIOverallRecommendation = roiOverallRecommendation,
                FullImageTechnicalScore = fullTechnicalScore,
                FullImageMedicalScore = fullMedicalScore,
                FullImageDetectionScore = fullDetectionScore,
                FullImageOverallRecommendation = fullOverallRecommendation
            };
        }

        /// <summary>
        /// 创建ROI掩码（使用现有的OTSU阈值方法）
        /// </summary>
        private Mat CreateROIMask(Mat image)
        {
            try
            {
                // 使用OTSU阈值分割来区分工件区域和过曝区域
                Mat binary = new Mat();
                double threshold = Cv2.Threshold(image, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

                // 创建掩码：保留低于阈值的区域（工件区域）
                Mat mask = new Mat();
                Cv2.Threshold(image, mask, threshold * 0.8, 255, ThresholdTypes.BinaryInv);
                mask.ConvertTo(mask, MatType.CV_8UC1, 1.0 / 256.0);

                binary.Dispose();
                return mask;
            }
            catch
            {
                // 如果出错，返回全图掩码
                return Mat.Ones(image.Size(), MatType.CV_8UC1);
            }
        }

        /// <summary>
        /// 显示综合分析结果
        /// </summary>
        private void DisplayComprehensiveResults(ComprehensiveAnalysisResult result)
        {
            var currentText = resultTextBox.Text;
            var comprehensiveText = GenerateComprehensiveReport(result);
            resultTextBox.Text = currentText + "\n\n" + comprehensiveText;
        }

        /// <summary>
        /// 生成综合分析报告
        /// </summary>
        private string GenerateComprehensiveReport(ComprehensiveAnalysisResult result)
        {
            var report = "\n=== 增强版图像质量分析 ===\n\n";

            // ROI区域分析（主要关注）
            report += "【ROI区域质量评估】\n";
            if (result.ROIQualityMetrics.SSIM > 0)
                report += $"结构相似性(SSIM): {result.ROIQualityMetrics.SSIM:F3} ({GetQualityRating(result.ROIQualityMetrics.SSIM, 0.8, 0.9)})\n";
            report += $"峰值信噪比(PSNR): {result.ROIQualityMetrics.PSNR:F1} dB ({GetQualityRating(result.ROIQualityMetrics.PSNR, 20, 30)})\n";
            report += $"过度增强评分: {result.ROIQualityMetrics.OverEnhancementScore:F1}% ({GetRiskRating(result.ROIQualityMetrics.OverEnhancementScore)})\n";
            report += $"噪声放大程度: {result.ROIQualityMetrics.NoiseAmplification:F2}x ({GetAmplificationRating(result.ROIQualityMetrics.NoiseAmplification)})\n";
            report += $"边缘质量评分: {result.ROIQualityMetrics.EdgeQuality:F1}/100 ({GetScoreRating(result.ROIQualityMetrics.EdgeQuality)})\n";
            report += $"光晕效应检测: {result.ROIQualityMetrics.HaloEffect:F1}% ({GetRiskRating(result.ROIQualityMetrics.HaloEffect)})\n\n";

            // ROI医学影像专用指标
            report += "【ROI区域医学影像质量评估】\n";
            report += $"信息保持度: {result.ROIMedicalMetrics.InformationPreservation:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.InformationPreservation)})\n";
            report += $"动态范围利用率: {result.ROIMedicalMetrics.DynamicRangeUtilization:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.DynamicRangeUtilization)})\n";
            report += $"局部对比度增强效果: {result.ROIMedicalMetrics.LocalContrastEnhancement:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.LocalContrastEnhancement)})\n";
            report += $"细节保真度: {result.ROIMedicalMetrics.DetailFidelity:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.DetailFidelity)})\n";
            report += $"窗宽窗位适应性: {result.ROIMedicalMetrics.WindowLevelAdaptability:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.WindowLevelAdaptability)})\n";
            report += $"医学影像质量综合评分: {result.ROIMedicalMetrics.OverallMedicalQuality:F1}/100 ({GetScoreRating(result.ROIMedicalMetrics.OverallMedicalQuality)})\n\n";

            // ROI缺陷检测友好度指标
            report += "【ROI区域缺陷检测友好度】\n";
            report += $"细线缺陷可见性提升: {result.ROIDetectionMetrics.ThinLineVisibility:F1}% ({GetImprovementRating(result.ROIDetectionMetrics.ThinLineVisibility)})\n";
            report += $"背景噪声抑制效果: {result.ROIDetectionMetrics.BackgroundNoiseReduction:F1}% ({GetScoreRating(result.ROIDetectionMetrics.BackgroundNoiseReduction)})\n";
            report += $"缺陷对比度提升: {result.ROIDetectionMetrics.DefectBackgroundContrast:F2}x ({GetContrastRating(result.ROIDetectionMetrics.DefectBackgroundContrast)})\n";
            report += $"假阳性风险评估: {result.ROIDetectionMetrics.FalsePositiveRisk:F1}% ({GetRiskRating(result.ROIDetectionMetrics.FalsePositiveRisk)})\n";
            report += $"缺陷检测适用性: {result.ROIDetectionMetrics.OverallSuitability:F1}/100 ({GetScoreRating(result.ROIDetectionMetrics.OverallSuitability)})\n\n";

            // 全图对比分析（参考）
            report += "【全图分析对比（含过曝区域影响）】\n";
            report += $"全图医学影像质量: {result.FullImageMedicalMetrics.OverallMedicalQuality:F1}/100 vs ROI: {result.ROIMedicalMetrics.OverallMedicalQuality:F1}/100\n";
            report += $"全图缺陷检测适用性: {result.FullImageDetectionMetrics.OverallSuitability:F1}/100 vs ROI: {result.ROIDetectionMetrics.OverallSuitability:F1}/100\n";
            report += $"说明: 全图分析包含48.6%过曝区域，ROI分析更准确反映算法效果\n\n";

            // 问题诊断
            if (result.DiagnosticResults?.Length > 0)
            {
                report += "【问题诊断】\n";
                foreach (var diagnostic in result.DiagnosticResults)
                {
                    string severityIcon = diagnostic.Severity >= 4 ? "❌" : diagnostic.Severity >= 3 ? "⚠️" : "ℹ️";
                    report += $"{severityIcon} {diagnostic.Description}\n";
                }
                report += "\n";
            }

            // 参数优化建议
            if (result.OptimizationSuggestions?.Length > 0)
            {
                report += "【参数优化建议】\n";
                foreach (var suggestion in result.OptimizationSuggestions)
                {
                    report += $"• {suggestion.ParameterName}: {suggestion.CurrentValue:F3} → {suggestion.SuggestedValue:F3}\n";
                    report += $"  原因: {suggestion.Reason}\n";
                    report += $"  预期改善: {suggestion.ExpectedImprovement}\n\n";
                }
            }

            // ROI综合评分
            report += "【ROI区域综合评分】\n";
            report += $"技术指标评分: {result.ROITechnicalScore:F1}/100 ({GetScoreRating(result.ROITechnicalScore)})\n";
            report += $"医学影像质量评分: {result.ROIMedicalScore:F1}/100 ({GetScoreRating(result.ROIMedicalScore)})\n";
            report += $"缺陷检测适用性: {result.ROIDetectionScore:F1}/100 ({GetScoreRating(result.ROIDetectionScore)})\n";
            report += $"综合推荐度: {result.ROIOverallRecommendation:F1}/100 ({GetRecommendationRating(result.ROIOverallRecommendation)})\n\n";

            // 全图评分对比
            report += "【全图评分对比】\n";
            report += $"全图综合推荐度: {result.FullImageOverallRecommendation:F1}/100 ({GetRecommendationRating(result.FullImageOverallRecommendation)})\n";
            report += $"ROI综合推荐度: {result.ROIOverallRecommendation:F1}/100 ({GetRecommendationRating(result.ROIOverallRecommendation)})\n";
            report += $"✓ 建议以ROI评分为准，更准确反映算法在关注区域的效果\n";

            return report;
        }

        // 辅助方法 - 评级函数
        private string GetQualityRating(double value, double good, double excellent)
        {
            if (value >= excellent) return "优秀";
            if (value >= good) return "良好";
            if (value >= good * 0.7) return "一般";
            return "较差";
        }

        private string GetScoreRating(double score)
        {
            if (score >= 80) return "优秀";
            if (score >= 60) return "良好";
            if (score >= 40) return "一般";
            return "较差";
        }

        private string GetRiskRating(double risk)
        {
            if (risk <= 10) return "低风险";
            if (risk <= 30) return "中等风险";
            if (risk <= 50) return "高风险";
            return "很高风险";
        }

        private string GetAmplificationRating(double amplification)
        {
            if (amplification <= 1.2) return "可接受";
            if (amplification <= 2.0) return "轻微放大";
            if (amplification <= 3.0) return "明显放大";
            return "严重放大";
        }

        private string GetImprovementRating(double improvement)
        {
            if (improvement >= 50) return "显著提升";
            if (improvement >= 20) return "明显提升";
            if (improvement >= 0) return "轻微提升";
            return "有所恶化";
        }

        private string GetContrastRating(double contrast)
        {
            if (contrast >= 2.0) return "显著提升";
            if (contrast >= 1.5) return "明显提升";
            if (contrast >= 1.1) return "轻微提升";
            return "无明显变化";
        }

        private string GetRecommendationRating(double score)
        {
            if (score >= 85) return "强烈推荐";
            if (score >= 70) return "推荐使用";
            if (score >= 50) return "可以使用";
            return "不推荐";
        }

        // 辅助方法 - 分析函数
        private DiagnosticResult[] GenerateDiagnostics(ImageQualityMetrics quality, MedicalImageMetrics medical, DefectDetectionMetrics detection)
        {
            var diagnostics = new System.Collections.Generic.List<DiagnosticResult>();

            // 检查过度增强
            if (quality.OverEnhancementScore > 30)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "过度增强",
                    Description = "检测到过度增强，可能产生不自然的效果",
                    Severity = quality.OverEnhancementScore > 50 ? 4 : 3,
                    Suggestion = "降低增强强度参数"
                });
            }

            // 检查噪声放大
            if (quality.NoiseAmplification > 2.0)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "噪声放大",
                    Description = "增强过程中噪声被显著放大",
                    Severity = quality.NoiseAmplification > 3.0 ? 4 : 3,
                    Suggestion = "添加噪声抑制处理或降低增强强度"
                });
            }

            // 检查医学影像质量
            if (medical.OverallMedicalQuality < 60)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "医学影像质量低",
                    Description = "医学影像质量评分较低，可能影响诊断效果",
                    Severity = medical.OverallMedicalQuality < 40 ? 4 : 3,
                    Suggestion = "调整参数以改善医学影像质量"
                });
            }

            // 检查信息保持度
            if (medical.InformationPreservation < 70)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "信息保持度低",
                    Description = "增强过程中诊断信息保持度不足",
                    Severity = medical.InformationPreservation < 50 ? 4 : 3,
                    Suggestion = "降低增强强度以保持更多原始信息"
                });
            }

            // 检查窗宽窗位适应性
            if (medical.WindowLevelAdaptability < 60)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "窗宽窗位适应性差",
                    Description = "在不同窗宽窗位设置下表现不稳定",
                    Severity = medical.WindowLevelAdaptability < 40 ? 4 : 3,
                    Suggestion = "优化算法以提高在不同显示条件下的稳定性"
                });
            }

            // 检查光晕效应
            if (quality.HaloEffect > 20)
            {
                diagnostics.Add(new DiagnosticResult
                {
                    ProblemType = "光晕效应",
                    Description = "检测到明显的光晕效应",
                    Severity = quality.HaloEffect > 40 ? 4 : 3,
                    Suggestion = "降低边缘增强强度"
                });
            }

            return diagnostics.ToArray();
        }

        private ParameterOptimizationSuggestion[] GenerateOptimizationSuggestions(ImageQualityMetrics quality, MedicalImageMetrics medical, DefectDetectionMetrics detection)
        {
            var suggestions = new System.Collections.Generic.List<ParameterOptimizationSuggestion>();

            // 基于分析结果生成参数建议
            if (quality.OverEnhancementScore > 30)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "retinexStrength",
                    CurrentValue = 2.0, // 假设当前值
                    SuggestedValue = 1.5,
                    Reason = "减少过度增强",
                    ExpectedImprovement = "改善视觉自然度"
                });
            }

            if (quality.NoiseAmplification > 2.0)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "localContrastStrength",
                    CurrentValue = 1.9,
                    SuggestedValue = 1.2,
                    Reason = "减少噪声放大",
                    ExpectedImprovement = "降低噪声水平"
                });
            }

            if (medical.LocalContrastEnhancement < 60)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "localContrastStrength",
                    CurrentValue = 1.0,
                    SuggestedValue = 1.5,
                    Reason = "改善局部对比度增强效果",
                    ExpectedImprovement = "提升医学影像质量"
                });
            }

            if (medical.DynamicRangeUtilization < 70)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "gammaCorrection",
                    CurrentValue = 1.0,
                    SuggestedValue = 1.2,
                    Reason = "改善动态范围利用率",
                    ExpectedImprovement = "更好地利用灰度范围"
                });
            }

            if (medical.DetailFidelity < 70)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "retinexStrength",
                    CurrentValue = 2.0,
                    SuggestedValue = 1.5,
                    Reason = "提高细节保真度",
                    ExpectedImprovement = "更好地保持细微结构"
                });
            }

            if (detection.OverallSuitability < 70)
            {
                suggestions.Add(new ParameterOptimizationSuggestion
                {
                    ParameterName = "contrastFactor",
                    CurrentValue = 1.5,
                    SuggestedValue = 2.0,
                    Reason = "提高缺陷检测适用性",
                    ExpectedImprovement = "增强缺陷与背景的对比度"
                });
            }

            return suggestions.ToArray();
        }

        private double CalculateTechnicalScore(ImageQualityMetrics quality)
        {
            double psnrScore = Math.Min(100, quality.PSNR * 2); // PSNR转换为0-100分
            double ssimScore = quality.SSIM * 100; // SSIM已经是0-1范围
            double edgeScore = quality.EdgeQuality;
            double overEnhancementPenalty = quality.OverEnhancementScore;
            double noisePenalty = Math.Max(0, (quality.NoiseAmplification - 1.0) * 25);

            double score = (psnrScore + ssimScore + edgeScore) / 3 - overEnhancementPenalty * 0.5 - noisePenalty;
            return Math.Max(0, Math.Min(100, score));
        }



        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            originalImage?.Dispose();
            enhancedImage?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
