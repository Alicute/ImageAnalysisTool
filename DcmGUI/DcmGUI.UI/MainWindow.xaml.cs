using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DcmGUI.Core.Models;
using DcmGUI.Core.Utils;
using DcmGUI.Core.Algorithms.Filters;

namespace DcmGUI.UI;

/// <summary>
/// 主窗口交互逻辑
/// </summary>
public partial class MainWindow : Window
{
    private ImageData? _currentImage;
    private ImageData? _originalImage;
    private readonly DicomImageManager _imageManager;
    private readonly GaussianBlurFilter _gaussianFilter;
    private readonly BrightnessContrastFilter _brightnessContrastFilter;
    private readonly WindowLevelFilter _windowLevelFilter;
    private bool _isProcessing = false;

    public MainWindow()
    {
        InitializeComponent();
        _imageManager = new DicomImageManager();
        _gaussianFilter = new GaussianBlurFilter();
        _brightnessContrastFilter = new BrightnessContrastFilter();
        _windowLevelFilter = new WindowLevelFilter();

        // 设置键盘快捷键
        SetupKeyBindings();
    }

    private void SetupKeyBindings()
    {
        // Ctrl+O 打开
        var openCommand = new RoutedCommand();
        var openBinding = new KeyBinding(openCommand, Key.O, ModifierKeys.Control);
        InputBindings.Add(openBinding);
        CommandBindings.Add(new CommandBinding(openCommand, OpenImage_Click));

        // Ctrl+S 保存
        var saveCommand = new RoutedCommand();
        var saveBinding = new KeyBinding(saveCommand, Key.S, ModifierKeys.Control);
        InputBindings.Add(saveBinding);
        CommandBindings.Add(new CommandBinding(saveCommand, SaveImage_Click));
    }

    #region 文件操作

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = DicomImageManager.GetSupportedFormatsFilter(),
            Title = "选择DICOM文件"
        };

        if (dialog.ShowDialog() == true)
        {
            await LoadImageAsync(dialog.FileName);
        }
    }

    private async Task LoadImageAsync(string filePath)
    {
        try
        {
            SetProcessingStatus("正在加载图像...", true);

            var stopwatch = Stopwatch.StartNew();
            _currentImage = await _imageManager.LoadImageAsync(filePath);
            _originalImage = _imageManager.CreateBackup(_currentImage);
            stopwatch.Stop();

            if (_currentImage != null)
            {
                // 自动计算窗宽窗位
                _currentImage.AutoCalculateWindowLevel();

                DisplayImage(_currentImage);
                UpdateImageInfo(_currentImage);
                NoImageText.Visibility = Visibility.Collapsed;
                SetProcessingStatus($"图像加载完成 ({stopwatch.ElapsedMilliseconds}ms)", false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载图像失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetProcessingStatus("加载失败", false);
        }
    }

    private async void SaveImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            MessageBox.Show("没有图像可保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrEmpty(_currentImage.FilePath))
        {
            await SaveImageAsync(_currentImage.FilePath);
        }
        else
        {
            SaveAsImage_Click(sender, e);
        }
    }

    private async void SaveAsImage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            MessageBox.Show("没有图像可保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = DicomImageManager.GetSaveFormatsFilter(),
            Title = "保存DICOM图像",
            FileName = Path.GetFileNameWithoutExtension(_currentImage.FilePath ?? "image") + "_processed"
        };

        if (dialog.ShowDialog() == true)
        {
            await SaveImageAsync(dialog.FileName);
        }
    }

    private async Task SaveImageAsync(string filePath)
    {
        try
        {
            SetProcessingStatus("正在保存图像...", true);

            var stopwatch = Stopwatch.StartNew();
            bool success = await _imageManager.SaveImageAsync(_currentImage, filePath);
            stopwatch.Stop();

            if (success)
            {
                SetProcessingStatus($"图像保存完成 ({stopwatch.ElapsedMilliseconds}ms)", false);
                MessageBox.Show("保存成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("保存失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                SetProcessingStatus("保存失败", false);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetProcessingStatus("保存失败", false);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region 编辑操作

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 实现撤销功能
        MessageBox.Show("撤销功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        // TODO: 实现重做功能
        MessageBox.Show("重做功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_originalImage != null)
        {
            _currentImage = _imageManager.RestoreFromBackup(_originalImage);
            if (_currentImage != null)
            {
                // 重新计算窗宽窗位
                _currentImage.AutoCalculateWindowLevel();
                DisplayImage(_currentImage);
                UpdateImageInfo(_currentImage);
                SetProcessingStatus("已重置到原始图像", false);
            }
        }
    }

    /// <summary>
    /// 自动窗宽窗位调节
    /// </summary>
    private void AutoWindowLevel_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage != null)
        {
            _currentImage.AutoCalculateWindowLevel();
            DisplayImage(_currentImage);
            UpdateImageInfo(_currentImage);
            SetProcessingStatus("窗宽窗位已自动调节", false);
        }
    }

    #endregion

    #region 滤镜操作

    private async void GaussianBlur_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            MessageBox.Show("请先打开DICOM图像", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isProcessing)
        {
            MessageBox.Show("正在处理中，请稍候", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SetProcessingStatus("正在应用高斯模糊...", true);

            var parameters = new GaussianBlurParameters
            {
                Radius = 2.0f,
                SeparableFilter = true
            };

            var stopwatch = Stopwatch.StartNew();
            var result = await _gaussianFilter.ApplyAsync(_currentImage, parameters);
            stopwatch.Stop();

            _currentImage = result;
            DisplayImage(_currentImage);
            SetProcessingStatus($"高斯模糊完成 ({stopwatch.ElapsedMilliseconds}ms)", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"处理失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetProcessingStatus("处理失败", false);
        }
    }

    private void MedianFilter_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("中值滤波功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BilateralFilter_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("双边滤波功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void BrightnessContrast_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage == null)
        {
            MessageBox.Show("请先打开DICOM图像", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_isProcessing)
        {
            MessageBox.Show("正在处理中，请稍候", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            SetProcessingStatus("正在调整亮度对比度...", true);

            var parameters = new BrightnessContrastParameters
            {
                Brightness = 10.0f,  // 增加亮度
                Contrast = 20.0f     // 增加对比度
            };

            var stopwatch = Stopwatch.StartNew();
            var result = await _brightnessContrastFilter.ApplyAsync(_currentImage, parameters);
            stopwatch.Stop();

            _currentImage = result;
            DisplayImage(_currentImage);
            SetProcessingStatus($"亮度对比度调整完成 ({stopwatch.ElapsedMilliseconds}ms)", false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"处理失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            SetProcessingStatus("处理失败", false);
        }
    }

    private void HistogramEqualization_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("直方图均衡化功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void GammaCorrection_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("伽马校正功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region 视图操作

    private void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage != null)
        {
            // TODO: 实现适应窗口功能
            MessageBox.Show("适应窗口功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ActualSize_Click(object sender, RoutedEventArgs e)
    {
        if (_currentImage != null)
        {
            // TODO: 实现实际大小功能
            MessageBox.Show("实际大小功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    #endregion

    #region 帮助

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("DcmGUI v1.0\n基于D.cs分析构建的图像处理工具\n\n功能特性:\n- 高斯模糊滤波\n- 图像加载和保存\n- 实时预览",
            "关于", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region 辅助方法

    private void DisplayImage(ImageData imageData)
    {
        if (imageData?.IsValid() == true)
        {
            var bitmapSource = imageData.ToBitmapSource();
            if (bitmapSource != null)
            {
                ImageDisplay.Source = bitmapSource;
                NoImageText.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void UpdateImageInfo(ImageData imageData)
    {
        if (imageData?.IsValid() == true)
        {
            ImageInfoText.Text = imageData.GetImageInfo();
        }
        else
        {
            ImageInfoText.Text = "就绪";
        }
    }

    private void SetProcessingStatus(string message, bool isProcessing)
    {
        _isProcessing = isProcessing;
        ProcessingStatusText.Text = message;
        ProcessingProgressBar.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;

        if (!isProcessing)
        {
            ProcessingTimeText.Text = "";
        }
    }

    #endregion
}