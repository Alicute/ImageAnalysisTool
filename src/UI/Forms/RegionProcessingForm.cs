using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ImageAnalysisTool.Core.Processors;
using NLog;
using OpenCvSharp;
using FellowOakDicom;

namespace ImageAnalysisTool.UI.Forms
{
    /// <summary>
    /// 区域处理窗口 - 提供高级图像增强功能
    /// </summary>
    public partial class RegionProcessingForm : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // 图像数据
        private Mat originalImage;
        private Mat processedImage;

        // UI 控件
        private PictureBox originalPictureBox;
        private PictureBox processedPictureBox;
        private Panel controlPanel; // 用于放置所有处理控件

        // 灰度变换控件
        private TrackBar gammaTrackBar;
        private Label gammaValueLabel;
        private TrackBar contrastLowTrackBar;
        private Label contrastLowValueLabel;
        private TrackBar contrastHighTrackBar;
        private Label contrastHighValueLabel;

        // 直方图调整控件
        private TrackBar claheClipLimitTrackBar;
        private Label claheClipLimitValueLabel;
        private TrackBar claheGridSizeTrackBar;
        private Label claheGridSizeValueLabel;

        public RegionProcessingForm(Mat image)
        {
            if (image == null || image.Empty())
            {
                throw new ArgumentNullException(nameof(image), "输入的图像不能为空");
            }

            originalImage = image.Clone();
            processedImage = image.Clone();

            InitializeComponent();
            InitializeCustomComponents();
            
            logger.Info("区域处理窗口初始化完成");
        }

        public RegionProcessingForm()
        {
            InitializeComponent();
            InitializeCustomComponents();
            
            // 创建一个默认的空图像
            originalImage = Mat.Zeros(512, 512, MatType.CV_16UC1);
            processedImage = originalImage.Clone();
            
            // 加载初始图像
            LoadImages();
            
            logger.Info("区域处理窗口初始化完成（无图像模式）");
        }

        private void InitializeComponent()
        {
            this.Text = "高级区域处理工具";
            this.Size = new System.Drawing.Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
        }
        
        private void LoadMenuItem_Click(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "图像文件|*.dcm;*.dic;*.acr;*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.tif";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        // 释放旧图像
                        originalImage?.Dispose();
                        processedImage?.Dispose();
                        
                        // 加载新图像
                        originalImage = LoadImageFile(dialog.FileName);
                        processedImage = originalImage.Clone();
                        
                        // 更新显示
                        LoadImages();
                        
                        logger.Info($"成功加载图像: {dialog.FileName}");
                        // 不显示提示信息，静默加载图像
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "加载图像失败");
                        MessageBox.Show($"加载图像失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        
        private void SaveMenuItem_Click(object sender, EventArgs e)
        {
            if (processedImage == null || processedImage.Empty())
            {
                // 不显示提示信息，静默返回
                return;
            }
            
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "TIFF图像|*.tiff;*.tif|PNG图像|*.png|所有文件|*.*";
                dialog.DefaultExt = "tiff";
                dialog.FileName = $"处理结果_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        processedImage.SaveImage(dialog.FileName);
                        logger.Info($"保存处理结果: {dialog.FileName}");
                        // 不显示提示信息，静默保存图像
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "保存图像失败");
                        MessageBox.Show($"保存图像失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
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

        private void InitializeCustomComponents()
        {
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F)); // 图像显示区
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F)); // 控制区
            this.Controls.Add(mainLayout);

            // 图像显示面板
            var imageLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            imageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            
            originalPictureBox = CreatePictureBox("原图");
            processedPictureBox = CreatePictureBox("处理后");
            
            imageLayout.Controls.Add(CreateLabeledPanel(originalPictureBox, "原图"), 0, 0);
            imageLayout.Controls.Add(CreateLabeledPanel(processedPictureBox, "处理后"), 1, 0);

            mainLayout.Controls.Add(imageLayout, 0, 0);

            // 控制面板
            controlPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true
            };

            // 创建一个垂直布局面板来放置按钮和选项卡控件
            var controlLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            controlLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // 按钮区域
            controlLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // 选项卡区域

            // 创建按钮面板
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            var loadImageButton = new Button
            {
                Text = "加载图像",
                Location = new System.Drawing.Point(5, 10),
                Size = new System.Drawing.Size(80, 30)
            };
            loadImageButton.Click += LoadMenuItem_Click;

            var saveImageButton = new Button
            {
                Text = "保存图像",
                Location = new System.Drawing.Point(90, 10),
                Size = new System.Drawing.Size(80, 30)
            };
            saveImageButton.Click += SaveMenuItem_Click;

            buttonPanel.Controls.Add(loadImageButton);
            buttonPanel.Controls.Add(saveImageButton);

            // 创建选项卡控件
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            
            string[] tabNames = { "灰度变换", "直方图调整", "空间域滤波", "频域增强", "噪声与边缘", "色彩处理" };
            foreach (var name in tabNames)
            {
                var tabPage = new TabPage(name);
                if (name == "灰度变换")
                {
                    InitializeGrayscaleTab(tabPage);
                }
                else if (name == "直方图调整")
                {
                    InitializeHistogramTab(tabPage);
                }
                else if (name == "空间域滤波")
                {
                    InitializeSpatialFilterTab(tabPage);
                }
                else if (name == "频域增强")
                {
                    InitializeFrequencyDomainTab(tabPage);
                }
                else if (name == "噪声与边缘")
                {
                    InitializeNoiseAndEdgeTab(tabPage);
                }
                tabControl.TabPages.Add(tabPage);
            }

            // 将按钮面板和选项卡控件添加到控制布局中
            controlLayout.Controls.Add(buttonPanel, 0, 0);
            controlLayout.Controls.Add(tabControl, 0, 1);

            controlPanel.Controls.Add(controlLayout);
            mainLayout.Controls.Add(controlPanel, 1, 0);

            // 加载初始图像
            LoadImages();
        }

        private PictureBox CreatePictureBox(string name)
        {
            return new PictureBox
            {
                Name = name,
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };
        }
        
        private Panel CreateLabeledPanel(PictureBox pictureBox, string labelText)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var label = new Label
            {
                Text = labelText,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 25,
                BackColor = Color.LightGray
            };
            panel.Controls.Add(pictureBox);
            panel.Controls.Add(label);
            return panel;
        }

        private void LoadImages()
        {
            try
            {
                originalPictureBox.Image = ConvertMatToBitmap(originalImage);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
            }
            catch(Exception ex)
            {
                logger.Error(ex, "加载图像到预览窗口失败");
                MessageBox.Show($"加载图像失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private Bitmap ConvertMatToBitmap(Mat mat)
        {
            if (mat == null || mat.Empty()) return null;

            // 确保为8位用于显示
            Mat displayMat = new Mat();
            if (mat.Type() == MatType.CV_16UC1)
            {
                mat.ConvertTo(displayMat, MatType.CV_8UC1, 255.0 / 65535.0);
            }
            else
            {
                displayMat = mat.Clone();
            }

            Bitmap bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(displayMat);
            displayMat.Dispose();
            return bmp;
        }

        private void InitializeGrayscaleTab(TabPage grayscaleTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4, // 伽马，对比度，对数，重置
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 伽马校正
            var gammaGroup = new GroupBox { Text = "伽马校正 (Gamma Correction)", Dock = DockStyle.Fill };
            gammaValueLabel = new Label { Text = "Gamma: 1.0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            gammaTrackBar = new TrackBar { Minimum = 1, Maximum = 50, Value = 10, Location = new System.Drawing.Point(110, 20), Width = 200 };
            gammaTrackBar.Scroll += (s, e) => {
                double gamma = gammaTrackBar.Value / 10.0;
                gammaValueLabel.Text = $"Gamma: {gamma:F1}";
                processedImage = RegionProcessor.ApplyGammaCorrection(originalImage, gamma);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
            };
            gammaGroup.Controls.Add(gammaValueLabel);
            gammaGroup.Controls.Add(gammaTrackBar);
            layout.Controls.Add(gammaGroup, 0, 0);
            
            // 2. 对比度拉伸
            var contrastGroup = new GroupBox { Text = "对比度拉伸 (Contrast Stretching)", Dock = DockStyle.Fill };
            contrastLowValueLabel = new Label { Text = "下限: 0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            contrastLowTrackBar = new TrackBar { Minimum = 0, Maximum = 65535, Value = 0, Location = new System.Drawing.Point(110, 20), Width = 200 };
            contrastHighValueLabel = new Label { Text = "上限: 65535", Location = new System.Drawing.Point(10, 55), Width = 100 };
            contrastHighTrackBar = new TrackBar { Minimum = 0, Maximum = 65535, Value = 65535, Location = new System.Drawing.Point(110, 50), Width = 200 };

            contrastLowTrackBar.Scroll += ContrastTrackBar_Scroll;
            contrastHighTrackBar.Scroll += ContrastTrackBar_Scroll;

            contrastGroup.Controls.Add(contrastLowValueLabel);
            contrastGroup.Controls.Add(contrastLowTrackBar);
            contrastGroup.Controls.Add(contrastHighValueLabel);
            contrastGroup.Controls.Add(contrastHighTrackBar);
            layout.Controls.Add(contrastGroup, 0, 1);

            // 3. 对数变换
            var logGroup = new GroupBox { Text = "对数变换 (Log Transform)", Dock = DockStyle.Fill };
            var applyLogBtn = new Button { Text = "应用对数变换", Location = new System.Drawing.Point(10, 20) };
            applyLogBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyLogTransform(originalImage);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info("应用对数变换");
            };
            logGroup.Controls.Add(applyLogBtn);
            layout.Controls.Add(logGroup, 0, 2);

            // 4. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                processedImage = originalImage.Clone();
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                gammaTrackBar.Value = 10;
                gammaValueLabel.Text = "Gamma: 1.0";
                contrastLowTrackBar.Value = 0;
                contrastHighTrackBar.Value = 65535;
                contrastLowValueLabel.Text = "下限: 0";
                contrastHighValueLabel.Text = "上限: 65535";
                logger.Info("灰度变换已重置");
            };
            layout.Controls.Add(resetBtn, 0, 3);

            grayscaleTab.Controls.Add(layout);
        }

        private void InitializeHistogramTab(TabPage histogramTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3, // 全局均衡化, CLAHE, 重置
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 全局直方图均衡化
            var heqGroup = new GroupBox { Text = "全局直方图均衡化 (HE)", Dock = DockStyle.Fill };
            var applyHeqBtn = new Button { Text = "应用全局均衡化", Location = new System.Drawing.Point(10, 20) };
            applyHeqBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyHistogramEqualization(originalImage);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info("应用全局直方图均衡化");
            };
            heqGroup.Controls.Add(applyHeqBtn);
            layout.Controls.Add(heqGroup, 0, 0);

            // 2. 自适应均衡化 (CLAHE)
            var claheGroup = new GroupBox { Text = "自适应均衡化 (CLAHE)", Dock = DockStyle.Fill };
            claheClipLimitValueLabel = new Label { Text = "Clip Limit: 2.0", Location = new System.Drawing.Point(10, 25), Width = 120 };
            claheClipLimitTrackBar = new TrackBar { Minimum = 1, Maximum = 100, Value = 20, Location = new System.Drawing.Point(130, 20), Width = 180 };
            claheGridSizeValueLabel = new Label { Text = "Grid Size: 8x8", Location = new System.Drawing.Point(10, 55), Width = 120 };
            claheGridSizeTrackBar = new TrackBar { Minimum = 2, Maximum = 32, Value = 8, Location = new System.Drawing.Point(130, 50), Width = 180 };
            var applyClaheBtn = new Button { Text = "应用CLAHE", Location = new System.Drawing.Point(10, 85) };

            claheClipLimitTrackBar.Scroll += (s, e) => {
                double clipLimit = claheClipLimitTrackBar.Value / 10.0;
                claheClipLimitValueLabel.Text = $"Clip Limit: {clipLimit:F1}";
            };
            claheGridSizeTrackBar.Scroll += (s, e) => {
                int gridSize = claheGridSizeTrackBar.Value;
                claheGridSizeValueLabel.Text = $"Grid Size: {gridSize}x{gridSize}";
            };
            applyClaheBtn.Click += (s, e) => {
                double clipLimit = claheClipLimitTrackBar.Value / 10.0;
                int gridSize = claheGridSizeTrackBar.Value;
                processedImage = RegionProcessor.ApplyCLAHE(originalImage, clipLimit, gridSize);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用CLAHE: ClipLimit={clipLimit}, GridSize={gridSize}");
            };

            claheGroup.Controls.Add(claheClipLimitValueLabel);
            claheGroup.Controls.Add(claheClipLimitTrackBar);
            claheGroup.Controls.Add(claheGridSizeValueLabel);
            claheGroup.Controls.Add(claheGridSizeTrackBar);
            claheGroup.Controls.Add(applyClaheBtn);
            layout.Controls.Add(claheGroup, 0, 1);
            
            // 3. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                processedImage = originalImage.Clone();
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                claheClipLimitTrackBar.Value = 20;
                claheGridSizeTrackBar.Value = 8;
                claheClipLimitValueLabel.Text = "Clip Limit: 2.0";
                claheGridSizeValueLabel.Text = "Grid Size: 8x8";
                logger.Info("直方图调整已重置");
            };
            layout.Controls.Add(resetBtn, 0, 2);

            histogramTab.Controls.Add(layout);
        }

        private void ContrastTrackBar_Scroll(object sender, EventArgs e)
        {
            if (contrastLowTrackBar.Value >= contrastHighTrackBar.Value)
            {
                if (sender == contrastLowTrackBar)
                    contrastLowTrackBar.Value = contrastHighTrackBar.Value - 1;
                else
                    contrastHighTrackBar.Value = contrastLowTrackBar.Value + 1;
            }

            int low = contrastLowTrackBar.Value;
            int high = contrastHighTrackBar.Value;

            contrastLowValueLabel.Text = $"下限: {low}";
            contrastHighValueLabel.Text = $"上限: {high}";
            
            processedImage = RegionProcessor.ApplyContrastStretching(originalImage, low, high);
            processedPictureBox.Image = ConvertMatToBitmap(processedImage);
        }

        private void InitializeSpatialFilterTab(TabPage spatialFilterTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4, // 均值滤波，中值滤波，拉普拉斯锐化，重置
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 均值滤波
            var meanFilterGroup = new GroupBox { Text = "均值滤波 (Mean Filter)", Dock = DockStyle.Fill };
            var meanFilterLabel = new Label { Text = "核大小: 3x3", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var meanFilterTrackBar = new TrackBar { Minimum = 3, Maximum = 15, Value = 3, Location = new System.Drawing.Point(110, 20), Width = 200 };
            meanFilterTrackBar.TickFrequency = 2; // 只允许奇数
            meanFilterTrackBar.SmallChange = 2;
            meanFilterTrackBar.LargeChange = 2;
            
            meanFilterTrackBar.Scroll += (s, e) => {
                // 确保值为奇数
                if (meanFilterTrackBar.Value % 2 == 0)
                {
                    meanFilterTrackBar.Value += 1;
                }
                meanFilterLabel.Text = $"核大小: {meanFilterTrackBar.Value}x{meanFilterTrackBar.Value}";
            };
            
            var applyMeanFilterBtn = new Button { Text = "应用均值滤波", Location = new System.Drawing.Point(10, 55) };
            applyMeanFilterBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyMeanFilter(originalImage, meanFilterTrackBar.Value);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用均值滤波: 核大小={meanFilterTrackBar.Value}");
            };
            
            meanFilterGroup.Controls.Add(meanFilterLabel);
            meanFilterGroup.Controls.Add(meanFilterTrackBar);
            meanFilterGroup.Controls.Add(applyMeanFilterBtn);
            layout.Controls.Add(meanFilterGroup, 0, 0);

            // 2. 中值滤波
            var medianFilterGroup = new GroupBox { Text = "中值滤波 (Median Filter)", Dock = DockStyle.Fill };
            var medianFilterLabel = new Label { Text = "核大小: 3x3", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var medianFilterTrackBar = new TrackBar { Minimum = 3, Maximum = 15, Value = 3, Location = new System.Drawing.Point(110, 20), Width = 200 };
            medianFilterTrackBar.TickFrequency = 2;
            medianFilterTrackBar.SmallChange = 2;
            medianFilterTrackBar.LargeChange = 2;
            
            medianFilterTrackBar.Scroll += (s, e) => {
                // 确保值为奇数
                if (medianFilterTrackBar.Value % 2 == 0)
                {
                    medianFilterTrackBar.Value += 1;
                }
                medianFilterLabel.Text = $"核大小: {medianFilterTrackBar.Value}x{medianFilterTrackBar.Value}";
            };
            
            var applyMedianFilterBtn = new Button { Text = "应用中值滤波", Location = new System.Drawing.Point(10, 55) };
            applyMedianFilterBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyMedianFilter(originalImage, medianFilterTrackBar.Value);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用中值滤波: 核大小={medianFilterTrackBar.Value}");
            };
            
            medianFilterGroup.Controls.Add(medianFilterLabel);
            medianFilterGroup.Controls.Add(medianFilterTrackBar);
            medianFilterGroup.Controls.Add(applyMedianFilterBtn);
            layout.Controls.Add(medianFilterGroup, 0, 1);

            // 3. 拉普拉斯锐化
            var laplacianGroup = new GroupBox { Text = "拉普拉斯锐化 (Laplacian Sharpening)", Dock = DockStyle.Fill };
            var laplacianLabel = new Label { Text = "核大小: 3x3", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var laplacianTrackBar = new TrackBar { Minimum = 3, Maximum = 5, Value = 3, Location = new System.Drawing.Point(110, 20), Width = 200 };
            laplacianTrackBar.TickFrequency = 2;
            laplacianTrackBar.SmallChange = 2;
            laplacianTrackBar.LargeChange = 2;
            
            laplacianTrackBar.Scroll += (s, e) => {
                // 确保值为3或5
                if (laplacianTrackBar.Value != 3 && laplacianTrackBar.Value != 5)
                {
                    laplacianTrackBar.Value = 3;
                }
                laplacianLabel.Text = $"核大小: {laplacianTrackBar.Value}x{laplacianTrackBar.Value}";
            };
            
            var applyLaplacianBtn = new Button { Text = "应用拉普拉斯锐化", Location = new System.Drawing.Point(10, 55) };
            applyLaplacianBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyLaplacianSharpening(originalImage, laplacianTrackBar.Value);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用拉普拉斯锐化: 核大小={laplacianTrackBar.Value}");
            };
            
            laplacianGroup.Controls.Add(laplacianLabel);
            laplacianGroup.Controls.Add(laplacianTrackBar);
            laplacianGroup.Controls.Add(applyLaplacianBtn);
            layout.Controls.Add(laplacianGroup, 0, 2);

            // 4. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                processedImage = originalImage.Clone();
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info("空间域滤波已重置");
            };
            layout.Controls.Add(resetBtn, 0, 3);

            spatialFilterTab.Controls.Add(layout);
        }

        private void InitializeFrequencyDomainTab(TabPage frequencyDomainTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3, // 低通滤波，高通滤波，重置
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 高斯低通滤波
            var lowPassGroup = new GroupBox { Text = "高斯低通滤波 (Gaussian Low-pass Filter)", Dock = DockStyle.Fill };
            var lowPassLabel = new Label { Text = "Sigma: 10.0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var lowPassTrackBar = new TrackBar { Minimum = 1, Maximum = 100, Value = 10, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            lowPassTrackBar.Scroll += (s, e) => {
                double sigma = lowPassTrackBar.Value;
                lowPassLabel.Text = $"Sigma: {sigma:F1}";
            };
            
            var applyLowPassBtn = new Button { Text = "应用低通滤波", Location = new System.Drawing.Point(10, 55) };
            applyLowPassBtn.Click += (s, e) => {
                double sigma = lowPassTrackBar.Value;
                processedImage = RegionProcessor.ApplyFrequencyDomainLowPassFilter(originalImage, sigma);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用高斯低通滤波: Sigma={sigma}");
            };
            
            lowPassGroup.Controls.Add(lowPassLabel);
            lowPassGroup.Controls.Add(lowPassTrackBar);
            lowPassGroup.Controls.Add(applyLowPassBtn);
            layout.Controls.Add(lowPassGroup, 0, 0);

            // 2. 高斯高通滤波
            var highPassGroup = new GroupBox { Text = "高斯高通滤波 (Gaussian High-pass Filter)", Dock = DockStyle.Fill };
            var highPassLabel = new Label { Text = "Sigma: 10.0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var highPassTrackBar = new TrackBar { Minimum = 1, Maximum = 100, Value = 10, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            highPassTrackBar.Scroll += (s, e) => {
                double sigma = highPassTrackBar.Value;
                highPassLabel.Text = $"Sigma: {sigma:F1}";
            };
            
            var applyHighPassBtn = new Button { Text = "应用高通滤波", Location = new System.Drawing.Point(10, 55) };
            applyHighPassBtn.Click += (s, e) => {
                double sigma = highPassTrackBar.Value;
                processedImage = RegionProcessor.ApplyFrequencyDomainHighPassFilter(originalImage, sigma);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用高斯高通滤波: Sigma={sigma}");
            };
            
            highPassGroup.Controls.Add(highPassLabel);
            highPassGroup.Controls.Add(highPassTrackBar);
            highPassGroup.Controls.Add(applyHighPassBtn);
            layout.Controls.Add(highPassGroup, 0, 1);

            // 3. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                processedImage = originalImage.Clone();
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                lowPassTrackBar.Value = 10;
                highPassTrackBar.Value = 10;
                lowPassLabel.Text = "Sigma: 10.0";
                highPassLabel.Text = "Sigma: 10.0";
                logger.Info("频域增强已重置");
            };
            layout.Controls.Add(resetBtn, 0, 2);

            frequencyDomainTab.Controls.Add(layout);
        }

        private void InitializeNoiseAndEdgeTab(TabPage noiseAndEdgeTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3, // 噪声抑制与边缘增强，参数设置，重置
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 噪声抑制与边缘增强组合处理
            var noiseEdgeGroup = new GroupBox { Text = "噪声抑制与边缘增强", Dock = DockStyle.Fill };
            var applyNoiseEdgeBtn = new Button { Text = "应用组合处理", Location = new System.Drawing.Point(10, 20) };
            applyNoiseEdgeBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyNoiseSuppressionAndEdgeEnhancement(originalImage);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info("应用噪声抑制与边缘增强组合处理");
            };
            
            var infoLabel = new Label { 
                Text = "先应用高斯模糊去噪，再用拉普拉斯算子增强边缘", 
                Location = new System.Drawing.Point(10, 60), 
                Width = 300,
                ForeColor = Color.Gray
            };
            
            noiseEdgeGroup.Controls.Add(applyNoiseEdgeBtn);
            noiseEdgeGroup.Controls.Add(infoLabel);
            layout.Controls.Add(noiseEdgeGroup, 0, 0);

            // 2. 参数设置
            var paramGroup = new GroupBox { Text = "参数设置", Dock = DockStyle.Fill };
            
            // 高斯模糊参数
            var blurLabel = new Label { Text = "模糊核大小: 5x5", Location = new System.Drawing.Point(10, 25), Width = 120 };
            var blurTrackBar = new TrackBar { Minimum = 3, Maximum = 15, Value = 5, Location = new System.Drawing.Point(130, 20), Width = 180 };
            blurTrackBar.TickFrequency = 2;
            blurTrackBar.SmallChange = 2;
            blurTrackBar.LargeChange = 2;
            
            blurTrackBar.Scroll += (s, e) => {
                // 确保值为奇数
                if (blurTrackBar.Value % 2 == 0)
                {
                    blurTrackBar.Value += 1;
                }
                blurLabel.Text = $"模糊核大小: {blurTrackBar.Value}x{blurTrackBar.Value}";
            };
            
            // 拉普拉斯参数
            var laplacianLabel = new Label { Text = "拉普拉斯核大小: 3x3", Location = new System.Drawing.Point(10, 55), Width = 120 };
            var laplacianTrackBar = new TrackBar { Minimum = 3, Maximum = 5, Value = 3, Location = new System.Drawing.Point(130, 50), Width = 180 };
            laplacianTrackBar.TickFrequency = 2;
            laplacianTrackBar.SmallChange = 2;
            
            laplacianTrackBar.Scroll += (s, e) => {
                laplacianLabel.Text = $"拉普拉斯核大小: {laplacianTrackBar.Value}x{laplacianTrackBar.Value}";
            };
            
            var applyWithParamsBtn = new Button { Text = "应用自定义参数", Location = new System.Drawing.Point(10, 85) };
            applyWithParamsBtn.Click += (s, e) => {
                processedImage = RegionProcessor.ApplyNoiseSuppressionAndEdgeEnhancement(
                    originalImage, blurTrackBar.Value, laplacianTrackBar.Value);
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                logger.Info($"应用噪声抑制与边缘增强组合处理: 模糊核={blurTrackBar.Value}, 拉普拉斯核={laplacianTrackBar.Value}");
            };
            
            paramGroup.Controls.Add(blurLabel);
            paramGroup.Controls.Add(blurTrackBar);
            paramGroup.Controls.Add(laplacianLabel);
            paramGroup.Controls.Add(laplacianTrackBar);
            paramGroup.Controls.Add(applyWithParamsBtn);
            layout.Controls.Add(paramGroup, 0, 1);

            // 3. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                processedImage = originalImage.Clone();
                processedPictureBox.Image = ConvertMatToBitmap(processedImage);
                blurTrackBar.Value = 5;
                laplacianTrackBar.Value = 3;
                blurLabel.Text = "模糊核大小: 5x5";
                laplacianLabel.Text = "拉普拉斯核大小: 3x3";
                logger.Info("噪声抑制与边缘增强已重置");
            };
            layout.Controls.Add(resetBtn, 0, 2);

            noiseAndEdgeTab.Controls.Add(layout);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            originalImage?.Dispose();
            processedImage?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
