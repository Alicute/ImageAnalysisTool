"""
主窗口模块
基于pixel_analyzer.py的统一GUI框架
"""

import sys
import os
from PyQt5.QtWidgets import *
from PyQt5.QtCore import *
from PyQt5.QtGui import *
import matplotlib.pyplot as plt
from matplotlib.backends.backend_qt5agg import FigureCanvasQTAgg as FigureCanvas
import seaborn as sns
from typing import Optional, Dict, Any

from .data_panel import DataPanel
from .analysis_panel import AnalysisPanel
from .visualization_panel import VisualizationPanel
from ..core.data_manager import DataManager
from ..core.algorithm_engine import AlgorithmEngine
from ..utils.report_generator import ReportGenerator


class MainWindow(QMainWindow):
    """主窗口类"""
    
    def __init__(self):
        super().__init__()
        self.data_manager = DataManager()
        self.algorithm_engine = AlgorithmEngine()
        self.report_generator = ReportGenerator()
        
        self.current_file = ""
        self.setup_ui()
        self.setup_connections()
        
    def setup_ui(self):
        """设置用户界面"""
        self.setWindowTitle("像素映射分析器 - 整合版")
        self.setGeometry(100, 100, 1400, 900)
        
        # 设置应用图标
        self.setWindowIcon(QIcon())
        
        # 创建中央部件
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        # 创建主布局
        main_layout = QVBoxLayout(central_widget)
        
        # 创建工具栏
        self.create_toolbar()
        
        # 创建标签页
        self.tab_widget = QTabWidget()
        main_layout.addWidget(self.tab_widget)
        
        # 创建各个面板
        self.data_panel = DataPanel(self.data_manager)
        self.analysis_panel = AnalysisPanel(self.algorithm_engine)
        self.visualization_panel = VisualizationPanel(self.data_manager, self.algorithm_engine)
        
        # 添加标签页
        self.tab_widget.addTab(self.data_panel, "数据处理")
        self.tab_widget.addTab(self.analysis_panel, "算法分析")
        self.tab_widget.addTab(self.visualization_panel, "可视化")
        
        # 创建状态栏
        self.status_bar = self.statusBar()
        self.status_bar.showMessage("就绪")
        
        # 创建菜单栏
        self.create_menu_bar()
        
    def create_toolbar(self):
        """创建工具栏"""
        toolbar = self.addToolBar("主工具栏")
        toolbar.setIconSize(QSize(24, 24))
        
        # 打开文件
        open_action = QAction("打开文件", self)
        open_action.setStatusTip("打开数据文件")
        open_action.triggered.connect(self.open_file)
        toolbar.addAction(open_action)
        
        toolbar.addSeparator()
        
        # 分析数据
        analyze_action = QAction("分析数据", self)
        analyze_action.setStatusTip("分析当前数据")
        analyze_action.triggered.connect(self.analyze_data)
        toolbar.addAction(analyze_action)
        
        # 生成报告
        report_action = QAction("生成报告", self)
        report_action.setStatusTip("生成分析报告")
        report_action.triggered.connect(self.generate_report)
        toolbar.addAction(report_action)
        
        toolbar.addSeparator()
        
        # 导出数据
        export_action = QAction("导出数据", self)
        export_action.setStatusTip("导出处理后的数据")
        export_action.triggered.connect(self.export_data)
        toolbar.addAction(export_action)
        
    def create_menu_bar(self):
        """创建菜单栏"""
        menubar = self.menuBar()
        
        # 文件菜单
        file_menu = menubar.addMenu("文件")
        
        open_action = QAction("打开文件", self)
        open_action.setShortcut(QKeySequence.Open)
        open_action.triggered.connect(self.open_file)
        file_menu.addAction(open_action)
        
        file_menu.addSeparator()
        
        exit_action = QAction("退出", self)
        exit_action.setShortcut(QKeySequence.Quit)
        exit_action.triggered.connect(self.close)
        file_menu.addAction(exit_action)
        
        # 分析菜单
        analysis_menu = menubar.addMenu("分析")
        
        basic_analysis_action = QAction("基础分析", self)
        basic_analysis_action.triggered.connect(lambda: self.run_analysis('basic'))
        analysis_menu.addAction(basic_analysis_action)
        
        linear_analysis_action = QAction("线性分析", self)
        linear_analysis_action.triggered.connect(lambda: self.run_analysis('linear'))
        analysis_menu.addAction(linear_analysis_action)
        
        piecewise_analysis_action = QAction("分段分析", self)
        piecewise_analysis_action.triggered.connect(lambda: self.run_analysis('piecewise'))
        analysis_menu.addAction(piecewise_analysis_action)
        
        comprehensive_analysis_action = QAction("综合分析", self)
        comprehensive_analysis_action.triggered.connect(lambda: self.run_analysis('comprehensive'))
        analysis_menu.addAction(comprehensive_analysis_action)
        
        # 工具菜单
        tools_menu = menubar.addMenu("工具")
        
        export_mapping_action = QAction("导出映射摘要", self)
        export_mapping_action.triggered.connect(self.export_mapping_summary)
        tools_menu.addAction(export_mapping_action)
        
        clear_cache_action = QAction("清除缓存", self)
        clear_cache_action.triggered.connect(self.clear_cache)
        tools_menu.addAction(clear_cache_action)
        
        # 帮助菜单
        help_menu = menubar.addMenu("帮助")
        
        about_action = QAction("关于", self)
        about_action.triggered.connect(self.show_about)
        help_menu.addAction(about_action)
        
    def setup_connections(self):
        """设置信号连接"""
        # 数据面板信号
        self.data_panel.data_loaded.connect(self.on_data_loaded)
        self.data_panel.data_cleared.connect(self.on_data_cleared)
        
        # 算法引擎信号
        self.algorithm_engine.analysis_completed.connect(self.on_analysis_completed)
        
    def open_file(self):
        """打开文件对话框"""
        file_path, _ = QFileDialog.getOpenFileName(
            self, "选择数据文件", "", 
            "文本文件 (*.txt);;所有文件 (*.*)"
        )
        
        if file_path:
            self.load_file(file_path)
    
    def load_file(self, file_path: str):
        """加载文件"""
        self.current_file = file_path
        self.status_bar.showMessage(f"正在加载文件: {file_path}")
        
        # 在数据面板中加载文件
        self.tab_widget.setCurrentWidget(self.data_panel)
        self.data_panel.load_file(file_path)
        
    def on_data_loaded(self, success: bool, message: str):
        """数据加载完成回调"""
        if success:
            self.status_bar.showMessage(f"数据加载成功: {message}")
            # 更新其他面板
            self.analysis_panel.update_data_info()
            self.visualization_panel.update_data_info()
        else:
            self.status_bar.showMessage(f"数据加载失败: {message}")
            QMessageBox.warning(self, "加载失败", message)
    
    def on_data_cleared(self):
        """数据清除回调"""
        self.status_bar.showMessage("数据已清除")
        self.algorithm_engine.clear_analysis()
        self.analysis_panel.clear_results()
        self.visualization_panel.clear_plots()
    
    def analyze_data(self):
        """分析数据"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "请先加载数据")
            return
        
        self.status_bar.showMessage("正在分析数据...")
        self.tab_widget.setCurrentWidget(self.analysis_panel)
        self.analysis_panel.run_comprehensive_analysis()
    
    def run_analysis(self, analysis_type: str):
        """运行指定类型的分析"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "请先加载数据")
            return
        
        self.status_bar.showMessage(f"正在运行{analysis_type}分析...")
        self.tab_widget.setCurrentWidget(self.analysis_panel)
        self.analysis_panel.run_analysis(analysis_type)
    
    def on_analysis_completed(self, results: Dict[str, Any]):
        """分析完成回调"""
        self.status_bar.showMessage("分析完成")
        # 更新可视化面板
        self.visualization_panel.update_analysis_results(results)
    
    def generate_report(self):
        """生成报告"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "请先加载数据并进行分析")
            return
        
        file_path, _ = QFileDialog.getSaveFileName(
            self, "保存分析报告", "", 
            "Markdown文件 (*.md);;所有文件 (*.*)"
        )
        
        if file_path:
            try:
                report = self.report_generator.generate_comprehensive_report(
                    self.data_manager, 
                    self.algorithm_engine
                )
                
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(report)
                
                QMessageBox.information(self, "成功", f"报告已保存到: {file_path}")
                self.status_bar.showMessage(f"报告已生成: {file_path}")
                
            except Exception as e:
                QMessageBox.critical(self, "错误", f"报告生成失败: {str(e)}")
    
    def export_data(self):
        """导出数据"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "没有可导出的数据")
            return
        
        file_path, _ = QFileDialog.getSaveFileName(
            self, "导出数据", "", 
            "CSV文件 (*.csv);;Excel文件 (*.xlsx);;所有文件 (*.*)"
        )
        
        if file_path:
            try:
                if file_path.endswith('.csv'):
                    self.data_manager.current_data.to_csv(file_path, index=False, encoding='utf-8-sig')
                elif file_path.endswith('.xlsx'):
                    self.data_manager.current_data.to_excel(file_path, index=False)
                else:
                    # 默认保存为CSV
                    file_path += '.csv'
                    self.data_manager.current_data.to_csv(file_path, index=False, encoding='utf-8-sig')
                
                QMessageBox.information(self, "成功", f"数据已导出到: {file_path}")
                self.status_bar.showMessage(f"数据已导出: {file_path}")
                
            except Exception as e:
                QMessageBox.critical(self, "错误", f"数据导出失败: {str(e)}")
    
    def export_mapping_summary(self):
        """导出映射摘要"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "没有可导出的数据")
            return
        
        file_path, _ = QFileDialog.getSaveFileName(
            self, "导出映射摘要", "", 
            "CSV文件 (*.csv);;所有文件 (*.*)"
        )
        
        if file_path:
            success, message = self.data_manager.export_mapping_summary(file_path)
            if success:
                QMessageBox.information(self, "成功", message)
                self.status_bar.showMessage("映射摘要已导出")
            else:
                QMessageBox.critical(self, "错误", message)
    
    def clear_cache(self):
        """清除缓存"""
        reply = QMessageBox.question(
            self, "确认", "确定要清除所有数据和缓存吗？",
            QMessageBox.Yes | QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            self.data_manager.clear_data()
            self.algorithm_engine.clear_analysis()
            self.data_panel.clear_data()
            self.analysis_panel.clear_results()
            self.visualization_panel.clear_plots()
            self.status_bar.showMessage("缓存已清除")
    
    def show_about(self):
        """显示关于对话框"""
        QMessageBox.about(self, "关于", 
                         "像素映射分析器 - 整合版\n\n"
                         "一个用于分析X射线图像增强算法的专业工具\n"
                         "支持多种数据格式和分析方法\n\n"
                         "版本: 1.0\n"
                         "基于PyQt5开发")
    
    def closeEvent(self, event):
        """关闭事件"""
        reply = QMessageBox.question(
            self, "确认退出", "确定要退出程序吗？",
            QMessageBox.Yes | QMessageBox.No
        )
        
        if reply == QMessageBox.Yes:
            # 清理资源
            self.data_manager.clear_data()
            event.accept()
        else:
            event.ignore()


def main():
    """主函数"""
    app = QApplication(sys.argv)
    
    # 设置应用样式
    app.setStyle('Fusion')
    
    # 创建主窗口
    window = MainWindow()
    window.show()
    
    # 运行应用
    sys.exit(app.exec_())


if __name__ == '__main__':
    main()