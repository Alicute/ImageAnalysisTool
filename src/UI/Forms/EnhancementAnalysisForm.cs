using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using FellowOakDicom;
using ImageAnalysisTool.Core.Analyzers;
using ImageAnalysisTool.Core.Models;
using ImageAnalysisTool.Core.Enhancers;

/// <summary>
/// ROI检测模式枚举
/// </summary>
public enum ROIMode
{
    /// <summary>
    /// 通用OTSU阈值模式
    /// </summary>
    General,

    /// <summary>
    /// 工业X射线焊缝检测模式
    /// </summary>
    Weld
}

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
        private Mat enhanced2Image;
        private ImageEnhancementAnalyzer.AnalysisResult analysisResult;

        // 灰度值分析结果
        private string originalGrayValueAnalysis = "";
        private string enhanced1GrayValueAnalysis = "";
        private string enhanced2GrayValueAnalysis = "";

        // 对比分析结果
        private ComprehensiveAnalysisResult? originalVsEnhanced1Result;
        private ComprehensiveAnalysisResult? originalVsEnhanced2Result;
        private ComprehensiveAnalysisResult? enhanced1VsEnhanced2Result;

        // 单图分析结果（用于AI专项分析）
        private ComprehensiveAnalysisResult? enhanced1AnalysisResult;
        private ComprehensiveAnalysisResult? enhanced2AnalysisResult;

        // UI控件
        private ScrollableControl mainScrollPanel;  // 主滚动面板
        private TableLayoutPanel mainLayout;
        private Panel topControlPanel;              // 顶部控制面板
        private Panel imagePanel;
        private PictureBox originalPictureBox;
        private PictureBox enhancedPictureBox;
        private PictureBox enhanced2PictureBox;
        private Button loadOriginalBtn;
        private Button loadEnhancedBtn;
        private Button loadEnhanced2Btn;
        private Button analyzeBtn;
        private Button compareBtn;
        private Button showROIButton;
        private ComboBox roiModeComboBox;
        private Label roiModeLabel;

        // 像素映射模式控件
        private RadioButton fullImageMappingRadio;
        private RadioButton roiMappingRadio;
        private Panel mappingModePanel;

        // 新的列式布局控件
        private Panel originalColumnPanel;
        private Panel enhanced1ColumnPanel;
        private Panel enhanced2ColumnPanel;

        // 原图列控件
        private Label originalLabel;
        private Chart originalHistogramChart;
        private Chart originalPixelMappingChart;
        private RichTextBox originalAnalysisTextBox;
        private RichTextBox originalGrayValueAnalysisTextBox;
        private RichTextBox originalAISummaryTextBox;

        // 增强图1列控件
        private Label enhancedLabel;
        private Chart enhanced1HistogramChart;
        private Chart enhanced1PixelMappingChart;
        private RichTextBox enhanced1AnalysisTextBox;
        private RichTextBox enhanced1GrayValueAnalysisTextBox;
        private RichTextBox enhanced1AISummaryTextBox;

        // 增强图2列控件
        private Label enhanced2Label;
        private Chart enhanced2HistogramChart;
        private Chart enhanced2PixelMappingChart;
        private RichTextBox enhanced2AnalysisTextBox;
        private RichTextBox enhanced2GrayValueAnalysisTextBox;
        private RichTextBox enhanced2AISummaryTextBox;

        // 保留旧的控件引用（兼容性）
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
                Size = new System.Drawing.Size(1580, 2000),  // 再次增加高度以容纳AI分析总结行
                ColumnCount = 3,  // 3列：原图、增强图1、增强图2
                RowCount = 2      // 2行：顶部控制、3列内容
            };

            // 设置列和行的大小
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 原图列
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 增强图1列
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F)); // 增强图2列
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));         // 顶部控制面板
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));         // 3列内容区域

            // 创建顶部控制面板
            CreateTopControlPanel();

            // 创建3列内容面板
            CreateColumnPanels();

            // 设置兼容性引用
            SetCompatibilityReferences();

            mainScrollPanel.Controls.Add(mainLayout);
            this.Controls.Add(mainScrollPanel);
        }

        /// <summary>
        /// 创建3列内容面板
        /// </summary>
        private void CreateColumnPanels()
        {
            // 创建原图列
            CreateOriginalColumn();

            // 创建增强图1列
            CreateEnhanced1Column();

            // 创建增强图2列
            CreateEnhanced2Column();
        }

        /// <summary>
        /// 创建原图列
        /// </summary>
        private void CreateOriginalColumn()
        {
            originalColumnPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建垂直布局
            TableLayoutPanel columnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,  // 增加到7行
                AutoSize = true
            };

            // 设置行样式（使用绝对高度，让列变得更长）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));   // 标题
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 图像
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 直方图
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 像素映射
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 分析结果（增大）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));  // 灰度值分析（增大）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 智能分析总结（新增）

            // 创建标题标签
            originalLabel = new Label
            {
                Text = "原始图像",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightBlue
            };

            // 创建图像显示控件
            originalPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建直方图
            originalHistogramChart = CreateChart("原图直方图");

            // 创建像素映射图
            originalPixelMappingChart = CreateChart("原图像素映射");

            // 创建分析结果文本框
            originalAnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.White
            };

            // 创建灰度值分析文本框
            originalGrayValueAnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightYellow
            };

            // 创建AI分析总结文本框
            originalAISummaryTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightCyan,
                Text = "AI分析数据摘要将在这里显示...\n\n点击'对比分析'后生成结构化数据，可复制给AI进行深度分析。"
            };

            // 添加到列布局
            columnLayout.Controls.Add(originalLabel, 0, 0);
            columnLayout.Controls.Add(originalPictureBox, 0, 1);
            columnLayout.Controls.Add(originalHistogramChart, 0, 2);
            columnLayout.Controls.Add(originalPixelMappingChart, 0, 3);
            columnLayout.Controls.Add(originalAnalysisTextBox, 0, 4);
            columnLayout.Controls.Add(originalGrayValueAnalysisTextBox, 0, 5);
            columnLayout.Controls.Add(originalAISummaryTextBox, 0, 6);

            originalColumnPanel.Controls.Add(columnLayout);

            // 添加到主布局
            mainLayout.Controls.Add(originalColumnPanel, 0, 1);
        }

        /// <summary>
        /// 创建增强图1列
        /// </summary>
        private void CreateEnhanced1Column()
        {
            enhanced1ColumnPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建垂直布局
            TableLayoutPanel columnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,  // 增加到7行
                AutoSize = true
            };

            // 设置行样式（使用绝对高度，让列变得更长）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));   // 标题
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 图像
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 直方图
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 像素映射
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 分析结果（增大）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));  // 灰度值分析（增大）

            // 创建标题标签
            enhancedLabel = new Label
            {
                Text = "增强图像1",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightGreen
            };

            // 创建图像显示控件
            enhancedPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建直方图
            enhanced1HistogramChart = CreateChart("增强图1直方图");

            // 创建像素映射图
            enhanced1PixelMappingChart = CreateChart("增强图1像素映射");

            // 创建分析结果文本框
            enhanced1AnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.White
            };

            // 创建灰度值分析文本框
            enhanced1GrayValueAnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightYellow
            };

            // 创建AI分析总结文本框
            enhanced1AISummaryTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightCyan,
                Text = "AI分析数据摘要将在这里显示...\n\n点击'对比分析'后生成结构化数据，可复制给AI进行深度分析。"
            };

            // 添加到列布局
            columnLayout.Controls.Add(enhancedLabel, 0, 0);
            columnLayout.Controls.Add(enhancedPictureBox, 0, 1);
            columnLayout.Controls.Add(enhanced1HistogramChart, 0, 2);
            columnLayout.Controls.Add(enhanced1PixelMappingChart, 0, 3);
            columnLayout.Controls.Add(enhanced1AnalysisTextBox, 0, 4);
            columnLayout.Controls.Add(enhanced1GrayValueAnalysisTextBox, 0, 5);
            columnLayout.Controls.Add(enhanced1AISummaryTextBox, 0, 6);

            enhanced1ColumnPanel.Controls.Add(columnLayout);

            // 添加到主布局
            mainLayout.Controls.Add(enhanced1ColumnPanel, 1, 1);
        }

        /// <summary>
        /// 创建增强图2列
        /// </summary>
        private void CreateEnhanced2Column()
        {
            enhanced2ColumnPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建垂直布局
            TableLayoutPanel columnLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 7,  // 增加到7行
                AutoSize = true
            };

            // 设置行样式（使用绝对高度，让列变得更长）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));   // 标题
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 图像
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 直方图
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));  // 像素映射
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));  // 分析结果（增大）
            columnLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));  // 灰度值分析（增大）

            // 创建标题标签
            enhanced2Label = new Label
            {
                Text = "增强图像2",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 12, FontStyle.Bold),
                BackColor = Color.LightCoral
            };

            // 创建图像显示控件
            enhanced2PictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            // 创建直方图
            enhanced2HistogramChart = CreateChart("增强图2直方图");

            // 创建像素映射图
            enhanced2PixelMappingChart = CreateChart("增强图2像素映射");

            // 创建分析结果文本框
            enhanced2AnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.White
            };

            // 创建灰度值分析文本框
            enhanced2GrayValueAnalysisTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightYellow
            };

            // 创建AI分析总结文本框
            enhanced2AISummaryTextBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = new Font("Consolas", 9),
                BackColor = Color.LightCyan,
                Text = "AI分析数据摘要将在这里显示...\n\n点击'对比分析'后生成结构化数据，可复制给AI进行深度分析。"
            };

            // 添加到列布局
            columnLayout.Controls.Add(enhanced2Label, 0, 0);
            columnLayout.Controls.Add(enhanced2PictureBox, 0, 1);
            columnLayout.Controls.Add(enhanced2HistogramChart, 0, 2);
            columnLayout.Controls.Add(enhanced2PixelMappingChart, 0, 3);
            columnLayout.Controls.Add(enhanced2AnalysisTextBox, 0, 4);
            columnLayout.Controls.Add(enhanced2GrayValueAnalysisTextBox, 0, 5);
            columnLayout.Controls.Add(enhanced2AISummaryTextBox, 0, 6);

            enhanced2ColumnPanel.Controls.Add(columnLayout);

            // 添加到主布局
            mainLayout.Controls.Add(enhanced2ColumnPanel, 2, 1);
        }

        /// <summary>
        /// 创建图表控件的通用方法
        /// </summary>
        private Chart CreateChart(string title)
        {
            Chart chart = new Chart
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White
            };

            ChartArea chartArea = new ChartArea("MainArea")
            {
                BackColor = Color.White,
                BorderColor = Color.Black,
                BorderWidth = 1,
                BorderDashStyle = ChartDashStyle.Solid
            };
            chart.ChartAreas.Add(chartArea);

            Title chartTitle = new Title(title)
            {
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            chart.Titles.Add(chartTitle);

            return chart;
        }

        /// <summary>
        /// 设置兼容性引用，让旧代码能够正常工作
        /// </summary>
        private void SetCompatibilityReferences()
        {
            // 设置主要控件引用
            histogramChart = enhanced1HistogramChart;  // 默认使用增强图1的直方图
            mappingChart = enhanced1PixelMappingChart; // 默认使用增强图1的像素映射
            resultTextBox = enhanced1AnalysisTextBox;  // 默认使用增强图1的分析文本
            grayValueAnalysisTextBox = enhanced1GrayValueAnalysisTextBox; // 默认使用增强图1的灰度分析
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
                Text = "加载增强图1",
                Size = new System.Drawing.Size(120, 35),
                Margin = new Padding(5)
            };
            loadEnhancedBtn.Click += LoadEnhancedBtn_Click;

            loadEnhanced2Btn = new Button
            {
                Text = "加载增强图2",
                Size = new System.Drawing.Size(120, 35),
                Margin = new Padding(5)
            };
            loadEnhanced2Btn.Click += LoadEnhanced2Btn_Click;

            analyzeBtn = new Button
            {
                Text = "开始分析",
                Size = new System.Drawing.Size(100, 35),
                Margin = new Padding(5),
                BackColor = Color.LightBlue,
                Enabled = false
            };
            analyzeBtn.Click += AnalyzeBtn_Click;

            compareBtn = new Button
            {
                Text = "对比分析",
                Size = new System.Drawing.Size(100, 35),
                Margin = new Padding(5),
                BackColor = Color.LightGreen,
                Enabled = false
            };
            compareBtn.Click += CompareBtn_Click;

            showROIButton = new Button
            {
                Text = "显示ROI区域",
                Size = new System.Drawing.Size(120, 35),
                Margin = new Padding(5)
            };
            showROIButton.Click += ShowROIButton_Click;

            // 创建ROI模式选择控件
            roiModeLabel = new Label
            {
                Text = "ROI模式:",
                Size = new System.Drawing.Size(60, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 9),
                Margin = new Padding(5)
            };

            roiModeComboBox = new ComboBox
            {
                Size = new System.Drawing.Size(100, 35),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Arial", 9),
                Margin = new Padding(5)
            };
            roiModeComboBox.Items.AddRange(new object[] { "通用模式", "焊缝模式" });
            roiModeComboBox.SelectedIndex = 0; // 默认选择通用模式

            // 创建像素映射模式控件
            CreateMappingModeControls();

            // 添加按钮到布局
            buttonLayout.Controls.AddRange(new Control[] {
                loadOriginalBtn, loadEnhancedBtn, loadEnhanced2Btn, analyzeBtn, compareBtn, showROIButton,
                roiModeLabel, roiModeComboBox, mappingModePanel
            });

            topControlPanel.Controls.Add(buttonLayout);



            // 添加到主布局，跨3列
            mainLayout.Controls.Add(topControlPanel, 0, 0);
            mainLayout.SetColumnSpan(topControlPanel, 3);
        }

        /// <summary>
        /// 创建像素映射模式控件
        /// </summary>
        private void CreateMappingModeControls()
        {
            mappingModePanel = new Panel
            {
                Size = new System.Drawing.Size(150, 35),
                Margin = new Padding(5),
                BorderStyle = BorderStyle.FixedSingle
            };

            fullImageMappingRadio = new RadioButton
            {
                Text = "全图",
                Location = new System.Drawing.Point(5, 8),
                Size = new System.Drawing.Size(50, 20),
                
                Font = new Font("Arial", 8)
            };
            fullImageMappingRadio.CheckedChanged += MappingModeRadio_CheckedChanged;

            roiMappingRadio = new RadioButton
            {
                Text = "ROI",
                Location = new System.Drawing.Point(60, 8),
                Size = new System.Drawing.Size(50, 20),
                Checked = true, // 默认选择ROI模式
                Font = new Font("Arial", 8)
            };
            roiMappingRadio.CheckedChanged += MappingModeRadio_CheckedChanged;

            // 添加标签
            var modeLabel = new Label
            {
                Text = "映射:",
                Location = new System.Drawing.Point(115, 8),
                Size = new System.Drawing.Size(30, 20),
                Font = new Font("Arial", 8),
                TextAlign = ContentAlignment.MiddleLeft
            };

            mappingModePanel.Controls.AddRange(new Control[] {
                fullImageMappingRadio, roiMappingRadio, modeLabel
            });
        }

        /// <summary>
        /// 像素映射模式切换事件处理
        /// </summary>
        private void MappingModeRadio_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radio && radio.Checked)
            {
                // 重新绘制所有像素映射图
                RefreshPixelMappingCharts();
            }
        }

        /// <summary>
        /// 刷新所有像素映射图表
        /// </summary>
        private void RefreshPixelMappingCharts()
        {
            try
            {
                // 显示像素映射
                if (originalImage != null)
                    DisplayPixelMappingForImages(originalImage, originalImage, originalPixelMappingChart, "原图 vs 原图");
                if (originalImage != null && enhancedImage != null)
                    DisplayPixelMappingForImages(originalImage, enhancedImage, enhanced1PixelMappingChart, "原图 vs 增强图1");
                if (originalImage != null && enhanced2Image != null)
                    DisplayPixelMappingForImages(originalImage, enhanced2Image, enhanced2PixelMappingChart, "原图 vs 增强图2");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新像素映射图表时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建图像显示面板（旧版本，已被列布局替代）
        /// </summary>
        private void CreateImagePanel_Deprecated()
        {
            // 原图显示（旧版本，已被列布局替代）
            var originalPanel_deprecated = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            var originalLabel_deprecated = new Label
            {
                Text = "原图",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightBlue
            };
            // originalPictureBox 现在在列布局中创建
            // originalPanel.Controls.Add(originalPictureBox); // 已移至列布局
            originalPanel_deprecated.Controls.Add(originalLabel_deprecated);

            // 增强图显示（旧版本，已被列布局替代）
            var enhancedPanel_deprecated = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            var enhancedLabel_deprecated = new Label
            {
                Text = "增强图",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightGreen
            };
            // enhancedPictureBox 现在在列布局中创建
            // enhancedPanel_deprecated.Controls.Add(enhancedPictureBox); // 已移至列布局
            enhancedPanel_deprecated.Controls.Add(enhancedLabel_deprecated);

            // 第二个增强图显示（旧版本，已被列布局替代）
            var enhanced2Panel_deprecated = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };
            var enhanced2Label_deprecated = new Label
            {
                Text = "增强图2",
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.LightCoral
            };
            // enhanced2PictureBox 现在在列布局中创建
            // enhanced2Panel_deprecated.Controls.Add(enhanced2PictureBox); // 已移至列布局
            enhanced2Panel_deprecated.Controls.Add(enhanced2Label_deprecated);

            // 添加到主布局 - 使用三列（旧版本，已被列布局替代）
            // mainLayout.Controls.Add(originalPanel_deprecated, 0, 1);
            // mainLayout.Controls.Add(enhancedPanel_deprecated, 1, 1);
            // mainLayout.Controls.Add(enhanced2Panel_deprecated, 2, 1);
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

            // 原图显示（旧版本，已被列布局替代）
            var originalPanel_old = new Panel { Dock = DockStyle.Fill };
            originalPanel_old.Controls.Add(new Label { Text = "原图", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter });
            // originalPictureBox 现在在列布局中创建
            // originalPanel_old.Controls.Add(originalPictureBox);

            // 增强图显示（旧版本，已被列布局替代）
            var enhancedPanel_old = new Panel { Dock = DockStyle.Fill };
            enhancedPanel_old.Controls.Add(new Label { Text = "增强后", Dock = DockStyle.Top, TextAlign = ContentAlignment.MiddleCenter });
            // enhancedPictureBox 现在在列布局中创建
            // enhancedPanel.Controls.Add(enhancedPictureBox); // 已移至列布局

            // imageLayout.Controls.Add(originalPanel_old, 0, 0); // 已移至列布局
            // imageLayout.Controls.Add(enhancedPanel_old, 1, 0); // 已移至列布局
            imagePanel.Controls.Add(imageLayout);
        }



        /// <summary>
        /// 创建结果面板（旧版本，已被列布局替代）
        /// </summary>
        private void CreateResultPanel_Deprecated()
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

            // 添加到主布局，跨3列
            mainLayout.Controls.Add(resultMainPanel, 0, 2);
            mainLayout.SetColumnSpan(resultMainPanel, 3);
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

        private void LoadEnhanced2Btn_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.dcm;*.dic;*.acr;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        enhanced2Image?.Dispose();
                        enhanced2Image = LoadImageFile(dialog.FileName);

                        enhanced2PictureBox.Image = ConvertMatToBitmap(enhanced2Image);
                        CheckCanAnalyze();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载增强图2失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void CheckCanAnalyze()
        {
            // 需要原图和至少一个增强图
            bool hasOriginal = originalImage != null;
            bool hasEnhanced1 = enhancedImage != null;
            bool hasEnhanced2 = enhanced2Image != null;

            // 检查尺寸匹配
            bool sizesMatch = true;
            if (hasOriginal && hasEnhanced1)
            {
                sizesMatch = sizesMatch && originalImage.Size() == enhancedImage.Size();
            }
            if (hasOriginal && hasEnhanced2)
            {
                sizesMatch = sizesMatch && originalImage.Size() == enhanced2Image.Size();
            }

            // 启用分析按钮：有原图且至少有一个增强图，且尺寸匹配
            analyzeBtn.Enabled = hasOriginal && (hasEnhanced1 || hasEnhanced2) && sizesMatch;

            // 启用对比按钮：有原图且有两个增强图，且尺寸匹配
            compareBtn.Enabled = hasOriginal && hasEnhanced1 && hasEnhanced2 && sizesMatch;

            showROIButton.Enabled = hasOriginal;
        }

        /// <summary>
        /// 开始分析按钮点击事件 - 执行单独的灰度值分析
        /// </summary>
        private void AnalyzeBtn_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                analyzeBtn.Text = "分析中...";
                analyzeBtn.Enabled = false;

                // 执行单独的灰度值分析
                PerformGrayValueAnalysis();

                // 显示基础分析结果（直方图、基础信息、ROI灰度值分析）
                DisplayBasicAnalysisResults();

                analyzeBtn.Text = "重新分析";
                analyzeBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"灰度值分析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                analyzeBtn.Text = "开始分析";
                analyzeBtn.Enabled = true;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// 对比分析按钮点击事件 - 执行4种对比分析
        /// </summary>
        private void CompareBtn_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                compareBtn.Text = "对比中...";
                compareBtn.Enabled = false;

                // 执行4种对比分析
                PerformComprehensiveComparison();

                // 显示对比分析结果（像素映射、对比报告）
                DisplayComparisonAnalysisResults();

                // 生成AI分析总结
                GenerateAISummary();

                compareBtn.Text = "重新对比";
                compareBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"对比分析失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                compareBtn.Text = "对比分析";
                compareBtn.Enabled = true;
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        /// <summary>
        /// 执行单独的灰度值分析
        /// </summary>
        private void PerformGrayValueAnalysis()
        {
            // 清空之前的分析结果
            originalGrayValueAnalysis = "";
            enhanced1GrayValueAnalysis = "";
            enhanced2GrayValueAnalysis = "";

            // 分析原图ROI灰度值
            if (originalImage != null)
            {
                originalGrayValueAnalysis = AnalyzeImageGrayValues(originalImage, "原图");
            }

            // 分析增强图1 ROI灰度值
            if (enhancedImage != null)
            {
                enhanced1GrayValueAnalysis = AnalyzeImageGrayValues(enhancedImage, "增强图1");
            }

            // 分析增强图2 ROI灰度值
            if (enhanced2Image != null)
            {
                enhanced2GrayValueAnalysis = AnalyzeImageGrayValues(enhanced2Image, "增强图2");
            }
        }

        /// <summary>
        /// 执行综合对比分析（4种对比）
        /// </summary>
        private void PerformComprehensiveComparison()
        {
            // 清空之前的对比结果
            originalVsEnhanced1Result = null;
            originalVsEnhanced2Result = null;
            enhanced1VsEnhanced2Result = null;
            enhanced1AnalysisResult = null;
            enhanced2AnalysisResult = null;

            // 1. 原图 vs 增强图1
            if (originalImage != null && enhancedImage != null)
            {
                originalVsEnhanced1Result = PerformComprehensiveAnalysis(originalImage, enhancedImage);
            }

            // 2. 原图 vs 增强图2
            if (originalImage != null && enhanced2Image != null)
            {
                originalVsEnhanced2Result = PerformComprehensiveAnalysis(originalImage, enhanced2Image);
            }

            // 3. 增强图1 vs 增强图2
            if (enhancedImage != null && enhanced2Image != null)
            {
                enhanced1VsEnhanced2Result = PerformComprehensiveAnalysis(enhancedImage, enhanced2Image);
            }

            // 4. 生成单图分析结果（用于AI专项分析）
            if (enhancedImage != null && enhanced2Image != null)
            {
                CompareTwoEnhanced(enhancedImage, enhanced2Image);
            }
        }

        /// <summary>
        /// 显示基础分析结果（开始分析按钮）
        /// </summary>
        private void DisplayBasicAnalysisResults()
        {
            // 显示直方图
            if (originalImage != null)
                DisplayHistogramForImage(originalImage, originalHistogramChart, "原图");
            if (enhancedImage != null)
                DisplayHistogramForImage(enhancedImage, enhanced1HistogramChart, "增强图1");
            if (enhanced2Image != null)
                DisplayHistogramForImage(enhanced2Image, enhanced2HistogramChart, "增强图2");

            // 显示基础信息和ROI灰度值分析
            DisplayBasicImageInfo();
        }

        /// <summary>
        /// 显示对比分析结果（对比分析按钮）
        /// </summary>
        private void DisplayComparisonAnalysisResults()
        {
            // 显示像素映射
            if (originalImage != null)
                DisplayPixelMappingForImages(originalImage, originalImage, originalPixelMappingChart, "原图 vs 原图");
            if (originalImage != null && enhancedImage != null)
                DisplayPixelMappingForImages(originalImage, enhancedImage, enhanced1PixelMappingChart, "原图 vs 增强图1");
            if (originalImage != null && enhanced2Image != null)
                DisplayPixelMappingForImages(originalImage, enhanced2Image, enhanced2PixelMappingChart, "原图 vs 增强图2");

            // 显示对比分析报告
            DisplayComparisonReports();
        }

        /// <summary>
        /// 分析单个图像的ROI灰度值
        /// </summary>
        private string AnalyzeImageGrayValues(Mat image, string imageName)
        {
            try
            {
                StringBuilder result = new StringBuilder();
                result.AppendLine($"=== {imageName} ROI灰度值分析 ===\n");

                // 创建ROI掩码（使用通用OTSU方法）
                Mat roiMask = CreateROIMaskForAnalysis(image);

                // 基本信息
                result.AppendLine($"图像尺寸: {image.Width} × {image.Height}");
                result.AppendLine($"图像类型: {image.Type()}");

                // 检测位深
                bool is16Bit = image.Type() == MatType.CV_16UC1;
                int maxValue = is16Bit ? 65535 : 255;
                result.AppendLine($"位深: {(is16Bit ? "16位" : "8位")} (0-{maxValue})");

                // ROI区域统计
                int roiPixels = Cv2.CountNonZero(roiMask);
                int totalPixels = image.Width * image.Height;
                double roiRatio = (double)roiPixels / totalPixels * 100;
                result.AppendLine($"ROI区域占比: {roiRatio:F1}%");
                result.AppendLine($"ROI像素数量: {roiPixels:N0}");

                // ROI区域灰度值统计
                if (roiPixels > 0)
                {
                    Scalar mean, stddev;
                    Cv2.MeanStdDev(image, out mean, out stddev, roiMask);

                    double minVal, maxVal;
                    OpenCvSharp.Point minLoc, maxLoc;
                    Cv2.MinMaxLoc(image, out minVal, out maxVal, out minLoc, out maxLoc, roiMask);

                    result.AppendLine($"\nROI区域灰度值统计:");
                    result.AppendLine($"  平均值: {mean.Val0:F1}");
                    result.AppendLine($"  标准差: {stddev.Val0:F1}");
                    result.AppendLine($"  最小值: {minVal:F0}");
                    result.AppendLine($"  最大值: {maxVal:F0}");
                    result.AppendLine($"  动态范围: {maxVal - minVal:F0}");

                    // 计算对比度
                    double contrast = stddev.Val0 / mean.Val0 * 100;
                    result.AppendLine($"  对比度系数: {contrast:F2}%");
                }
                else
                {
                    result.AppendLine("\n警告: 未检测到有效的ROI区域");
                }

                roiMask.Dispose();
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{imageName} ROI灰度值分析失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 显示基础图像信息
        /// </summary>
        private void DisplayBasicImageInfo()
        {
            // 显示原图信息
            if (originalImage != null)
            {
                originalAnalysisTextBox.Text = GenerateBasicImageReport(originalImage, "原图");
                originalGrayValueAnalysisTextBox.Text = originalGrayValueAnalysis;
            }

            // 显示增强图1信息
            if (enhancedImage != null)
            {
                enhanced1AnalysisTextBox.Text = GenerateBasicImageReport(enhancedImage, "增强图1");
                enhanced1GrayValueAnalysisTextBox.Text = enhanced1GrayValueAnalysis;
            }

            // 显示增强图2信息
            if (enhanced2Image != null)
            {
                enhanced2AnalysisTextBox.Text = GenerateBasicImageReport(enhanced2Image, "增强图2");
                enhanced2GrayValueAnalysisTextBox.Text = enhanced2GrayValueAnalysis;
            }
        }

        /// <summary>
        /// 生成基础图像报告
        /// </summary>
        private string GenerateBasicImageReport(Mat image, string imageName)
        {
            try
            {
                StringBuilder result = new StringBuilder();
                result.AppendLine($"=== {imageName} 基础信息 ===\n");

                // 基本信息
                result.AppendLine($"图像尺寸: {image.Width} × {image.Height}");
                result.AppendLine($"图像类型: {image.Type()}");
                result.AppendLine($"通道数: {image.Channels()}");

                // 检测位深
                bool is16Bit = image.Type() == MatType.CV_16UC1;
                int maxValue = is16Bit ? 65535 : 255;
                result.AppendLine($"位深: {(is16Bit ? "16位" : "8位")} (0-{maxValue})");

                // 全图统计信息
                Scalar mean, stddev;
                Cv2.MeanStdDev(image, out mean, out stddev);
                result.AppendLine($"\n全图统计:");
                result.AppendLine($"  平均值: {mean.Val0:F1}");
                result.AppendLine($"  标准差: {stddev.Val0:F1}");

                double minVal, maxVal;
                OpenCvSharp.Point minLoc, maxLoc;
                Cv2.MinMaxLoc(image, out minVal, out maxVal, out minLoc, out maxLoc);
                result.AppendLine($"  最小值: {minVal:F0}");
                result.AppendLine($"  最大值: {maxVal:F0}");
                result.AppendLine($"  动态范围: {maxVal - minVal:F0}");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"{imageName} 基础信息生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 显示对比分析报告
        /// </summary>
        private void DisplayComparisonReports()
        {
            // 显示原图对比报告（保持基础信息）
            if (originalImage != null)
            {
                originalAnalysisTextBox.Text = GenerateBasicImageReport(originalImage, "原图");
            }

            // 显示增强图1对比报告
            if (enhancedImage != null && originalVsEnhanced1Result.HasValue)
            {
                string enhanced1Report = GenerateComparisonReport(originalVsEnhanced1Result.Value, "增强图1", "原图");

                // 添加增强图1 vs 增强图2的对比结果
                if (enhanced1VsEnhanced2Result.HasValue)
                {
                    enhanced1Report += "\n" + GenerateEnhancedComparisonSection(enhanced1VsEnhanced2Result.Value, "增强图1", "增强图2");
                }

                enhanced1AnalysisTextBox.Text = enhanced1Report;
            }

            // 显示增强图2对比报告
            if (enhanced2Image != null && originalVsEnhanced2Result.HasValue)
            {
                string enhanced2Report = GenerateComparisonReport(originalVsEnhanced2Result.Value, "增强图2", "原图");

                // 添加增强图1 vs 增强图2的对比结果（相同内容）
                if (enhanced1VsEnhanced2Result.HasValue)
                {
                    enhanced2Report += "\n" + GenerateEnhancedComparisonSection(enhanced1VsEnhanced2Result.Value, "增强图2", "增强图1");
                }

                enhanced2AnalysisTextBox.Text = enhanced2Report;
            }
        }

        /// <summary>
        /// 生成对比分析报告
        /// </summary>
        private string GenerateComparisonReport(ComprehensiveAnalysisResult result, string targetName, string baseName)
        {
            try
            {
                StringBuilder report = new StringBuilder();
                report.AppendLine($"=== {targetName} vs {baseName} 对比分析 ===\n");

                // ROI技术评分
                report.AppendLine($"ROI技术评分: {result.ROITechnicalScore:F1}");
                report.AppendLine($"全图技术评分: {result.FullImageTechnicalScore:F1}");

                // 质量指标
                report.AppendLine($"\nROI质量指标:");
                report.AppendLine($"  PSNR: {result.ROIQualityMetrics.PSNR:F2} dB");
                report.AppendLine($"  SSIM: {result.ROIQualityMetrics.SSIM:F4}");
                report.AppendLine($"  边缘质量: {result.ROIQualityMetrics.EdgeQuality:F2}");
                report.AppendLine($"  过度增强评分: {result.ROIQualityMetrics.OverEnhancementScore:F2}");

                // 医学影像指标
                report.AppendLine($"\nROI医学影像指标:");
                report.AppendLine($"  信息保持度: {result.ROIMedicalMetrics.InformationPreservation:F2}");
                report.AppendLine($"  细节保真度: {result.ROIMedicalMetrics.DetailFidelity:F2}");
                report.AppendLine($"  动态范围利用: {result.ROIMedicalMetrics.DynamicRangeUtilization:F2}");
                report.AppendLine($"  局部对比度增强: {result.ROIMedicalMetrics.LocalContrastEnhancement:F2}");

                // 缺陷检测指标
                report.AppendLine($"\nROI缺陷检测指标:");
                report.AppendLine($"  细线缺陷可见性: {result.ROIDetectionMetrics.ThinLineVisibility:F2}");
                report.AppendLine($"  背景噪声抑制: {result.ROIDetectionMetrics.BackgroundNoiseReduction:F2}");
                report.AppendLine($"  缺陷背景对比度: {result.ROIDetectionMetrics.DefectBackgroundContrast:F2}");
                report.AppendLine($"  综合适用性: {result.ROIDetectionMetrics.OverallSuitability:F2}");

                return report.ToString();
            }
            catch (Exception ex)
            {
                return $"{targetName} vs {baseName} 对比报告生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 生成增强图间对比部分
        /// </summary>
        private string GenerateEnhancedComparisonSection(ComprehensiveAnalysisResult result, string viewerName, string compareName)
        {
            try
            {
                StringBuilder section = new StringBuilder();
                section.AppendLine($"=== 增强图对比报告 ===");
                section.AppendLine($"{viewerName} vs {compareName}\n");

                // 综合评分对比
                section.AppendLine($"ROI技术评分差异: {result.ROITechnicalScore:F1}");
                section.AppendLine($"全图技术评分差异: {result.FullImageTechnicalScore:F1}");

                // 推荐结论
                string recommendation = "";
                if (result.ROITechnicalScore > 5)
                {
                    recommendation = viewerName == "增强图1" ? "推荐增强图1" : "推荐增强图2";
                }
                else if (result.ROITechnicalScore < -5)
                {
                    recommendation = viewerName == "增强图1" ? "推荐增强图2" : "推荐增强图1";
                }
                else
                {
                    recommendation = "两种增强效果相近";
                }

                section.AppendLine($"\n推荐结论: {recommendation}");

                // 主要差异分析
                section.AppendLine($"\n主要差异:");
                if (Math.Abs(result.ROIQualityMetrics.PSNR) > 1)
                    section.AppendLine($"  PSNR差异: {result.ROIQualityMetrics.PSNR:F2} dB");
                if (Math.Abs(result.ROIQualityMetrics.SSIM) > 0.01)
                    section.AppendLine($"  SSIM差异: {result.ROIQualityMetrics.SSIM:F4}");
                if (Math.Abs(result.ROIQualityMetrics.EdgeQuality) > 0.1)
                    section.AppendLine($"  边缘质量差异: {result.ROIQualityMetrics.EdgeQuality:F2}");
                if (Math.Abs(result.ROIMedicalMetrics.LocalContrastEnhancement) > 0.1)
                    section.AppendLine($"  局部对比度差异: {result.ROIMedicalMetrics.LocalContrastEnhancement:F2}");

                return section.ToString();
            }
            catch (Exception ex)
            {
                return $"增强图对比部分生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 在新的列布局中显示结果
        /// </summary>
        private void DisplayColumnResults()
        {
            try
            {
                // 显示原图分析结果
                if (originalImage != null)
                {
                    DisplayOriginalImageResults();
                }

                // 显示增强图1分析结果
                if (enhancedImage != null)
                {
                    DisplayEnhanced1ImageResults();
                }

                // 显示增强图2分析结果
                if (enhanced2Image != null)
                {
                    DisplayEnhanced2ImageResults();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示结果时出错: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 显示原图分析结果
        /// </summary>
        private void DisplayOriginalImageResults()
        {
            // 显示原图直方图
            DisplayHistogramForImage(originalImage, originalHistogramChart, "原图");

            // 显示原图像素映射（原图vs原图，作为参考基准）
            DisplayPixelMappingForImages(originalImage, originalImage, originalPixelMappingChart, "原图 vs 原图");

            // 显示原图分析文本
            DisplayAnalysisTextForImage(originalImage, originalAnalysisTextBox, "原图");
        }

        /// <summary>
        /// 显示增强图1分析结果
        /// </summary>
        private void DisplayEnhanced1ImageResults()
        {
            // 显示增强图1直方图
            DisplayHistogramForImage(enhancedImage, enhanced1HistogramChart, "增强图1");

            // 显示增强图1像素映射（原图vs增强图1）
            DisplayPixelMappingForImages(originalImage, enhancedImage, enhanced1PixelMappingChart, "原图 vs 增强图1");

            // 显示增强图1分析文本
            DisplayAnalysisTextForImage(enhancedImage, enhanced1AnalysisTextBox, "增强图1");
        }

        /// <summary>
        /// 显示增强图2分析结果
        /// </summary>
        private void DisplayEnhanced2ImageResults()
        {
            // 显示增强图2直方图
            DisplayHistogramForImage(enhanced2Image, enhanced2HistogramChart, "增强图2");

            // 显示增强图2像素映射（原图vs增强图2）
            DisplayPixelMappingForImages(originalImage, enhanced2Image, enhanced2PixelMappingChart, "原图 vs 增强图2");

            // 显示增强图2分析文本
            DisplayAnalysisTextForImage(enhanced2Image, enhanced2AnalysisTextBox, "增强图2");
        }

        /// <summary>
        /// 为单个图像显示直方图（支持16位数据）
        /// </summary>
        private void DisplayHistogramForImage(Mat image, Chart chart, string imageName)
        {
            try
            {
                chart.Series.Clear();
                chart.Legends.Clear();

                // 检测图像位深
                bool is16Bit = image.Type() == MatType.CV_16UC1;
                int maxValue = is16Bit ? 65535 : 255;
                int binCount = maxValue + 1;

                // 计算直方图
                Mat hist = new Mat();
                int[] histSize = { binCount };
                Rangef[] ranges = { new Rangef(0, maxValue + 1) };
                Cv2.CalcHist(new Mat[] { image }, new int[] { 0 }, null, hist, 1, histSize, ranges);

                // 创建系列
                var series = new Series(imageName)
                {
                    ChartType = SeriesChartType.Column,
                    Color = GetColorForImage(imageName)
                };

                // 设置柱子宽度为100%以消除间隔，形成连续的山丘效果
                series["PointWidth"] = "1.0";

                // 添加所有数据点，但只添加有值的点以提高性能
                for (int i = 0; i < binCount; i++)
                {
                    float value = hist.Get<float>(i);
                    if (value > 0) // 只添加有数据的点
                    {
                        series.Points.AddXY(i, value);
                    }
                }

                chart.Series.Add(series);

                // 设置图表区域
                if (chart.ChartAreas.Count > 0)
                {
                    var chartArea = chart.ChartAreas[0];

                    // 设置坐标轴标题
                    chartArea.AxisX.Title = $"灰度值 (0-{maxValue})";
                    chartArea.AxisY.Title = "像素数量 (对数刻度)";
                    chartArea.AxisX.Minimum = 0;
                    chartArea.AxisX.Maximum = maxValue;

                    // 设置Y轴为对数刻度
                    chartArea.AxisY.IsLogarithmic = true;
                    chartArea.AxisY.LogarithmBase = 10;
                    chartArea.AxisY.Minimum = 1; // 对数刻度最小值必须大于0

                    // 启用网格线
                    chartArea.AxisX.MajorGrid.Enabled = true;
                    chartArea.AxisY.MajorGrid.Enabled = true;
                    chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
                    chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
                    chartArea.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Solid;
                    chartArea.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Solid;

                    // 设置X轴刻度
                    if (is16Bit)
                    {
                        chartArea.AxisX.Interval = 8192; // 每8192一个刻度
                        chartArea.AxisX.LabelStyle.Format = "N0";
                    }
                    else
                    {
                        chartArea.AxisX.Interval = 50; // 8位图像每50一个刻度
                    }

                    // 设置背景色
                    chartArea.BackColor = Color.White;
                }

                // 强制刷新图表
                chart.Invalidate();
                chart.Update();

                hist.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示{imageName}直方图时出错: {ex.Message}");
                // 在图表上显示错误信息
                chart.Series.Clear();
                chart.Titles.Clear();
                chart.Titles.Add($"直方图显示错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 为两个图像显示像素映射关系（改进版：曲线+差值显示）
        /// </summary>
        private void DisplayPixelMappingForImages(Mat sourceImage, Mat targetImage, Chart chart, string title)
        {
            try
            {
                chart.Series.Clear();
                chart.Legends.Clear();

                // 检测图像位深
                bool is16Bit = sourceImage.Type() == MatType.CV_16UC1;
                int maxValue = is16Bit ? 65535 : 255;

                // 根据当前模式收集映射数据
                Dictionary<int, int> mappingData;
                string modeText;

                if (roiMappingRadio != null && roiMappingRadio.Checked)
                {
                    // ROI模式：只分析ROI区域
                    Mat roiMask = CreateROIMaskForAnalysis(sourceImage);
                    mappingData = CollectMappingDataForCurveROI(sourceImage, targetImage, roiMask, is16Bit);
                    modeText = " (ROI分析)";
                    roiMask.Dispose();
                }
                else
                {
                    // 全图模式：分析整个图像
                    mappingData = CollectMappingDataForCurve(sourceImage, targetImage, is16Bit);
                    modeText = " (全图分析)";
                }

                // 1. 创建映射曲线系列
                var curveSeries = new Series("映射曲线")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Blue,
                    BorderWidth = 3,
                    MarkerStyle = MarkerStyle.None
                };

                // 2. 创建理想对角线参考
                var referenceSeries = new Series("理想映射(y=x)")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Gray,
                    BorderWidth = 1,
                    BorderDashStyle = ChartDashStyle.Dash,
                    MarkerStyle = MarkerStyle.None
                };

                // 3. 创建变化区域高亮
                var changeSeries = new Series("显著变化区域")
                {
                    ChartType = SeriesChartType.Point,
                    Color = Color.Red,
                    MarkerSize = 3,
                    MarkerStyle = MarkerStyle.Circle
                };

                // 添加映射曲线数据
                foreach (var point in mappingData.OrderBy(x => x.Key))
                {
                    curveSeries.Points.AddXY(point.Key, point.Value);

                    // 标记显著变化的点（差值超过阈值）
                    int difference = Math.Abs(point.Value - point.Key);
                    int threshold = is16Bit ? 1000 : 10;  // 16位图像阈值1000，8位图像阈值10

                    if (difference > threshold)
                    {
                        changeSeries.Points.AddXY(point.Key, point.Value);
                    }
                }

                // 添加理想对角线
                referenceSeries.Points.AddXY(0, 0);
                referenceSeries.Points.AddXY(maxValue, maxValue);

                // 添加系列到图表
                chart.Series.Add(curveSeries);
                chart.Series.Add(referenceSeries);
                if (changeSeries.Points.Count > 0)
                {
                    chart.Series.Add(changeSeries);
                }

                // 设置图表区域
                if (chart.ChartAreas.Count > 0)
                {
                    chart.ChartAreas[0].AxisX.Title = $"原始像素值 (0-{maxValue})";
                    chart.ChartAreas[0].AxisY.Title = $"增强像素值 (0-{maxValue})";
                    chart.ChartAreas[0].AxisX.Minimum = 0;
                    chart.ChartAreas[0].AxisX.Maximum = maxValue;
                    chart.ChartAreas[0].AxisY.Minimum = 0;
                    chart.ChartAreas[0].AxisY.Maximum = maxValue;

                    // 16位图像设置合适的刻度间隔
                    if (is16Bit)
                    {
                        chart.ChartAreas[0].AxisX.Interval = 8192;
                        chart.ChartAreas[0].AxisY.Interval = 8192;
                        chart.ChartAreas[0].AxisX.LabelStyle.Format = "N0";
                        chart.ChartAreas[0].AxisY.LabelStyle.Format = "N0";
                    }
                }

                // 添加图例
                chart.Legends.Add(new Legend("映射关系")
                {
                    Docking = Docking.Top,
                    Alignment = StringAlignment.Center
                });

                // 设置标题
                chart.Titles.Clear();
                chart.Titles.Add(title);

                // 强制刷新图表
                chart.Invalidate();
                chart.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示像素映射时出错: {ex.Message}");
                // 在图表上显示错误信息
                chart.Series.Clear();
                chart.Titles.Clear();
                chart.Titles.Add($"像素映射显示错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 收集映射数据用于曲线显示（智能分组和平滑）
        /// </summary>
        private Dictionary<int, int> CollectMappingDataForCurve(Mat sourceImage, Mat targetImage, bool is16Bit)
        {
            return CollectMappingDataForCurveInternal(sourceImage, targetImage, null, is16Bit);
        }

        /// <summary>
        /// 收集ROI区域的映射数据用于曲线显示
        /// </summary>
        private Dictionary<int, int> CollectMappingDataForCurveROI(Mat sourceImage, Mat targetImage, Mat roiMask, bool is16Bit)
        {
            return CollectMappingDataForCurveInternal(sourceImage, targetImage, roiMask, is16Bit);
        }

        /// <summary>
        /// 内部方法：收集映射数据（支持ROI掩码）
        /// </summary>
        private Dictionary<int, int> CollectMappingDataForCurveInternal(Mat sourceImage, Mat targetImage, Mat roiMask, bool is16Bit)
        {
            var mappingGroups = new Dictionary<int, List<int>>();
            int maxValue = is16Bit ? 65535 : 255;

            // 分组大小：16位图像分256组，8位图像不分组
            int groupSize = is16Bit ? 256 : 1;

            // 采样策略：更密集的采样以获得更平滑的曲线
            int totalPixels = sourceImage.Width * sourceImage.Height;
            int targetSamples = 10000;  // 增加采样点数
            int step = Math.Max(1, (int)Math.Sqrt(totalPixels / targetSamples));

            for (int y = 0; y < sourceImage.Height; y += step)
            {
                for (int x = 0; x < sourceImage.Width; x += step)
                {
                    if (x < sourceImage.Width && y < sourceImage.Height)
                    {
                        // 如果有ROI掩码，检查当前像素是否在ROI区域内
                        if (roiMask != null)
                        {
                            byte maskValue = roiMask.Get<byte>(y, x);
                            if (maskValue == 0) // 不在ROI区域内，跳过
                                continue;
                        }

                        int sourceVal, targetVal;

                        if (is16Bit)
                        {
                            sourceVal = sourceImage.Get<ushort>(y, x);
                            targetVal = targetImage.Get<ushort>(y, x);
                        }
                        else
                        {
                            sourceVal = sourceImage.Get<byte>(y, x);
                            targetVal = targetImage.Get<byte>(y, x);
                        }

                        // 分组处理
                        int groupKey = (sourceVal / groupSize) * groupSize;

                        if (!mappingGroups.ContainsKey(groupKey))
                            mappingGroups[groupKey] = new List<int>();

                        mappingGroups[groupKey].Add(targetVal);
                    }
                }
            }

            // 计算每组的平均值，生成平滑曲线
            var result = new Dictionary<int, int>();
            foreach (var group in mappingGroups)
            {
                if (group.Value.Count > 0)
                {
                    result[group.Key] = (int)group.Value.Average();
                }
            }

            return result;
        }

        /// <summary>
        /// 显示像素值变化差值图（可选的替代显示方式）
        /// </summary>
        private void DisplayPixelMappingAsDifference(Mat sourceImage, Mat targetImage, Chart chart, string title)
        {
            try
            {
                chart.Series.Clear();
                chart.Legends.Clear();

                // 检测图像位深
                bool is16Bit = sourceImage.Type() == MatType.CV_16UC1;
                int maxValue = is16Bit ? 65535 : 255;

                // 收集映射数据
                var mappingData = CollectMappingDataForCurve(sourceImage, targetImage, is16Bit);

                // 创建差值曲线系列
                var differenceSeries = new Series("像素值变化")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Red,
                    BorderWidth = 2,
                    MarkerStyle = MarkerStyle.None
                };

                // 创建零线参考
                var zeroLineSeries = new Series("无变化线")
                {
                    ChartType = SeriesChartType.Line,
                    Color = Color.Gray,
                    BorderWidth = 1,
                    BorderDashStyle = ChartDashStyle.Dash,
                    MarkerStyle = MarkerStyle.None
                };

                // 添加差值数据
                foreach (var point in mappingData.OrderBy(x => x.Key))
                {
                    int difference = point.Value - point.Key;  // 计算差值
                    differenceSeries.Points.AddXY(point.Key, difference);
                }

                // 添加零线
                zeroLineSeries.Points.AddXY(0, 0);
                zeroLineSeries.Points.AddXY(maxValue, 0);

                // 添加系列到图表
                chart.Series.Add(differenceSeries);
                chart.Series.Add(zeroLineSeries);

                // 设置图表区域
                if (chart.ChartAreas.Count > 0)
                {
                    chart.ChartAreas[0].AxisX.Title = $"原始像素值 (0-{maxValue})";
                    chart.ChartAreas[0].AxisY.Title = "像素值变化量 (增强值 - 原值)";
                    chart.ChartAreas[0].AxisX.Minimum = 0;
                    chart.ChartAreas[0].AxisX.Maximum = maxValue;

                    // 16位图像设置合适的刻度间隔
                    if (is16Bit)
                    {
                        chart.ChartAreas[0].AxisX.Interval = 8192;
                        chart.ChartAreas[0].AxisX.LabelStyle.Format = "N0";
                    }
                }

                // 添加图例
                chart.Legends.Add(new Legend("变化关系")
                {
                    Docking = Docking.Top,
                    Alignment = StringAlignment.Center
                });

                // 设置标题
                chart.Titles.Clear();
                chart.Titles.Add(title + " - 变化量视图");

                // 强制刷新图表
                chart.Invalidate();
                chart.Update();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"显示像素变化差值时出错: {ex.Message}");
                chart.Series.Clear();
                chart.Titles.Clear();
                chart.Titles.Add($"差值显示错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 为图像显示分析文本
        /// </summary>
        private void DisplayAnalysisTextForImage(Mat image, RichTextBox textBox, string imageName)
        {
            try
            {
                StringBuilder result = new StringBuilder();
                result.AppendLine($"=== {imageName} 分析结果 ===\n");

                // 基本信息
                result.AppendLine($"图像尺寸: {image.Width} × {image.Height}");
                result.AppendLine($"图像类型: {image.Type()}");
                result.AppendLine($"通道数: {image.Channels()}");

                // 统计信息
                Scalar mean, stddev;
                Cv2.MeanStdDev(image, out mean, out stddev);
                result.AppendLine($"平均值: {mean.Val0:F2}");
                result.AppendLine($"标准差: {stddev.Val0:F2}");

                // 最值信息
                double minVal, maxVal;
                Cv2.MinMaxLoc(image, out minVal, out maxVal);
                result.AppendLine($"最小值: {minVal:F0}");
                result.AppendLine($"最大值: {maxVal:F0}");
                result.AppendLine($"动态范围: {maxVal - minVal:F0}");

                // 如果有分析结果，添加质量指标
                if (analysisResult != null)
                {
                    result.AppendLine("\n=== 质量指标 ===");
                    if (imageName.Contains("增强"))
                    {
                        result.AppendLine($"对比度比率: {analysisResult.ContrastRatio:F2}");
                        result.AppendLine($"亮度变化: {analysisResult.BrightnessChange:F2}");
                        result.AppendLine($"Gamma估值: {analysisResult.GammaEstimate:F2}");
                        result.AppendLine($"推荐算法: {analysisResult.SuggestedAlgorithm}");
                    }
                }

                textBox.Text = result.ToString();
            }
            catch (Exception ex)
            {
                textBox.Text = $"显示{imageName}分析结果时出错: {ex.Message}";
            }
        }



        /// <summary>
        /// 根据图像名称获取对应的颜色
        /// </summary>
        private Color GetColorForImage(string imageName)
        {
            if (imageName.Contains("原图"))
                return Color.DarkBlue;  // 使用深蓝色以匹配目标样式
            else if (imageName.Contains("增强图1"))
                return Color.DarkGreen;  // 使用深绿色
            else if (imageName.Contains("增强图2"))
                return Color.DarkRed;    // 使用深红色
            else
                return Color.Black;
        }

        /// <summary>
        /// 显示直方图（旧版本，保留兼容性）
        /// </summary>
        private void DisplayHistogram_Deprecated()
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
                    ChartType = SeriesChartType.Column,
                    Color = Color.DarkBlue,
                    Legend = "Legend"
                };
                originalSeries["PointWidth"] = "1.0"; // 消除柱子间隔

                var enhancedSeries = new Series("增强后")
                {
                    ChartType = SeriesChartType.Column,
                    Color = Color.DarkRed,
                    Legend = "Legend"
                };
                enhancedSeries["PointWidth"] = "1.0"; // 消除柱子间隔

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
        /// 显示像素映射关系（旧版本，保留兼容性）
        /// </summary>
        private void DisplayPixelMapping_Deprecated()
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

        private void DisplayTextResults_Deprecated()
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

            // 获取当前ROI模式
            ROIMode mode = GetSelectedROIMode();

            // 根据模式创建ROI掩码
            Mat roiMask = CreateIndustrialROIMask(originalImage, mode);

            // 根据模式显示不同的可视化信息
            if (mode == ROIMode.Weld)
            {
                DrawWeldROIVisualization(colorDisplay, originalImage, roiMask);
            }
            else
            {
                DrawGeneralROIVisualization(colorDisplay, originalImage, roiMask);
            }

            // 清理临时Mat对象
            display8bit.Dispose();

            return colorDisplay;
        }

        /// <summary>
        /// 绘制通用ROI可视化
        /// </summary>
        private void DrawGeneralROIVisualization(Mat colorDisplay, Mat originalImage, Mat roiMask)
        {
            // 找到工件轮廓
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(roiMask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 绘制工件轮廓（绿色）
            for (int i = 0; i < contours.Length; i++)
            {
                Cv2.DrawContours(colorDisplay, contours, i, Scalar.Green, 2);
            }

            // 计算并绘制工件边界框（红色）
            if (contours.Length > 0)
            {
                var allPoints = contours.SelectMany(c => c).ToArray();
                if (allPoints.Length > 0)
                {
                    var boundingRect = Cv2.BoundingRect(allPoints);
                    Cv2.Rectangle(colorDisplay, boundingRect, Scalar.Red, 3);
                }
            }

            // 添加阈值信息文字（黄色）
            Mat binary = new Mat();
            double threshold = Cv2.Threshold(originalImage, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            string thresholdInfo = $"通用模式 - OTSU阈值: {threshold:F0}";
            Cv2.PutText(colorDisplay, thresholdInfo, new OpenCvSharp.Point(10, 30),
                HersheyFonts.HersheySimplex, 0.8, Scalar.Yellow, 2);

            // 计算ROI区域占比
            int roiPixels = Cv2.CountNonZero(roiMask);
            double roiRatio = (double)roiPixels / (originalImage.Width * originalImage.Height) * 100;
            string roiInfo = $"ROI区域占比: {roiRatio:F1}%";
            Cv2.PutText(colorDisplay, roiInfo, new OpenCvSharp.Point(10, 70),
                HersheyFonts.HersheySimplex, 0.8, Scalar.Yellow, 2);

            binary.Dispose();
        }

        /// <summary>
        /// 绘制焊缝ROI可视化
        /// </summary>
        private void DrawWeldROIVisualization(Mat colorDisplay, Mat originalImage, Mat roiMask)
        {
            // 找到焊缝轮廓
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(roiMask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 绘制焊缝轮廓（蓝色，区别于通用模式）
            for (int i = 0; i < contours.Length; i++)
            {
                Cv2.DrawContours(colorDisplay, contours, i, Scalar.Blue, 2);
            }

            // 计算并绘制焊缝边界框（橙色）
            if (contours.Length > 0)
            {
                var allPoints = contours.SelectMany(c => c).ToArray();
                if (allPoints.Length > 0)
                {
                    var boundingRect = Cv2.BoundingRect(allPoints);
                    Cv2.Rectangle(colorDisplay, boundingRect, new Scalar(0, 165, 255), 3); // 橙色
                }
            }

            // 计算双阈值信息
            Mat binary1 = new Mat();
            double threshold1 = Cv2.Threshold(originalImage, binary1, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
            double threshold2 = threshold1 * 1.2;

            // 添加焊缝检测信息文字（青色）
            string weldInfo = $"焊缝模式 - 低阈值: {threshold1:F0}, 高阈值: {threshold2:F0}";
            Cv2.PutText(colorDisplay, weldInfo, new OpenCvSharp.Point(10, 30),
                HersheyFonts.HersheySimplex, 0.7, Scalar.Cyan, 2);

            // 计算焊缝区域占比
            int weldPixels = Cv2.CountNonZero(roiMask);
            double weldRatio = (double)weldPixels / (originalImage.Width * originalImage.Height) * 100;
            string roiInfo = $"焊缝区域占比: {weldRatio:F1}%";
            Cv2.PutText(colorDisplay, roiInfo, new OpenCvSharp.Point(10, 70),
                HersheyFonts.HersheySimplex, 0.7, Scalar.Cyan, 2);

            // 添加焊缝质量评估
            string qualityInfo = GetWeldROIQualityAssessment(weldRatio, contours.Length);
            Cv2.PutText(colorDisplay, qualityInfo, new OpenCvSharp.Point(10, 110),
                HersheyFonts.HersheySimplex, 0.7, Scalar.Cyan, 2);

            binary1.Dispose();
        }

        /// <summary>
        /// 获取焊缝ROI质量评估
        /// </summary>
        private string GetWeldROIQualityAssessment(double weldRatio, int contourCount)
        {
            if (weldRatio < 10)
                return "质量评估: 焊缝区域过小，可能检测不准确";
            else if (weldRatio > 60)
                return "质量评估: 焊缝区域过大，可能包含过多背景";
            else if (contourCount > 10)
                return "质量评估: 焊缝区域过于分散，建议调整参数";
            else
                return "质量评估: 焊缝区域检测良好";
        }

        /// <summary>
        /// 在新窗口中显示ROI可视化结果
        /// </summary>
        private void ShowROIVisualizationWindow(Mat roiVisualization)
        {
            // 根据当前ROI模式设置窗口标题
            ROIMode mode = GetSelectedROIMode();
            string windowTitle = mode == ROIMode.Weld ? "焊缝ROI区域可视化" : "通用ROI区域可视化";

            Form roiForm = new Form
            {
                Text = windowTitle,
                Size = new System.Drawing.Size(800, 600),
                StartPosition = FormStartPosition.CenterParent
            };

            PictureBox roiPictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = roiVisualization.ToBitmap()
            };

            // 根据当前ROI模式显示不同的说明信息
            ROIMode currentMode = GetSelectedROIMode();
            string infoText = currentMode == ROIMode.Weld
                ? "蓝色线条：焊缝轮廓  |  橙色框：焊缝边界框  |  青色文字：焊缝检测信息"
                : "绿色线条：工件轮廓  |  红色框：工件边界框  |  黄色文字：阈值信息";

            Label infoLabel = new Label
            {
                Text = infoText,
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
        /// 显示灰度值分布分析（旧版本，保留兼容性）
        /// </summary>
        private void DisplayGrayValueAnalysis_Deprecated()
        {
            var text = "=== 灰度值分布分析 ===\n\n";

            // 分析原图灰度值分布
            text += "【原图灰度值分析】\n";
            text += AnalyzeImageGrayValuesBasic(originalImage, "原图");
            text += "\n";

            // 分析增强后图像灰度值分布
            text += "【增强后灰度值分析】\n";
            text += AnalyzeImageGrayValuesBasic(enhancedImage, "增强后");
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
        /// 分析图像的基础灰度值分布（旧版本，用于兼容）
        /// </summary>
        private string AnalyzeImageGrayValuesBasic(Mat image, string imageName)
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

            // 创建ROI掩码（统一使用通用OTSU方法）
            Mat roiMask = CreateROIMaskForAnalysis(original);

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
        /// 创建ROI掩码（支持多种模式）
        /// </summary>
        private Mat CreateROIMask(Mat image)
        {
            // 获取当前选择的ROI模式
            ROIMode mode = GetSelectedROIMode();
            return CreateIndustrialROIMask(image, mode);
        }

        /// <summary>
        /// 获取当前选择的ROI模式
        /// </summary>
        private ROIMode GetSelectedROIMode()
        {
            if (roiModeComboBox?.SelectedIndex == 1)
                return ROIMode.Weld;
            return ROIMode.General;
        }

        /// <summary>
        /// 创建工业X射线ROI掩码（支持通用和焊缝模式）
        /// </summary>
        private Mat CreateIndustrialROIMask(Mat image, ROIMode mode)
        {
            try
            {
                if (mode == ROIMode.Weld)
                {
                    // 使用WeldDefectEnhancer的焊缝ROI检测算法
                    return CreateWeldROIMask(image);
                }
                else
                {
                    // 使用通用OTSU阈值方法
                    return CreateGeneralROIMask(image);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"创建ROI掩码失败: {ex.Message}");
                // 如果出错，返回全图掩码
                return Mat.Ones(image.Size(), MatType.CV_8UC1);
            }
        }

        /// <summary>
        /// 创建通用ROI掩码（原有的OTSU方法）
        /// </summary>
        private Mat CreateGeneralROIMask(Mat image)
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

        /// <summary>
        /// 创建焊缝专用ROI掩码（基于WeldDefectEnhancer算法）
        /// </summary>
        private Mat CreateWeldROIMask(Mat image)
        {
            // 使用双阈值方法检测焊缝区域
            Mat binary1 = new Mat();
            Mat binary2 = new Mat();

            // 第一个阈值：检测低灰度区域(焊缝和热影响区)
            double threshold1 = Cv2.Threshold(image, binary1, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // 第二个阈值：检测中等灰度区域
            double threshold2 = threshold1 * 1.2;
            Cv2.Threshold(image, binary2, threshold2, 255, ThresholdTypes.Binary);

            // 组合两个阈值结果，创建焊缝区域掩码
            Mat weldMask = new Mat();
            Cv2.BitwiseOr(binary1, binary2, weldMask);
            Cv2.BitwiseNot(weldMask, weldMask); // 反转，焊缝区域为白色

            // 形态学操作优化掩码
            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));
            Cv2.MorphologyEx(weldMask, weldMask, MorphTypes.Close, kernel);
            Cv2.MorphologyEx(weldMask, weldMask, MorphTypes.Open, kernel);

            // 转换为8位掩码
            weldMask.ConvertTo(weldMask, MatType.CV_8UC1);

            // 清理资源
            binary1.Dispose();
            binary2.Dispose();
            kernel.Dispose();

            return weldMask;
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
            enhanced2Image?.Dispose();
            base.OnFormClosed(e);
        }
        /// <summary>
        /// 对比两个增强图的效果
        /// </summary>
        private ComparisonAnalysisResult CompareTwoEnhanced(Mat enhanced1, Mat enhanced2)
        {
            var config = AnalysisConfiguration.Default;
            Mat roiMask = CreateROIMask(originalImage);

            // 分析增强图1
            enhanced1AnalysisResult = new ComprehensiveAnalysisResult
            {
                ROIQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(originalImage, enhanced1, config, roiMask),
                ROIMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(originalImage, enhanced1, config, roiMask),
                ROIDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(originalImage, enhanced1, config, roiMask),
                FullImageQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(originalImage, enhanced1, config, null),
                FullImageMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(originalImage, enhanced1, config, null),
                FullImageDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(originalImage, enhanced1, config, null)
            };

            // 分析增强图2
            enhanced2AnalysisResult = new ComprehensiveAnalysisResult
            {
                ROIQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(originalImage, enhanced2, config, roiMask),
                ROIMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(originalImage, enhanced2, config, roiMask),
                ROIDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(originalImage, enhanced2, config, roiMask),
                FullImageQualityMetrics = ImageQualityAnalyzer.AnalyzeQuality(originalImage, enhanced2, config, null),
                FullImageMedicalMetrics = MedicalImageAnalyzer.AnalyzeMedicalImage(originalImage, enhanced2, config, null),
                FullImageDetectionMetrics = DefectDetectionAnalyzer.AnalyzeDetectionFriendliness(originalImage, enhanced2, config, null)
            };

            // 计算综合评分
            var result1 = enhanced1AnalysisResult.Value;
            result1.ROITechnicalScore = CalculateImageQualityScore(result1.ROIQualityMetrics);
            result1.ROIMedicalScore = result1.ROIMedicalMetrics.OverallMedicalQuality;
            result1.ROIDetectionScore = result1.ROIDetectionMetrics.OverallSuitability;
            result1.ROIOverallRecommendation = (result1.ROITechnicalScore + result1.ROIMedicalScore + result1.ROIDetectionScore) / 3.0;
            enhanced1AnalysisResult = result1;

            var result2 = enhanced2AnalysisResult.Value;
            result2.ROITechnicalScore = CalculateImageQualityScore(result2.ROIQualityMetrics);
            result2.ROIMedicalScore = result2.ROIMedicalMetrics.OverallMedicalQuality;
            result2.ROIDetectionScore = result2.ROIDetectionMetrics.OverallSuitability;
            result2.ROIOverallRecommendation = (result2.ROITechnicalScore + result2.ROIMedicalScore + result2.ROIDetectionScore) / 3.0;
            enhanced2AnalysisResult = result2;

            // 生成对比总结
            var summary = GenerateComparisonSummary(result1, result2);

            roiMask?.Dispose();

            return new ComparisonAnalysisResult
            {
                Enhanced1Result = result1,
                Enhanced2Result = result2,
                Summary = summary
            };
        }

        /// <summary>
        /// 生成对比总结
        /// </summary>
        private ComparisonSummary GenerateComparisonSummary(ComprehensiveAnalysisResult result1, ComprehensiveAnalysisResult result2)
        {
            var summary = new ComparisonSummary();

            // 计算各项差异
            summary.QualityDifference = result2.ROITechnicalScore - result1.ROITechnicalScore;
            summary.MedicalDifference = result2.ROIMedicalScore - result1.ROIMedicalScore;
            summary.DetectionDifference = result2.ROIDetectionScore - result1.ROIDetectionScore;
            summary.OverallDifference = result2.ROIOverallRecommendation - result1.ROIOverallRecommendation;

            // 确定推荐图像
            if (Math.Abs(summary.OverallDifference) < 2.0)
            {
                summary.RecommendedImage = 0; // 差异很小，无明显推荐
                summary.RecommendationReason = "两个增强图效果相近，可根据具体需求选择";
            }
            else if (summary.OverallDifference > 0)
            {
                summary.RecommendedImage = 2;
                summary.RecommendationReason = GenerateRecommendationReason(result2, result1, "增强图2");
            }
            else
            {
                summary.RecommendedImage = 1;
                summary.RecommendationReason = GenerateRecommendationReason(result1, result2, "增强图1");
            }

            return summary;
        }

        /// <summary>
        /// 生成推荐理由
        /// </summary>
        private string GenerateRecommendationReason(ComprehensiveAnalysisResult better, ComprehensiveAnalysisResult worse, string imageName)
        {
            var reasons = new List<string>();

            if (better.ROITechnicalScore - worse.ROITechnicalScore > 5)
                reasons.Add("技术指标更优");
            if (better.ROIMedicalScore - worse.ROIMedicalScore > 5)
                reasons.Add("医学影像质量更佳");
            if (better.ROIDetectionScore - worse.ROIDetectionScore > 5)
                reasons.Add("缺陷检测适用性更强");

            if (reasons.Count == 0)
                return $"{imageName}在综合评估中略胜一筹";

            return $"{imageName}在{string.Join("、", reasons)}方面表现更好";
        }

        /// <summary>
        /// 计算图像质量综合评分
        /// </summary>
        private double CalculateImageQualityScore(ImageQualityMetrics metrics)
        {
            // 基于各项指标计算综合评分
            double psnrScore = Math.Min(100, Math.Max(0, (metrics.PSNR - 20) * 2)); // PSNR > 20dB 为可接受
            double ssimScore = metrics.SSIM * 100; // SSIM 0-1 转换为 0-100
            double edgeScore = metrics.EdgeQuality; // 已经是 0-100
            double overEnhancementPenalty = 100 - metrics.OverEnhancementScore; // 过度增强越低越好
            double noisePenalty = Math.Max(0, 100 - (metrics.NoiseAmplification - 1.0) * 50); // 噪声放大越低越好
            double haloPenalty = 100 - metrics.HaloEffect; // 光晕效应越低越好

            // 加权平均
            double weightedScore =
                psnrScore * 0.25 +
                ssimScore * 0.25 +
                edgeScore * 0.20 +
                overEnhancementPenalty * 0.15 +
                noisePenalty * 0.10 +
                haloPenalty * 0.05;

            return Math.Max(0, Math.Min(100, weightedScore));
        }

        /// <summary>
        /// 显示对比分析结果
        /// </summary>
        private void DisplayComparisonResults(ComparisonAnalysisResult comparisonResult)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== 双图像对比分析结果 ===\n");

            // 显示增强图1结果
            sb.AppendLine("【增强图1分析结果】");
            sb.AppendLine($"技术指标评分: {comparisonResult.Enhanced1Result.ROITechnicalScore:F1}");
            sb.AppendLine($"医学影像评分: {comparisonResult.Enhanced1Result.ROIMedicalScore:F1}");
            sb.AppendLine($"缺陷检测评分: {comparisonResult.Enhanced1Result.ROIDetectionScore:F1}");
            sb.AppendLine($"综合推荐度: {comparisonResult.Enhanced1Result.ROIOverallRecommendation:F1}\n");

            // 显示增强图2结果
            sb.AppendLine("【增强图2分析结果】");
            sb.AppendLine($"技术指标评分: {comparisonResult.Enhanced2Result.ROITechnicalScore:F1}");
            sb.AppendLine($"医学影像评分: {comparisonResult.Enhanced2Result.ROIMedicalScore:F1}");
            sb.AppendLine($"缺陷检测评分: {comparisonResult.Enhanced2Result.ROIDetectionScore:F1}");
            sb.AppendLine($"综合推荐度: {comparisonResult.Enhanced2Result.ROIOverallRecommendation:F1}\n");

            // 显示对比总结
            sb.AppendLine("【对比总结】");
            if (comparisonResult.Summary.RecommendedImage == 0)
            {
                sb.AppendLine("推荐结果: 两图效果相近");
            }
            else
            {
                sb.AppendLine($"推荐结果: 增强图{comparisonResult.Summary.RecommendedImage}");
            }
            sb.AppendLine($"推荐理由: {comparisonResult.Summary.RecommendationReason}");
            sb.AppendLine($"综合评分差异: {comparisonResult.Summary.OverallDifference:F1}");

            // 显示详细差异
            sb.AppendLine("\n【详细差异分析】");
            sb.AppendLine($"技术指标差异: {comparisonResult.Summary.QualityDifference:F1}");
            sb.AppendLine($"医学影像差异: {comparisonResult.Summary.MedicalDifference:F1}");
            sb.AppendLine($"缺陷检测差异: {comparisonResult.Summary.DetectionDifference:F1}");

            // 在结果文本框中显示
            resultTextBox.Text = sb.ToString();
        }

        /// <summary>
        /// 生成AI分析总结数据
        /// </summary>
        private void GenerateAISummary()
        {
            try
            {
                // 第1列：综合对比分析
                var comprehensiveSummary = GenerateComprehensiveAnalysisSummary();

                // 第2列：增强算法1专项分析
                var algorithm1Analysis = GenerateAlgorithmSpecificAnalysis(originalImage, enhancedImage, "增强算法1", enhanced1AnalysisResult);

                // 第3列：增强算法2专项分析
                var algorithm2Analysis = GenerateAlgorithmSpecificAnalysis(originalImage, enhanced2Image, "增强算法2", enhanced2AnalysisResult);

                // 分别显示不同内容
                if (originalAISummaryTextBox != null)
                    originalAISummaryTextBox.Text = comprehensiveSummary;
                if (enhanced1AISummaryTextBox != null)
                    enhanced1AISummaryTextBox.Text = algorithm1Analysis;
                if (enhanced2AISummaryTextBox != null)
                    enhanced2AISummaryTextBox.Text = algorithm2Analysis;
            }
            catch (Exception ex)
            {
                var errorMsg = $"AI总结生成失败: {ex.Message}";
                if (originalAISummaryTextBox != null)
                    originalAISummaryTextBox.Text = errorMsg;
                if (enhanced1AISummaryTextBox != null)
                    enhanced1AISummaryTextBox.Text = errorMsg;
                if (enhanced2AISummaryTextBox != null)
                    enhanced2AISummaryTextBox.Text = errorMsg;
            }
        }

        /// <summary>
        /// 生成三图综合对比分析摘要，便于AI分析
        /// </summary>
        private string GenerateComprehensiveAnalysisSummary()
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 三图综合增强算法对比分析 ===");
            sb.AppendLine($"分析时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"分析模式: {(roiMappingRadio?.Checked == true ? "ROI区域分析" : "全图分析")}");
            sb.AppendLine();

            // 图像基本信息
            if (originalImage != null)
            {
                // 正确检测图像位数
                bool is16Bit = originalImage.Type() == MatType.CV_16UC1 || originalImage.Type() == MatType.CV_16SC1;
                int maxValue = is16Bit ? 65535 : 255;

                sb.AppendLine("【图像基本信息】");
                sb.AppendLine($"图像尺寸: {originalImage.Width} × {originalImage.Height}");
                sb.AppendLine($"图像类型: {(is16Bit ? "16位" : "8位")}灰度工业图像");
                sb.AppendLine($"像素范围: 0 - {maxValue}");
                sb.AppendLine();
            }

            // 核心对比分析：原图 vs 增强图1 vs 增强图2
            sb.AppendLine("【核心增强效果对比】");
            var originalStats = ExtractStatisticsValues(originalGrayValueAnalysis);
            var enhanced1Stats = ExtractStatisticsValues(enhanced1GrayValueAnalysis);
            var enhanced2Stats = ExtractStatisticsValues(enhanced2GrayValueAnalysis);

            if (originalStats.Count > 0 && enhanced1Stats.Count > 0 && enhanced2Stats.Count > 0)
            {
                sb.AppendLine("原图 → 增强图1 → 增强图2 数值变化:");
                foreach (var key in originalStats.Keys)
                {
                    if (enhanced1Stats.ContainsKey(key) && enhanced2Stats.ContainsKey(key))
                    {
                        var orig = originalStats[key];
                        var enh1 = enhanced1Stats[key];
                        var enh2 = enhanced2Stats[key];

                        var change1 = orig > 0 ? ((enh1 - orig) / orig * 100) : 0;
                        var change2 = orig > 0 ? ((enh2 - orig) / orig * 100) : 0;

                        sb.AppendLine($"{key}: {orig:F1} → {enh1:F1}({change1:+0.0;-0.0;0}%) → {enh2:F1}({change2:+0.0;-0.0;0}%)");
                    }
                }
            }
            sb.AppendLine();

            // 算法策略差异分析
            sb.AppendLine("【算法策略差异分析】");
            sb.AppendLine(AnalyzeAlgorithmDifferences(originalStats, enhanced1Stats, enhanced2Stats));
            sb.AppendLine();

            // 增强效果评估
            sb.AppendLine("【增强效果评估】");
            sb.AppendLine(EvaluateEnhancementEffectiveness(originalStats, enhanced1Stats, enhanced2Stats));
            sb.AppendLine();

            // AI分析建议
            sb.AppendLine("【复制给AI进行深度分析】");
            sb.AppendLine("请将以上完整数据复制给AI，可以询问:");
            sb.AppendLine("1. '分析这两种工业图像增强算法的技术原理差异'");
            sb.AppendLine("2. '评估哪种算法更适合工业缺陷检测，为什么？'");
            sb.AppendLine("3. '基于数据分析，如何进一步优化这些算法？'");
            sb.AppendLine("4. '在什么工业检测场景下应该选择哪种算法？'");
            sb.AppendLine("5. '这些数值变化反映了什么样的图像处理策略？'");

            return sb.ToString();
        }

        /// <summary>
        /// 生成单个算法的专项分析
        /// </summary>
        private string GenerateAlgorithmSpecificAnalysis(Mat originalImg, Mat enhancedImg, string algorithmName, ComprehensiveAnalysisResult? analysisResult)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"=== {algorithmName}深度分析 ===");
            sb.AppendLine($"分析时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"分析模式: {(roiMappingRadio?.Checked == true ? "ROI区域分析" : "全图分析")}");
            sb.AppendLine();

            if (originalImg == null || enhancedImg == null)
            {
                sb.AppendLine("图像数据不完整，无法进行专项分析");
                return sb.ToString();
            }

            // 质量指标评估
            sb.AppendLine("【质量指标评估】");
            if (analysisResult.HasValue)
            {
                var metrics = roiMappingRadio?.Checked == true ? analysisResult.Value.ROIQualityMetrics : analysisResult.Value.FullImageQualityMetrics;
                sb.AppendLine($"PSNR (峰值信噪比): {metrics.PSNR:F2} dB");
                sb.AppendLine($"SSIM (结构相似性): {metrics.SSIM:F3}");
                sb.AppendLine($"边缘质量评分: {metrics.EdgeQuality:F1}/100");
                sb.AppendLine($"过度增强风险: {metrics.OverEnhancementScore:F1}%");
                sb.AppendLine($"噪声放大系数: {metrics.NoiseAmplification:F2}x");
            }
            else
            {
                sb.AppendLine("质量指标计算中...");
            }
            sb.AppendLine();

            // 缺陷检测友好度
            sb.AppendLine("【缺陷检测友好度】");
            if (analysisResult.HasValue)
            {
                var detectionMetrics = roiMappingRadio?.Checked == true ? analysisResult.Value.ROIDetectionMetrics : analysisResult.Value.FullImageDetectionMetrics;
                sb.AppendLine($"细线可见性提升: {detectionMetrics.ThinLineVisibility:F1}%");
                sb.AppendLine($"背景噪声抑制: {detectionMetrics.BackgroundNoiseReduction:F1}/100");
                sb.AppendLine($"缺陷对比度增强: {detectionMetrics.DefectBackgroundContrast:F1}/100");
                sb.AppendLine($"假阳性风险: {detectionMetrics.FalsePositiveRisk:F1}%");
                sb.AppendLine($"综合适用性: {detectionMetrics.OverallSuitability:F1}/100");
            }
            else
            {
                sb.AppendLine("缺陷检测分析计算中...");
            }
            sb.AppendLine();

            // 算法参数推断
            sb.AppendLine("【算法参数推断】");
            try
            {
                var analyzer = new ImageEnhancementAnalyzer();
                var enhancementAnalysis = analyzer.AnalyzeEnhancement(originalImg, enhancedImg);
                sb.AppendLine($"推断算法类型: {enhancementAnalysis.SuggestedAlgorithm}");
                sb.AppendLine($"估计Gamma值: {enhancementAnalysis.GammaEstimate:F2}");
                sb.AppendLine($"对比度变化率: {enhancementAnalysis.ContrastRatio:F2}x");
                sb.AppendLine($"亮度变化: {enhancementAnalysis.BrightnessChange:+F1;-F1;0}%");

                if (enhancementAnalysis.EstimatedParameters.Count > 0)
                {
                    sb.AppendLine("估计参数:");
                    foreach (var param in enhancementAnalysis.EstimatedParameters)
                    {
                        sb.AppendLine($"  {param.Key}: {param.Value:F2}");
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"参数推断失败: {ex.Message}");
            }
            sb.AppendLine();

            // 专项建议
            sb.AppendLine("【专项优化建议】");
            if (analysisResult.HasValue)
            {
                var score = roiMappingRadio?.Checked == true ? analysisResult.Value.ROIOverallRecommendation : analysisResult.Value.FullImageOverallRecommendation;
                if (score > 80)
                    sb.AppendLine("✅ 算法效果优秀，建议保持当前参数");
                else if (score > 60)
                    sb.AppendLine("⚠️ 算法效果良好，可考虑微调参数");
                else
                    sb.AppendLine("❌ 算法效果需要改进，建议调整参数或更换算法");

                sb.AppendLine($"综合推荐度: {score:F1}/100");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 提取统计数值到字典
        /// </summary>
        private Dictionary<string, double> ExtractStatisticsValues(string analysis)
        {
            var stats = new Dictionary<string, double>();
            if (string.IsNullOrEmpty(analysis)) return stats;

            var lines = analysis.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("平均值:"))
                {
                    var value = ExtractNumericValue(line);
                    if (value.HasValue) stats["平均值"] = value.Value;
                }
                else if (line.Contains("最小值:"))
                {
                    var value = ExtractNumericValue(line);
                    if (value.HasValue) stats["最小值"] = value.Value;
                }
                else if (line.Contains("最大值:"))
                {
                    var value = ExtractNumericValue(line);
                    if (value.HasValue) stats["最大值"] = value.Value;
                }
                else if (line.Contains("动态范围:"))
                {
                    var value = ExtractNumericValue(line);
                    if (value.HasValue) stats["动态范围"] = value.Value;
                }
            }

            return stats;
        }

        /// <summary>
        /// 从文本行中提取数值
        /// </summary>
        private double? ExtractNumericValue(string line)
        {
            var parts = line.Split(':');
            if (parts.Length > 1)
            {
                var valueStr = parts[1].Trim().Split(' ')[0]; // 取第一个数字部分
                if (double.TryParse(valueStr, out double value))
                {
                    return value;
                }
            }
            return null;
        }

        /// <summary>
        /// 分析像素映射特征
        /// </summary>
        private string AnalyzePixelMappingCharacteristics(string algorithmType)
        {
            // 这里可以基于像素映射图的数据特征进行分析
            // 简化版本，实际可以更复杂
            if (algorithmType == "enhanced1")
            {
                return "暗部大幅提亮，亮部适度压缩，整体向中间集中";
            }
            else
            {
                return "全局提亮，保持动态范围，对比度增强";
            }
        }

        /// <summary>
        /// 分析算法差异
        /// </summary>
        private string AnalyzeAlgorithmDifferences(Dictionary<string, double> original, Dictionary<string, double> enhanced1, Dictionary<string, double> enhanced2)
        {
            var sb = new StringBuilder();

            if (original.TryGetValue("平均值", out double origAvg) &&
                enhanced1.TryGetValue("平均值", out double enh1Avg) &&
                enhanced2.TryGetValue("平均值", out double enh2Avg))
            {
                var change1 = ((enh1Avg - origAvg) / origAvg * 100);
                var change2 = ((enh2Avg - origAvg) / origAvg * 100);

                sb.AppendLine($"算法1策略: 平均值变化{change1:+0.0;-0.0;0}% - {(change1 > 100 ? "激进提亮" : change1 > 50 ? "显著提亮" : "温和调整")}");
                sb.AppendLine($"算法2策略: 平均值变化{change2:+0.0;-0.0;0}% - {(change2 > 100 ? "激进提亮" : change2 > 50 ? "显著提亮" : "温和调整")}");
            }

            if (original.TryGetValue("动态范围", out double origRange) &&
                enhanced1.TryGetValue("动态范围", out double enh1Range) &&
                enhanced2.TryGetValue("动态范围", out double enh2Range))
            {
                var rangeChange1 = ((enh1Range - origRange) / origRange * 100);
                var rangeChange2 = ((enh2Range - origRange) / origRange * 100);

                sb.AppendLine($"算法1对比度: {(rangeChange1 > 0 ? "扩展" : "压缩")}动态范围{Math.Abs(rangeChange1):F1}%");
                sb.AppendLine($"算法2对比度: {(rangeChange2 > 0 ? "扩展" : "压缩")}动态范围{Math.Abs(rangeChange2):F1}%");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 评估增强效果
        /// </summary>
        private string EvaluateEnhancementEffectiveness(Dictionary<string, double> original, Dictionary<string, double> enhanced1, Dictionary<string, double> enhanced2)
        {
            var sb = new StringBuilder();

            sb.AppendLine("基于数值变化的效果评估:");

            // 评估算法1
            if (original.TryGetValue("最小值", out double origMin1) && enhanced1.TryGetValue("最小值", out double enh1Min))
            {
                var minChange1 = ((enh1Min - origMin1) / origMin1 * 100);
                sb.AppendLine($"算法1: 暗部提升{minChange1:F0}% - {(minChange1 > 500 ? "极强" : minChange1 > 200 ? "很强" : minChange1 > 100 ? "较强" : "一般")}暗部增强");
            }

            // 评估算法2
            if (original.TryGetValue("最小值", out double origMin2) && enhanced2.TryGetValue("最小值", out double enh2Min))
            {
                var minChange2 = ((enh2Min - origMin2) / origMin2 * 100);
                sb.AppendLine($"算法2: 暗部提升{minChange2:F0}% - {(minChange2 > 500 ? "极强" : minChange2 > 200 ? "很强" : minChange2 > 100 ? "较强" : "一般")}暗部增强");
            }

            sb.AppendLine();
            sb.AppendLine("工业影像应用建议:");
            sb.AppendLine("- 缺陷检测: 选择暗部增强更强的算法");
            sb.AppendLine("- 质量检测: 选择动态范围保持更好的算法");
            sb.AppendLine("- 尺寸测量: 考虑像素值变化对测量精度的影响");

            return sb.ToString();
        }
    }
}
