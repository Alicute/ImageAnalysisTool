#!/bin/bash

echo "正在安装像素映射分析器依赖..."
echo ""

# 检查Python是否安装
if ! command -v python3 &> /dev/null; then
    echo "错误: 未检测到Python3，请先安装Python"
    echo "Ubuntu/Debian: sudo apt-get install python3 python3-pip"
    echo "CentOS/RHEL: sudo yum install python3 python3-pip"
    echo "macOS: brew install python3"
    exit 1
fi

echo "检测到Python版本:"
python3 --version
echo ""

# 升级pip
echo "正在升级pip..."
python3 -m pip install --upgrade pip
echo ""

# 安装依赖
echo "正在安装依赖包..."
python3 -m pip install -r requirements.txt
echo ""

# 检查安装是否成功
echo "验证安装..."
python3 -c "import PyQt5, pandas, numpy, matplotlib, seaborn, scipy; print('✓ 所有依赖安装成功')"

if [ $? -ne 0 ]; then
    echo ""
    echo "错误: 依赖安装失败，请检查网络连接或手动安装"
    exit 1
fi

echo ""
echo "===================================="
echo "  像素映射分析器环境配置完成"
echo "===================================="
echo ""
echo "使用方法:"
echo "  1. 将你的像素数据文件放到此目录"
echo "  2. 运行: python3 pixel_analyzer.py"
echo "  3. 在GUI中加载并分析数据"
echo ""