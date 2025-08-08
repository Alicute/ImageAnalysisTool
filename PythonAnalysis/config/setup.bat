@echo off
echo 正在安装像素映射分析器依赖...
echo.

REM 检查Python是否安装
python --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未检测到Python，请先安装Python
    echo 下载地址: https://www.python.org/downloads/
    pause
    exit /b 1
)

echo 检测到Python版本:
python --version
echo.

REM 升级pip
echo 正在升级pip...
python -m pip install --upgrade pip
echo.

REM 安装依赖
echo 正在安装依赖包...
pip install -r requirements.txt
echo.

REM 检查安装是否成功
echo 验证安装...
python -c "import PyQt5, pandas, numpy, matplotlib, seaborn, scipy; print('✓ 所有依赖安装成功')"

if %errorlevel% neq 0 (
    echo.
    echo 错误: 依赖安装失败，请检查网络连接或手动安装
    pause
    exit /b 1
)

echo.
echo ====================================
echo   像素映射分析器环境配置完成
echo ====================================
echo.
echo 使用方法:
echo   1. 将你的像素数据文件放到此目录
echo   2. 运行: python pixel_analyzer.py
echo   3. 在GUI中加载并分析数据
echo.
pause