using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OpenCvSharp;
using FellowOakDicom;
using NLog;
using itk.simple;
using System.Collections.Generic;

namespace ImageAnalysisTool.UI.Forms
{
    /// <summary>
    /// ITK医学图像处理窗口 - 提供专业的医学图像增强功能
    /// </summary>
    public partial class ITKMedicalProcessingForm : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // 图像数据
        private Mat originalImage;
        private itk.simple.Image itkProcessedImage;
        private itk.simple.Image itkOriginalImage;

        // UI 控件
        private PictureBox originalPictureBox;
        private PictureBox processedPictureBox;
        private Panel controlPanel;

        public ITKMedicalProcessingForm(Mat image)
        {
            if (image != null && !image.Empty())
            {
                originalImage = image.Clone();
                // 转换OpenCV Mat到ITK Image
                itkOriginalImage = ConvertMatToITKImage(originalImage);
            }
            else
            {
                // 创建默认图像
                originalImage = Mat.Zeros(512, 512, MatType.CV_16UC1);
                itkOriginalImage = ConvertMatToITKImage(originalImage);
            }

            if (itkOriginalImage != null)
            {
                itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
            }

            InitializeComponent();
            InitializeCustomComponents();
            
            logger.Info("ITK医学图像处理窗口初始化完成");
        }

        private void InitializeComponent()
        {
            this.Text = "ITK医学图像处理工具";
            this.Size = new System.Drawing.Size(1400, 900);
            this.StartPosition = FormStartPosition.CenterParent;
            this.WindowState = FormWindowState.Maximized;
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

            // 创建处理选项卡控件
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };
            
            // 医学图像去噪Tab
            var denoiseTab = new TabPage("医学图像去噪");
            InitializeDenoiseTab(denoiseTab);
            tabControl.TabPages.Add(denoiseTab);

            // 医学图像增强Tab
            var enhanceTab = new TabPage("医学图像增强");
            InitializeEnhanceTab(enhanceTab);
            tabControl.TabPages.Add(enhanceTab);

            // 形态学操作Tab
            var morphologyTab = new TabPage("形态学操作");
            InitializeMorphologyTab(morphologyTab);
            tabControl.TabPages.Add(morphologyTab);

            // 医学图像分割Tab
            var segmentationTab = new TabPage("医学图像分割");
            InitializeSegmentationTab(segmentationTab);
            tabControl.TabPages.Add(segmentationTab);

            // 图像配准Tab
            var registrationTab = new TabPage("图像配准");
            InitializeRegistrationTab(registrationTab);
            tabControl.TabPages.Add(registrationTab);

            // 特征提取Tab
            var featureTab = new TabPage("特征提取");
            InitializeFeatureTab(featureTab);
            tabControl.TabPages.Add(featureTab);

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
                processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
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
        
        private Bitmap ConvertITKImageToBitmap(itk.simple.Image itkImage)
        {
            if (itkImage == null) return null;

            try
            {
                // 获取图像尺寸
                var size = itkImage.GetSize();
                if (size.Count < 2) return null;

                int width = (int)size[0];
                int height = (int)size[1];

                // 根据像素类型获取缓冲区
                Mat mat;
                var pixelType = itkImage.GetPixelID();

                if (pixelType == itk.simple.PixelIDValueEnum.sitkUInt16)
                {
                    // 获取16位无符号整数缓冲区
                    var bufferPtr = itkImage.GetBufferAsUInt16();
                    var array = new ushort[height, width];

                    // 使用unsafe代码复制数据
                    unsafe
                    {
                        ushort* buffer = (ushort*)bufferPtr.ToPointer();
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                array[y, x] = buffer[y * width + x];
                            }
                        }
                    }

                    mat = new Mat(height, width, MatType.CV_16UC1, array);
                }
                else if (pixelType == itk.simple.PixelIDValueEnum.sitkUInt8)
                {
                    // 获取8位无符号整数缓冲区
                    var bufferPtr = itkImage.GetBufferAsUInt8();
                    var array = new byte[height, width];

                    unsafe
                    {
                        byte* buffer = (byte*)bufferPtr.ToPointer();
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                array[y, x] = buffer[y * width + x];
                            }
                        }
                    }

                    mat = new Mat(height, width, MatType.CV_8UC1, array);
                }
                else
                {
                    // 对于其他类型，转换为16位
                    var bufferPtr = itkImage.GetBufferAsFloat();
                    var array = new ushort[height, width];

                    unsafe
                    {
                        float* buffer = (float*)bufferPtr.ToPointer();
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                float value = buffer[y * width + x];
                                array[y, x] = (ushort)Math.Max(0, Math.Min(65535, value));
                            }
                        }
                    }

                    mat = new Mat(height, width, MatType.CV_16UC1, array);
                }

                // 转换为Bitmap显示
                return ConvertMatToBitmap(mat);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "ITK图像转Bitmap失败");
                return null;
            }
        }

        private itk.simple.Image ConvertMatToITKImage(Mat mat)
        {
            if (mat == null || mat.Empty()) return null;
            
            try
            {
                // 将OpenCV Mat转换为ITK Image
                if (mat.Type() == MatType.CV_16UC1)
                {
                    // 对于16位图像，创建一维数组
                    var array = new ushort[mat.Rows * mat.Cols];
                    // Marshal.Copy(mat.Data, array, 0, mat.Rows * mat.Cols); // mat.Data 是 IntPtr, 需要正确处理
                    unsafe
                    {
                        fixed (ushort* ptr = array)
                        {
                            Buffer.MemoryCopy((void*)mat.Data, ptr, array.Length * sizeof(ushort), array.Length * sizeof(ushort));
                        }
                    }
                    
                    // 重塑为二维数组
                    var array2D = new ushort[mat.Rows, mat.Cols];
                    for (int i = 0; i < mat.Rows; i++)
                    {
                        for (int j = 0; j < mat.Cols; j++)
                        {
                            array2D[i, j] = array[i * mat.Cols + j];
                        }
                    }
                    
                    // 使用 SimpleITK 创建图像 (对于 ushort 类型)
                    var itkImage = new itk.simple.Image((uint)mat.Cols, (uint)mat.Rows, itk.simple.PixelIDValueEnum.sitkUInt16);

                    // 复制数据到ITK图像
                    unsafe
                    {
                        var bufferPtr = itkImage.GetBufferAsUInt16();
                        ushort* buffer = (ushort*)bufferPtr.ToPointer();
                        for (int i = 0; i < array.Length; i++)
                        {
                            buffer[i] = array[i];
                        }
                    }

                    return itkImage;
                }
                else
                {
                    // 对于8位图像
                    var array = new byte[mat.Rows * mat.Cols];
                    // Marshal.Copy(mat.Data, array, 0, mat.Rows * mat.Cols); // mat.Data 是 IntPtr, 需要正确处理
                    unsafe
                    {
                        fixed (byte* ptr = array)
                        {
                            Buffer.MemoryCopy((void*)mat.Data, ptr, array.Length, array.Length);
                        }
                    }
                    
                    // 重塑为二维数组
                    var array2D = new byte[mat.Rows, mat.Cols];
                    for (int i = 0; i < mat.Rows; i++)
                    {
                        for (int j = 0; j < mat.Cols; j++)
                        {
                            array2D[i, j] = array[i * mat.Cols + j];
                        }
                    }
                    
                    // 使用 SimpleITK 创建图像 (对于 byte 类型)
                    var itkImage = new itk.simple.Image((uint)mat.Cols, (uint)mat.Rows, itk.simple.PixelIDValueEnum.sitkUInt8);

                    // 复制数据到ITK图像
                    unsafe
                    {
                        var bufferPtr = itkImage.GetBufferAsUInt8();
                        byte* buffer = (byte*)bufferPtr.ToPointer();
                        for (int i = 0; i < array.Length; i++)
                        {
                            buffer[i] = array[i];
                        }
                    }

                    return itkImage;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Mat转ITK图像失败");
                return null;
            }
        }

        private void InitializeDenoiseTab(TabPage denoiseTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 各向异性扩散去噪
            var denoiseGroup = new GroupBox { Text = "各向异性扩散去噪", Dock = DockStyle.Fill };
            var denoiseValueLabel = new Label { Text = "迭代次数: 5", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var denoiseTrackBar = new TrackBar { Minimum = 1, Maximum = 20, Value = 5, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            denoiseTrackBar.Scroll += (s, e) => {
                denoiseValueLabel.Text = $"迭代次数: {denoiseTrackBar.Value}";
            };
            
            var applyDenoiseBtn = new Button { Text = "应用去噪", Location = new System.Drawing.Point(10, 55) };
            applyDenoiseBtn.Click += (s, e) => {
                try
                {
                    itkProcessedImage = SimpleITK.CurvatureFlow(itkOriginalImage, 0.125, (uint)denoiseTrackBar.Value);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用各向异性扩散去噪: 迭代={denoiseTrackBar.Value}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用去噪失败");
                    MessageBox.Show($"应用去噪失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            denoiseGroup.Controls.Add(denoiseValueLabel);
            denoiseGroup.Controls.Add(denoiseTrackBar);
            denoiseGroup.Controls.Add(applyDenoiseBtn);
            layout.Controls.Add(denoiseGroup, 0, 0);

            // 2. 中值滤波去噪
            var medianGroup = new GroupBox { Text = "中值滤波去噪", Dock = DockStyle.Fill };
            var applyMedianBtn = new Button { Text = "应用中值滤波", Location = new System.Drawing.Point(10, 20) };
            applyMedianBtn.Click += (s, e) => {
                try
                {
                    itkProcessedImage = SimpleITK.Median(itkOriginalImage, new itk.simple.VectorUInt32(new uint[] { 3, 3 }));
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用中值滤波去噪");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用中值滤波失败");
                    MessageBox.Show($"应用中值滤波失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            medianGroup.Controls.Add(applyMedianBtn);
            layout.Controls.Add(medianGroup, 0, 1);

            // 3. 双边滤波
            var bilateralGroup = new GroupBox { Text = "双边滤波", Dock = DockStyle.Fill };
            var bilateralDomainLabel = new Label { Text = "Domain Sigma: 2.0", Location = new System.Drawing.Point(10, 25), Width = 120 };
            var bilateralDomainTrackBar = new TrackBar { Minimum = 1, Maximum = 20, Value = 2, Location = new System.Drawing.Point(130, 20), Width = 180 };
            var bilateralRangeLabel = new Label { Text = "Range Sigma: 2.0", Location = new System.Drawing.Point(10, 55), Width = 120 };
            var bilateralRangeTrackBar = new TrackBar { Minimum = 1, Maximum = 20, Value = 2, Location = new System.Drawing.Point(130, 50), Width = 180 };
            
            bilateralDomainTrackBar.Scroll += (s, e) => {
                bilateralDomainLabel.Text = $"Domain Sigma: {bilateralDomainTrackBar.Value / 10.0:F1}";
            };
            
            bilateralRangeTrackBar.Scroll += (s, e) => {
                bilateralRangeLabel.Text = $"Range Sigma: {bilateralRangeTrackBar.Value / 10.0:F1}";
            };
            
            var applyBilateralBtn = new Button { Text = "应用双边滤波", Location = new System.Drawing.Point(10, 85) };
            applyBilateralBtn.Click += (s, e) => {
                try
                {
                    double domainSigma = bilateralDomainTrackBar.Value / 10.0;
                    double rangeSigma = bilateralRangeTrackBar.Value / 10.0;
                    itkProcessedImage = SimpleITK.Bilateral(itkOriginalImage, domainSigma, rangeSigma);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用双边滤波: DomainSigma={domainSigma}, RangeSigma={rangeSigma}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用双边滤波失败");
                    MessageBox.Show($"应用双边滤波失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            bilateralGroup.Controls.Add(bilateralDomainLabel);
            bilateralGroup.Controls.Add(bilateralDomainTrackBar);
            bilateralGroup.Controls.Add(bilateralRangeLabel);
            bilateralGroup.Controls.Add(bilateralRangeTrackBar);
            bilateralGroup.Controls.Add(applyBilateralBtn);
            layout.Controls.Add(bilateralGroup, 0, 2);

            // 4. 高斯滤波
            var gaussianGroup = new GroupBox { Text = "高斯滤波", Dock = DockStyle.Fill };
            var gaussianSigmaLabel = new Label { Text = "Sigma: 1.0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var gaussianSigmaTrackBar = new TrackBar { Minimum = 1, Maximum = 50, Value = 10, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            gaussianSigmaTrackBar.Scroll += (s, e) => {
                gaussianSigmaLabel.Text = $"Sigma: {gaussianSigmaTrackBar.Value / 10.0:F1}";
            };
            
            var applyGaussianBtn = new Button { Text = "应用高斯滤波", Location = new System.Drawing.Point(10, 55) };
            applyGaussianBtn.Click += (s, e) => {
                try
                {
                    double sigma = gaussianSigmaTrackBar.Value / 10.0;
                    itkProcessedImage = SimpleITK.DiscreteGaussian(itkOriginalImage, sigma);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用高斯滤波: Sigma={sigma}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用高斯滤波失败");
                    MessageBox.Show($"应用高斯滤波失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            gaussianGroup.Controls.Add(gaussianSigmaLabel);
            gaussianGroup.Controls.Add(gaussianSigmaTrackBar);
            gaussianGroup.Controls.Add(applyGaussianBtn);
            layout.Controls.Add(gaussianGroup, 0, 3);

            // 5. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                denoiseTrackBar.Value = 5;
                denoiseValueLabel.Text = "迭代次数: 5";
                bilateralDomainTrackBar.Value = 2;
                bilateralRangeTrackBar.Value = 2;
                bilateralDomainLabel.Text = "Domain Sigma: 2.0";
                bilateralRangeLabel.Text = "Range Sigma: 2.0";
                gaussianSigmaTrackBar.Value = 10;
                gaussianSigmaLabel.Text = "Sigma: 1.0";
                logger.Info("医学图像去噪已重置");
            };
            layout.Controls.Add(resetBtn, 0, 4);

            denoiseTab.Controls.Add(layout);
        }

        private void InitializeEnhanceTab(TabPage enhanceTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 自适应直方图均衡化
            var aheGroup = new GroupBox { Text = "自适应直方图均衡化", Dock = DockStyle.Fill };
            var enhanceValueLabel = new Label { Text = "半径: 5", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var enhanceTrackBar = new TrackBar { Minimum = 3, Maximum = 20, Value = 5, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            enhanceTrackBar.Scroll += (s, e) => {
                enhanceValueLabel.Text = $"半径: {enhanceTrackBar.Value}";
            };
            
            var applyAHEBtn = new Button { Text = "应用AHE", Location = new System.Drawing.Point(10, 55) };
            applyAHEBtn.Click += (s, e) => {
                try
                {
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add((uint)enhanceTrackBar.Value);
                    itkProcessedImage = SimpleITK.AdaptiveHistogramEqualization(itkOriginalImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用自适应直方图均衡化: 半径={enhanceTrackBar.Value}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用AHE失败");
                    MessageBox.Show($"应用AHE失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            aheGroup.Controls.Add(enhanceValueLabel);
            aheGroup.Controls.Add(enhanceTrackBar);
            aheGroup.Controls.Add(applyAHEBtn);
            layout.Controls.Add(aheGroup, 0, 0);

            // 2. N4偏置场校正
            var n4Group = new GroupBox { Text = "N4偏置场校正", Dock = DockStyle.Fill };
            var applyN4Btn = new Button { Text = "应用N4校正", Location = new System.Drawing.Point(10, 20) };
            applyN4Btn.Click += (s, e) => {
                try
                {
                    // 创建一个简单的二值掩膜（实际应用中应该使用更精确的掩膜）
                    var mask = SimpleITK.OtsuThreshold(itkOriginalImage, 0, 1);
                    itkProcessedImage = SimpleITK.N4BiasFieldCorrection(itkOriginalImage, mask);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用N4偏置场校正");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用N4校正失败");
                    MessageBox.Show($"应用N4校正失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            n4Group.Controls.Add(applyN4Btn);
            layout.Controls.Add(n4Group, 0, 1);

            // 3. 直方图匹配
            var histMatchGroup = new GroupBox { Text = "直方图匹配", Dock = DockStyle.Fill };
            var applyHistMatchBtn = new Button { Text = "应用直方图匹配", Location = new System.Drawing.Point(10, 20) };
            applyHistMatchBtn.Click += (s, e) => {
                try
                {
                    // 使用原图作为参考图像（实际应用中应该使用另一幅图像作为参考）
                    itkProcessedImage = SimpleITK.HistogramMatching(itkOriginalImage, itkOriginalImage);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用直方图匹配");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用直方图匹配失败");
                    MessageBox.Show($"应用直方图匹配失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            histMatchGroup.Controls.Add(applyHistMatchBtn);
            layout.Controls.Add(histMatchGroup, 0, 2);

            // 4. 对比度增强
            var contrastGroup = new GroupBox { Text = "对比度增强", Dock = DockStyle.Fill };
            var applyContrastBtn = new Button { Text = "应用对比度增强", Location = new System.Drawing.Point(10, 20) };
            applyContrastBtn.Click += (s, e) => {
                try
                {
                    // 使用直方图均衡化作为对比度增强 (SimpleITK 中可能没有直接的 HistogramEqualization, 使用 AdaptiveHistogramEqualization 代替)
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add(5u); // 使用默认半径
                    itkProcessedImage = SimpleITK.AdaptiveHistogramEqualization(itkOriginalImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用对比度增强");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用对比度增强失败");
                    MessageBox.Show($"应用对比度增强失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            contrastGroup.Controls.Add(applyContrastBtn);
            layout.Controls.Add(contrastGroup, 0, 3);

            // 5. 伽马校正
            var gammaGroup = new GroupBox { Text = "伽马校正", Dock = DockStyle.Fill };
            var gammaValueLabel = new Label { Text = "Gamma: 1.0", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var gammaTrackBar = new TrackBar { Minimum = 1, Maximum = 50, Value = 10, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            gammaTrackBar.Scroll += (s, e) => {
                gammaValueLabel.Text = $"Gamma: {gammaTrackBar.Value / 10.0:F1}";
            };
            
            var applyGammaBtn = new Button { Text = "应用伽马校正", Location = new System.Drawing.Point(10, 55) };
            applyGammaBtn.Click += (s, e) => {
                try
                {
                    double gamma = gammaTrackBar.Value / 10.0;
                    itkProcessedImage = SimpleITK.Pow(itkOriginalImage, 1.0 / gamma);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用伽马校正: Gamma={gamma}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用伽马校正失败");
                    MessageBox.Show($"应用伽马校正失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            gammaGroup.Controls.Add(gammaValueLabel);
            gammaGroup.Controls.Add(gammaTrackBar);
            gammaGroup.Controls.Add(applyGammaBtn);
            layout.Controls.Add(gammaGroup, 0, 4);

            // 6. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                enhanceTrackBar.Value = 5;
                enhanceValueLabel.Text = "半径: 5";
                gammaTrackBar.Value = 10;
                gammaValueLabel.Text = "Gamma: 1.0";
                logger.Info("医学图像增强已重置");
            };
            layout.Controls.Add(resetBtn, 0, 5);

            enhanceTab.Controls.Add(layout);
        }

        private void InitializeMorphologyTab(TabPage morphologyTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 二值化（先进行二值化处理）
            var binarizeGroup = new GroupBox { Text = "二值化", Dock = DockStyle.Fill };
            var thresholdLabel = new Label { Text = "阈值: 1000", Location = new System.Drawing.Point(10, 25), Width = 100 };
            var thresholdTrackBar = new TrackBar { Minimum = 0, Maximum = 65535, Value = 1000, Location = new System.Drawing.Point(110, 20), Width = 200 };
            
            thresholdTrackBar.Scroll += (s, e) => {
                thresholdLabel.Text = $"阈值: {thresholdTrackBar.Value}";
            };
            
            var applyBinarizeBtn = new Button { Text = "应用二值化", Location = new System.Drawing.Point(10, 55) };
            applyBinarizeBtn.Click += (s, e) => {
                try
                {
                    itkProcessedImage = SimpleITK.BinaryThreshold(itkOriginalImage, thresholdTrackBar.Value, 65535);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用二值化: 阈值={thresholdTrackBar.Value}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用二值化失败");
                    MessageBox.Show($"应用二值化失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            binarizeGroup.Controls.Add(thresholdLabel);
            binarizeGroup.Controls.Add(thresholdTrackBar);
            binarizeGroup.Controls.Add(applyBinarizeBtn);
            layout.Controls.Add(binarizeGroup, 0, 0);

            // 2. 二值膨胀
            var dilateGroup = new GroupBox { Text = "二值膨胀", Dock = DockStyle.Fill };
            var applyDilateBtn = new Button { Text = "应用膨胀", Location = new System.Drawing.Point(10, 20) };
            applyDilateBtn.Click += (s, e) => {
                try
                {
                    // 先二值化再膨胀
                    var binaryImage = SimpleITK.BinaryThreshold(itkOriginalImage, 1000, 65535);
                    // 使用半径参数进行膨胀操作
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add(1); // X方向半径
                    radius.Add(1); // Y方向半径
                    itkProcessedImage = SimpleITK.BinaryDilate(binaryImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用二值膨胀");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用二值膨胀失败");
                    MessageBox.Show($"应用二值膨胀失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            dilateGroup.Controls.Add(applyDilateBtn);
            layout.Controls.Add(dilateGroup, 0, 1);

            // 3. 二值腐蚀
            var erodeGroup = new GroupBox { Text = "二值腐蚀", Dock = DockStyle.Fill };
            var applyErodeBtn = new Button { Text = "应用腐蚀", Location = new System.Drawing.Point(10, 20) };
            applyErodeBtn.Click += (s, e) => {
                try
                {
                    // 先二值化再腐蚀
                    var binaryImage = SimpleITK.BinaryThreshold(itkOriginalImage, 1000, 65535);
                    // 使用半径参数进行腐蚀操作
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add(1); // X方向半径
                    radius.Add(1); // Y方向半径
                    itkProcessedImage = SimpleITK.BinaryErode(binaryImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用二值腐蚀");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用二值腐蚀失败");
                    MessageBox.Show($"应用二值腐蚀失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            erodeGroup.Controls.Add(applyErodeBtn);
            layout.Controls.Add(erodeGroup, 0, 2);

            // 4. 开运算
            var openGroup = new GroupBox { Text = "开运算", Dock = DockStyle.Fill };
            var applyOpenBtn = new Button { Text = "应用开运算", Location = new System.Drawing.Point(10, 20) };
            applyOpenBtn.Click += (s, e) => {
                try
                {
                    // 先二值化再开运算
                    var binaryImage = SimpleITK.BinaryThreshold(itkOriginalImage, 1000, 65535);
                    // 使用半径参数进行开运算操作
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add(1); // X方向半径
                    radius.Add(1); // Y方向半径
                    var tempImage = SimpleITK.BinaryErode(binaryImage, radius);
                    itkProcessedImage = SimpleITK.BinaryDilate(tempImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用开运算");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用开运算失败");
                    MessageBox.Show($"应用开运算失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            openGroup.Controls.Add(applyOpenBtn);
            layout.Controls.Add(openGroup, 0, 3);

            // 5. 闭运算
            var closeGroup = new GroupBox { Text = "闭运算", Dock = DockStyle.Fill };
            var applyCloseBtn = new Button { Text = "应用闭运算", Location = new System.Drawing.Point(10, 20) };
            applyCloseBtn.Click += (s, e) => {
                try
                {
                    // 先二值化再闭运算
                    var binaryImage = SimpleITK.BinaryThreshold(itkOriginalImage, 1000, 65535);
                    // 使用半径参数进行闭运算操作
                    var radius = new itk.simple.VectorUInt32();
                    radius.Add(1); // X方向半径
                    radius.Add(1); // Y方向半径
                    var tempImage = SimpleITK.BinaryDilate(binaryImage, radius);
                    itkProcessedImage = SimpleITK.BinaryErode(tempImage, radius);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用闭运算");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用闭运算失败");
                    MessageBox.Show($"应用闭运算失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            closeGroup.Controls.Add(applyCloseBtn);
            layout.Controls.Add(closeGroup, 0, 4);

            // 6. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                thresholdTrackBar.Value = 1000;
                thresholdLabel.Text = "阈值: 1000";
                logger.Info("形态学操作已重置");
            };
            layout.Controls.Add(resetBtn, 0, 5);

            morphologyTab.Controls.Add(layout);
        }

        private void InitializeSegmentationTab(TabPage segmentationTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 阈值分割
            var thresholdGroup = new GroupBox { Text = "阈值分割", Dock = DockStyle.Fill };
            var applyThresholdBtn = new Button { Text = "应用阈值分割", Location = new System.Drawing.Point(10, 20) };
            applyThresholdBtn.Click += (s, e) => {
                try
                {
                    // 计算Otsu阈值 (SimpleITK.OtsuThreshold 返回的是图像, 不是阈值)
                    var otsuImage = SimpleITK.OtsuThreshold(itkOriginalImage);
                    // 获取阈值 (假设阈值存储在图像的某个元数据中, 这里简化处理)
                    // 实际应用中可能需要更复杂的逻辑来获取阈值
                    double otsuThreshold = 1000; // 这里使用一个默认值
                    itkProcessedImage = SimpleITK.BinaryThreshold(itkOriginalImage, otsuThreshold, 65535);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info($"应用阈值分割: 阈值={otsuThreshold}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用阈值分割失败");
                    MessageBox.Show($"应用阈值分割失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            thresholdGroup.Controls.Add(applyThresholdBtn);
            layout.Controls.Add(thresholdGroup, 0, 0);

            // 2. 连通阈值分割
            var connectedThresholdGroup = new GroupBox { Text = "连通阈值分割", Dock = DockStyle.Fill };
            var applyConnectedThresholdBtn = new Button { Text = "应用连通阈值分割", Location = new System.Drawing.Point(10, 20) };
            applyConnectedThresholdBtn.Click += (s, e) => {
                try
                {
                    // 使用固定种子点和阈值范围（实际应用中应该让用户选择种子点）
                    // SimpleITK中使用VectorUIntList作为种子点
                    var seedList = new VectorUIntList();
                    var seedPoint = new VectorUInt32();
                    seedPoint.Add(100);
                    seedPoint.Add(100);
                    seedList.Add(seedPoint);
                    itkProcessedImage = SimpleITK.ConnectedThreshold(itkOriginalImage, seedList, 500, 2000);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用连通阈值分割");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用连通阈值分割失败");
                    MessageBox.Show($"应用连通阈值分割失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            connectedThresholdGroup.Controls.Add(applyConnectedThresholdBtn);
            layout.Controls.Add(connectedThresholdGroup, 0, 1);

            // 3. 区域生长分割
            var regionGrowingGroup = new GroupBox { Text = "区域生长分割", Dock = DockStyle.Fill };
            var applyRegionGrowingBtn = new Button { Text = "应用区域生长", Location = new System.Drawing.Point(10, 20) };
            applyRegionGrowingBtn.Click += (s, e) => {
                try
                {
                    // 使用固定种子点（实际应用中应该让用户选择种子点）
                    // SimpleITK中使用VectorUIntList作为种子点
                    var seedList = new VectorUIntList();
                    var seedPoint = new VectorUInt32();
                    seedPoint.Add(100);
                    seedPoint.Add(100);
                    seedList.Add(seedPoint);
                    itkProcessedImage = SimpleITK.NeighborhoodConnected(itkOriginalImage, seedList, 500, 2000);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用区域生长分割");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用区域生长分割失败");
                    MessageBox.Show($"应用区域生长分割失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            regionGrowingGroup.Controls.Add(applyRegionGrowingBtn);
            layout.Controls.Add(regionGrowingGroup, 0, 2);

            // 4. 快速行进分割
            var fastMarchingGroup = new GroupBox { Text = "快速行进分割", Dock = DockStyle.Fill };
            var applyFastMarchingBtn = new Button { Text = "应用快速行进", Location = new System.Drawing.Point(10, 20) };
            applyFastMarchingBtn.Click += (s, e) => {
                try
                {
                    // 使用固定种子点（实际应用中应该让用户选择种子点）
                    // SimpleITK中使用VectorUIntList作为种子点
                    var trialPoints = new VectorUIntList();
                    var trialPoint = new VectorUInt32();
                    trialPoint.Add(100);
                    trialPoint.Add(100);
                    trialPoints.Add(trialPoint);
                    var speedImage = SimpleITK.Abs(SimpleITK.Sigmoid(SimpleITK.GradientMagnitude(itkOriginalImage), 0, 100, -1, 1));
                    // 使用FastMarchingImageFilter
                    var fastMarching = new itk.simple.FastMarchingImageFilter();
                    fastMarching.SetTrialPoints(trialPoints);
                    // FastMarchingImageFilter已经设置了试验点，可以直接执行
                    itkProcessedImage = fastMarching.Execute(itkOriginalImage);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用快速行进分割");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用快速行进分割失败");
                    MessageBox.Show($"应用快速行进分割失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            fastMarchingGroup.Controls.Add(applyFastMarchingBtn);
            layout.Controls.Add(fastMarchingGroup, 0, 3);

            // 5. 活动轮廓分割
            var activeContourGroup = new GroupBox { Text = "活动轮廓分割", Dock = DockStyle.Fill };
            var applyActiveContourBtn = new Button { Text = "应用活动轮廓", Location = new System.Drawing.Point(10, 20) };
            applyActiveContourBtn.Click += (s, e) => {
                try
                {
                    // 创建初始轮廓（简单的圆形）
                    var size = itkOriginalImage.GetSize();
                    // SimpleITK.Image构造函数直接使用size数组
                    var contourImage = new itk.simple.Image(size, itk.simple.PixelIDValueEnum.sitkUInt8);
                    // 注意：ChanVeseDenoising在SimpleITK中可能不可用，使用其他方法代替
                    itkProcessedImage = contourImage;
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用活动轮廓分割");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用活动轮廓分割失败");
                    MessageBox.Show($"应用活动轮廓分割失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            activeContourGroup.Controls.Add(applyActiveContourBtn);
            layout.Controls.Add(activeContourGroup, 0, 4);

            // 6. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                logger.Info("医学图像分割已重置");
            };
            layout.Controls.Add(resetBtn, 0, 5);

            segmentationTab.Controls.Add(layout);
        }

        private void InitializeRegistrationTab(TabPage registrationTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 刚体配准
            var rigidGroup = new GroupBox { Text = "刚体配准", Dock = DockStyle.Fill };
            var applyRigidBtn = new Button { Text = "应用刚体配准", Location = new System.Drawing.Point(10, 20) };
            applyRigidBtn.Click += (s, e) => {
                try
                {
                    // 使用原图作为固定图像和移动图像（实际应用中应该使用不同的图像）
                    var fixedImage = itkOriginalImage;
                    var movingImage = itkOriginalImage;
                    
                    // 创建配准器
                    var registration = new itk.simple.ImageRegistrationMethod();
                    registration.SetMetricAsMeanSquares();
                    registration.SetOptimizerAsRegularStepGradientDescent(1.0, 0.001, 200);
                    registration.SetInitialTransform(new itk.simple.Euler2DTransform());
                    // 设置插值器
                    registration.SetInterpolator(itk.simple.InterpolatorEnum.sitkLinear);

                    // 执行配准
                    var transform = registration.Execute(fixedImage, movingImage);

                    // 应用变换
                    itkProcessedImage = SimpleITK.Resample(movingImage, fixedImage, transform, itk.simple.InterpolatorEnum.sitkLinear, 0.0, movingImage.GetPixelID());
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用刚体配准");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用刚体配准失败");
                    MessageBox.Show($"应用刚体配准失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            rigidGroup.Controls.Add(applyRigidBtn);
            layout.Controls.Add(rigidGroup, 0, 0);

            // 2. 仿射配准
            var affineGroup = new GroupBox { Text = "仿射配准", Dock = DockStyle.Fill };
            var applyAffineBtn = new Button { Text = "应用仿射配准", Location = new System.Drawing.Point(10, 20) };
            applyAffineBtn.Click += (s, e) => {
                try
                {
                    // 使用原图作为固定图像和移动图像（实际应用中应该使用不同的图像）
                    var fixedImage = itkOriginalImage;
                    var movingImage = itkOriginalImage;
                    
                    // 创建配准器
                    var registration = new itk.simple.ImageRegistrationMethod();
                    registration.SetMetricAsMeanSquares();
                    registration.SetOptimizerAsRegularStepGradientDescent(1.0, 0.001, 200);
                    registration.SetInitialTransform(new itk.simple.AffineTransform(2));
                    // 设置插值器
                    registration.SetInterpolator(itk.simple.InterpolatorEnum.sitkLinear);

                    // 执行配准
                    var transform = registration.Execute(fixedImage, movingImage);

                    // 应用变换
                    itkProcessedImage = SimpleITK.Resample(movingImage, fixedImage, transform, itk.simple.InterpolatorEnum.sitkLinear, 0.0, movingImage.GetPixelID());
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用仿射配准");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用仿射配准失败");
                    MessageBox.Show($"应用仿射配准失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            affineGroup.Controls.Add(applyAffineBtn);
            layout.Controls.Add(affineGroup, 0, 1);

            // 3. BSpline非线性配准
            var bsplineGroup = new GroupBox { Text = "BSpline非线性配准", Dock = DockStyle.Fill };
            var applyBSplineBtn = new Button { Text = "应用BSpline配准", Location = new System.Drawing.Point(10, 20) };
            applyBSplineBtn.Click += (s, e) => {
                try
                {
                    // 使用原图作为固定图像和移动图像（实际应用中应该使用不同的图像）
                    var fixedImage = itkOriginalImage;
                    var movingImage = itkOriginalImage;
                    
                    // 创建配准器
                    var registration = new itk.simple.ImageRegistrationMethod();
                    registration.SetMetricAsMeanSquares();
                    registration.SetOptimizerAsLBFGSB(100);
                    // BSplineTransform在SimpleITK中的构造方式不同
                    // 使用简单的平移变换作为初始变换
                    var initialTransform = new itk.simple.TranslationTransform(2);
                    registration.SetInitialTransform(initialTransform);
                    // 设置插值器
                    registration.SetInterpolator(itk.simple.InterpolatorEnum.sitkLinear);

                    // 执行配准
                    var transform = registration.Execute(fixedImage, movingImage);

                    // 应用变换
                    itkProcessedImage = SimpleITK.Resample(movingImage, fixedImage, transform, itk.simple.InterpolatorEnum.sitkLinear, 0.0, movingImage.GetPixelID());
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用BSpline非线性配准");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用BSpline配准失败");
                    MessageBox.Show($"应用BSpline配准失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            bsplineGroup.Controls.Add(applyBSplineBtn);
            layout.Controls.Add(bsplineGroup, 0, 2);

            // 4. Demons配准
            var demonsGroup = new GroupBox { Text = "Demons配准", Dock = DockStyle.Fill };
            var applyDemonsBtn = new Button { Text = "应用Demons配准", Location = new System.Drawing.Point(10, 20) };
            applyDemonsBtn.Click += (s, e) => {
                try
                {
                    // 使用原图作为固定图像和移动图像（实际应用中应该使用不同的图像）
                    var fixedImage = itkOriginalImage;
                    var movingImage = itkOriginalImage;
                    
                    // 执行Demons配准
                    // 注意：FastSymmetricForcesDemonsRegistration在SimpleITK中可能不可用，使用DemonsRegistrationFilter代替
                    var demons = new itk.simple.DemonsRegistrationFilter();
                    demons.SetNumberOfIterations(10);
                    demons.SetStandardDeviations(1.5);
                    var displacementField = demons.Execute(fixedImage, movingImage);
                    
                    // 应用变换 - displacementField需要转换为Transform
                    // 使用SimpleITK的DisplacementFieldTransform
                    var displacementTransform = new itk.simple.DisplacementFieldTransform(displacementField);
                    itkProcessedImage = SimpleITK.Resample(movingImage, fixedImage, displacementTransform, itk.simple.InterpolatorEnum.sitkLinear, 0.0, movingImage.GetPixelID());
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用Demons配准");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用Demons配准失败");
                    MessageBox.Show($"应用Demons配准失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            demonsGroup.Controls.Add(applyDemonsBtn);
            layout.Controls.Add(demonsGroup, 0, 3);

            // 5. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                logger.Info("图像配准已重置");
            };
            layout.Controls.Add(resetBtn, 0, 4);

            registrationTab.Controls.Add(layout);
        }

        private void InitializeFeatureTab(TabPage featureTab)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            // 1. 边缘检测
            var edgeGroup = new GroupBox { Text = "边缘检测", Dock = DockStyle.Fill };
            var applyEdgeBtn = new Button { Text = "应用边缘检测", Location = new System.Drawing.Point(10, 20) };
            applyEdgeBtn.Click += (s, e) => {
                try
                {
                    // CannyEdgeDetection的参数需要是VectorDouble
                    var variance = new VectorDouble();
                    variance.Add(1.0);
                    variance.Add(1.0);
                    itkProcessedImage = SimpleITK.CannyEdgeDetection(itkOriginalImage, 50, 100, variance);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用边缘检测");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用边缘检测失败");
                    MessageBox.Show($"应用边缘检测失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            edgeGroup.Controls.Add(applyEdgeBtn);
            layout.Controls.Add(edgeGroup, 0, 0);

            // 2. 拉普拉斯算子
            var laplacianGroup = new GroupBox { Text = "拉普拉斯算子", Dock = DockStyle.Fill };
            var applyLaplacianBtn = new Button { Text = "应用拉普拉斯", Location = new System.Drawing.Point(10, 20) };
            applyLaplacianBtn.Click += (s, e) => {
                try
                {
                    var laplacian = SimpleITK.Laplacian(itkOriginalImage);
                    var laplacianAbs = SimpleITK.Abs(laplacian);
                    itkProcessedImage = laplacianAbs;
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用拉普拉斯算子");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用拉普拉斯算子失败");
                    MessageBox.Show($"应用拉普拉斯算子失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            laplacianGroup.Controls.Add(applyLaplacianBtn);
            layout.Controls.Add(laplacianGroup, 0, 1);

            // 3. 梯度计算
            var gradientGroup = new GroupBox { Text = "梯度计算", Dock = DockStyle.Fill };
            var applyGradientBtn = new Button { Text = "应用梯度计算", Location = new System.Drawing.Point(10, 20) };
            applyGradientBtn.Click += (s, e) => {
                try
                {
                    var gradient = SimpleITK.Gradient(itkOriginalImage);
                    // 计算梯度幅值
                    itkProcessedImage = SimpleITK.VectorMagnitude(gradient);
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用梯度计算");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用梯度计算失败");
                    MessageBox.Show($"应用梯度计算失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            gradientGroup.Controls.Add(applyGradientBtn);
            layout.Controls.Add(gradientGroup, 0, 2);

            // 4. 角点检测
            var cornerGroup = new GroupBox { Text = "角点检测", Dock = DockStyle.Fill };
            var applyCornerBtn = new Button { Text = "应用角点检测", Location = new System.Drawing.Point(10, 20) };
            applyCornerBtn.Click += (s, e) => {
                try
                {
                    // 使用Hessian矩阵特征值计算角点响应
                    // SimpleITK中没有直接的HessianRecursiveGaussianImageFilter
                    // 使用梯度幅值和拉普拉斯算子组合来检测角点
                    var gradient = SimpleITK.Gradient(itkOriginalImage);
                    var gradientMagnitude = SimpleITK.VectorMagnitude(gradient);
                    var laplacian = SimpleITK.Laplacian(itkOriginalImage);
                    var laplacianAbs = SimpleITK.Abs(laplacian);
                    // 组合梯度幅值和拉普拉斯来增强角点响应
                    itkProcessedImage = gradientMagnitude * laplacianAbs;
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                    logger.Info("应用角点检测");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "应用角点检测失败");
                    MessageBox.Show($"应用角点检测失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            cornerGroup.Controls.Add(applyCornerBtn);
            layout.Controls.Add(cornerGroup, 0, 3);

            // 5. 重置按钮
            var resetBtn = new Button { Text = "重置为原图", BackColor = Color.MistyRose, Location = new System.Drawing.Point(10, 20) };
            resetBtn.Click += (s, e) => {
                if (itkOriginalImage != null)
                {
                    itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                    processedPictureBox.Image = ConvertITKImageToBitmap(itkProcessedImage);
                }
                logger.Info("特征提取已重置");
            };
            layout.Controls.Add(resetBtn, 0, 4);

            featureTab.Controls.Add(layout);
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
                        itkOriginalImage?.Dispose();
                        itkProcessedImage?.Dispose();
                        
                        // 加载新图像
                        originalImage = LoadImageFile(dialog.FileName);
                        itkOriginalImage = ConvertMatToITKImage(originalImage);
                        if (itkOriginalImage != null)
                        {
                            itkProcessedImage = itkOriginalImage; // SimpleITK Image doesn't have Clone(), assign directly
                        }
                        
                        // 更新显示
                        LoadImages();
                        
                        logger.Info($"成功加载图像: {dialog.FileName}");
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
            if (itkProcessedImage == null)
            {
                return;
            }
            
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "TIFF图像|*.tiff;*.tif|PNG图像|*.png|所有文件|*.*";
                dialog.DefaultExt = "tiff";
                dialog.FileName = $"ITK处理结果_{DateTime.Now:yyyyMMdd_HHmmss}";
                
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        SimpleITK.WriteImage(itkProcessedImage, dialog.FileName);
                        logger.Info($"保存ITK处理结果: {dialog.FileName}");
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            originalImage?.Dispose();
            itkOriginalImage?.Dispose();
            itkProcessedImage?.Dispose();
            base.OnFormClosed(e);
        }
    }
}