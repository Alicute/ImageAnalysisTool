# 图像处理工具项目

基于D.cs文件分析构建的现代化图像处理工具，提供简洁易用的GUI界面和丰富的图像处理功能。

## 项目概述

本项目通过分析一个包含28,560行代码的大型图像处理应用程序（D.cs），提取其核心算法，并使用现代化的C#技术栈重新实现。

## 功能特性

### 🎯 核心功能
- **图像加载与保存**: 支持常见图像格式（PNG、JPEG、BMP等）
- **实时预览**: 参数调整时即时显示处理效果
- **撤销/重做**: 完整的操作历史管理
- **批处理**: 支持批量图像处理

### 🔧 图像处理算法
- **滤波器**
  - 高斯模糊（支持可分离滤波优化）
  - 中值滤波
  - 双边滤波
  - USM锐化
- **图像增强**
  - 亮度/对比度调整
  - 直方图均衡化
  - 伽马校正
  - 阴影/高光调整
- **边缘检测**
  - Canny边缘检测
  - Sobel算子
  - 拉普拉斯算子

## 技术架构

### 🏗️ 技术栈
- **UI框架**: WPF (.NET 6+)
- **架构模式**: MVVM
- **图像处理**: 自定义算法实现
- **异步处理**: async/await
- **依赖注入**: Microsoft.Extensions.DependencyInjection

### 📁 项目结构
```
ImageProcessingTool/
├── src/
│   ├── ImageProcessingTool.Core/          # 核心算法库
│   │   ├── Algorithms/                    # 算法实现
│   │   ├── Models/                        # 数据模型
│   │   ├── Interfaces/                    # 接口定义
│   │   └── Utils/                         # 工具类
│   ├── ImageProcessingTool.UI/            # WPF用户界面
│   │   ├── Views/                         # 视图
│   │   ├── ViewModels/                    # 视图模型
│   │   └── Controls/                      # 自定义控件
│   └── ImageProcessingTool.Tests/         # 单元测试
├── docs/                                  # 文档
├── samples/                               # 示例图片
└── README.md
```

## 快速开始

### 📋 环境要求
- Visual Studio 2022
- .NET 6.0 或更高版本
- Windows 10/11

### 🚀 安装步骤

1. **克隆项目**
```bash
git clone https://github.com/your-username/ImageProcessingTool.git
cd ImageProcessingTool
```

2. **创建解决方案**
```bash
dotnet new sln -n ImageProcessingTool

# 创建项目
dotnet new classlib -n ImageProcessingTool.Core
dotnet new wpf -n ImageProcessingTool.UI
dotnet new xunit -n ImageProcessingTool.Tests

# 添加到解决方案
dotnet sln add ImageProcessingTool.Core
dotnet sln add ImageProcessingTool.UI  
dotnet sln add ImageProcessingTool.Tests

# 添加项目引用
cd ImageProcessingTool.UI
dotnet add reference ../ImageProcessingTool.Core
cd ../ImageProcessingTool.Tests
dotnet add reference ../ImageProcessingTool.Core
```

3. **编译运行**
```bash
dotnet build
cd ImageProcessingTool.UI
dotnet run
```

### 🎮 使用方法

1. **打开图像**: 点击"文件" → "打开"选择图像文件
2. **应用滤镜**: 在左侧功能面板选择所需的滤镜
3. **调整参数**: 使用滑块调整滤镜参数，实时预览效果
4. **保存结果**: 点击"文件" → "保存"导出处理后的图像

## 文档

### 📚 详细文档
- [D.cs文件分析报告](D.cs文件分析报告.md) - 原始代码的详细分析
- [简单GUI图像处理工具设计方案](简单GUI图像处理工具设计方案.md) - 项目架构设计
- [项目实施指南](项目实施指南.md) - 详细的开发指南
- [算法提取示例-高斯模糊](算法提取示例-高斯模糊.md) - 算法提取和重构示例

### 🔍 核心概念

#### ImageData类
```csharp
public class ImageData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Channels { get; set; }
    public byte[] PixelData { get; set; }
    public PixelFormat Format { get; set; }
    public string FilePath { get; set; }
}
```

#### 滤波器接口
```csharp
public interface IImageFilter
{
    string Name { get; }
    string Description { get; }
    Task<ImageData> ApplyAsync(ImageData input, object parameters = null);
}
```

## 开发指南

### 🔧 添加新滤镜

1. **实现IImageFilter接口**
```csharp
public class MyFilter : IImageFilter
{
    public string Name => "我的滤镜";
    public string Description => "滤镜描述";
    
    public async Task<ImageData> ApplyAsync(ImageData input, object parameters = null)
    {
        // 实现算法逻辑
        return await Task.Run(() => ProcessImage(input, parameters));
    }
}
```

2. **注册到依赖注入容器**
3. **在UI中添加对应的控件**

### 🧪 性能优化建议

- 使用`Span<T>`和`Memory<T>`进行高效内存操作
- 利用`Parallel.For`进行并行处理
- 实现可分离滤波器减少计算复杂度
- 使用内存池复用临时缓冲区

## 贡献指南

### 🤝 如何贡献

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建 Pull Request

### 📝 代码规范

- 遵循C#编码规范
- 添加XML文档注释
- 编写单元测试
- 保持代码简洁可读

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 致谢

- 感谢原始D.cs文件提供的丰富算法实现参考
- 感谢开源社区提供的优秀图像处理算法

## 联系方式

- 项目主页: [GitHub Repository](https://github.com/your-username/ImageProcessingTool)
- 问题反馈: [Issues](https://github.com/your-username/ImageProcessingTool/issues)

---

⭐ 如果这个项目对您有帮助，请给我们一个星标！
