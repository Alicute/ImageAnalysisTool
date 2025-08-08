"""
主程序入口
整合版像素映射分析器
"""

import sys
import os

# 添加项目根目录到Python路径
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from analysis.gui.main_window import main

if __name__ == '__main__':
    main()