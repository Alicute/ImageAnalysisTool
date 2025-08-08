# 像素映射分析器 - 整合版

一个专门用于分析16位DICOM图像像素映射数据的Python工具，整合了所有分析功能到统一的GUI界面。

## 🎯 项目特点

### 核心功能
- **智能数据解析**: 支持多种格式的像素数据文件
- **流式读取**: 支持大文件处理，内存高效
- **算法自动识别**: 自动识别常量、线性、分段等算法类型
- **综合分析**: 提供详细的分析报告和可视化
- **模块化设计**: 清晰的代码结构，易于扩展

### 支持的文件格式
1. **DICOM格式**: `===完整16位DICOM像素映射数据===`
2. **简单映射**: `原值X→新值Y`
3. **标准格式**: `[001]位置(0,0)原值65535→新值38730`

## 🚀 快速开始

### 安装依赖
```bash
pip install PyQt5 pandas numpy matplotlib seaborn scipy
```

### 运行程序
```bash
python main.py
```

### 使用流程
1. **加载数据文件**: 选择你的像素数据文件
2. **查看数据信息**: 检查数据统计和预览
3. **运行分析**: 选择分析类型并执行
4. **查看结果**: 查看分析结果和可视化图表
5. **生成报告**: 导出详细的分析报告

## 📁 项目结构

```
analysis/
├── main.py                    # 主程序入口
├── test_core.py              # 核心功能测试
├── project_structure_test.py  # 项目结构测试
├── analysis/                 # 分析模块
│   ├── __init__.py
│   ├── core/                 # 核心模块
│   │   ├── file_parser.py    # 文件解析器
│   │   ├── data_manager.py   # 数据管理器
│   │   └── algorithm_engine.py # 算法引擎
│   ├── algorithms/           # 算法模块
│   │   ├── mapping_analyzer.py    # 映射分析
│   │   ├── algorithm_deriver.py   # 算法推导
│   │   ├── model_fitter.py        # 模型拟合
│   │   └── comparator.py          # 算法比较
│   ├── gui/                  # GUI模块
│   │   ├── main_window.py        # 主窗口
│   │   ├── data_panel.py         # 数据面板
│   │   ├── analysis_panel.py     # 分析面板
│   │   └── visualization_panel.py # 可视化面板
│   ├── utils/                # 工具模块
│   │   └── report_generator.py   # 报告生成器
│   └── resources/            # 资源文件
│       └── __init__.py
└── requirements.txt          # 依赖列表
```

## 🎨 用户界面

### 主要功能面板

#### 1. 数据处理面板
- 文件选择和加载
- 采样选项设置
- 数据信息显示
- 数据预览表格
- 数据导出功能

#### 2. 算法分析面板
- 分析类型选择
- 实时分析结果
- 算法信息显示
- 结果保存和复制

#### 3. 可视化面板
- 多种图表类型
- 交互式图表
- 图表保存功能
- 自定义采样设置

### 支持的分析类型

#### 综合分析
- 基础统计分析
- 映射模式识别
- 线性关系检测
- 分段算法识别
- 模型拟合评估

#### 线性分析
- 线性回归分析
- 相关系数计算
- 拟合优度评估
- 误差分析

#### 分段分析
- 转折点检测
- 分段线性拟合
- 段数优化
- 边界分析

#### 算法推导
- 自动算法类型识别
- 参数优化
- 置信度评估
- 算法公式生成

## 🔧 技术特性

### 性能优化
- **流式读取**: 支持大文件逐行读取
- **内存管理**: 自动采样和垃圾回收
- **数据优化**: 智能数据类型选择
- **缓存机制**: 分析结果缓存

### 算法能力
- **常量算法检测**: 识别全局统一偏移
- **线性算法识别**: 检测线性映射关系
- **分段算法分析**: 识别复杂的分段函数
- **模型拟合**: 支持多种数学模型

### 可视化功能
- **散点图**: 原始值vs目标值关系
- **直方图**: 数据分布分析
- **热力图**: 密度分布可视化
- **对比图**: 多维度对比分析

## 📊 输出结果

### 分析报告
- **Markdown格式**: 结构化的分析报告
- **算法推断**: 自动算法类型和参数
- **统计信息**: 详细的数据统计
- **可视化结果**: 图表和分析结果

### 数据导出
- **CSV格式**: 处理后的数据导出
- **映射摘要**: 映射关系汇总
- **验证结果**: 算法验证数据

## 🛠️ 开发说明

### 代码架构
- **模块化设计**: 清晰的功能分离
- **统一接口**: 标准化的API设计
- **可扩展性**: 易于添加新功能
- **错误处理**: 完善的异常处理

### 核心组件

#### FileParser
- 文件格式检测
- 流式数据读取
- 数据解析和验证
- 采样支持

#### DataManager
- 数据加载和管理
- 统计分析
- 内存优化
- 数据导出

#### AlgorithmEngine
- 算法分析调度
- 结果整合
- 置信度评估
- 报告生成

## 📝 使用示例

### 基本使用
```python
# 创建分析器
from analysis.core.algorithm_engine import AlgorithmEngine
from analysis.core.data_manager import DataManager

# 初始化
data_manager = DataManager()
algorithm_engine = AlgorithmEngine()

# 加载数据
success, message = data_manager.load_data("data.txt")

# 运行分析
results = algorithm_engine.analyze_mapping("comprehensive")

# 生成报告
report = algorithm_engine.get_analysis_summary()
```

### 高级使用
```python
# 自定义分析
from analysis.algorithms.mapping_analyzer import MappingAnalyzer

analyzer = MappingAnalyzer()

# 线性分析
linear_result = analyzer.analyze_linear_mapping(data)

# 分段分析
piecewise_result = analyzer.find_piecewise_mapping(data)

# 分布分析
distribution_result = analyzer.analyze_mapping_distribution(data)
```

## 🔍 故障排除

### 常见问题

#### 1. 依赖安装失败
```bash
# 升级pip
python -m pip install --upgrade pip

# 安装依赖
pip install PyQt5 pandas numpy matplotlib seaborn scipy
```

#### 2. 大文件处理
- 使用采样功能减少内存使用
- 调整采样率平衡性能和精度
- 定期清理缓存释放内存

#### 3. 格式识别失败
- 检查文件编码是否为UTF-8
- 确认文件格式符合支持的标准
- 查看错误信息了解具体问题

## 📈 性能指标

### 处理能力
- **文件大小**: 支持600MB+大文件
- **数据量**: 处理850万+像素点
- **内存使用**: 优化后<1GB
- **处理速度**: 流式读取<5分钟

### 算法精度
- **常量算法**: 100%准确率
- **线性算法**: R²>0.95高精度
- **分段算法**: 自动转折点检测
- **复杂算法**: 多模型拟合评估

## 🔄 更新日志

### v1.0.0 (2024-08-07)
- ✅ 完成项目整合
- ✅ 统一GUI界面
- ✅ 流式读取功能
- ✅ 模块化架构
- ✅ 完整功能支持

## 📞 支持和反馈

如果遇到问题或有改进建议，请：
1. 检查本文档的故障排除部分
2. 查看项目的错误日志
3. 提供详细的问题描述和复现步骤

---

**像素映射分析器** - 专业的X射线图像增强算法分析工具