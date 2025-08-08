"""
可视化面板模块
"""

from PyQt5.QtWidgets import *
from PyQt5.QtCore import *
from PyQt5.QtGui import *
import matplotlib.pyplot as plt
from matplotlib.backends.backend_qt5agg import FigureCanvasQTAgg as FigureCanvas
from matplotlib.figure import Figure
import seaborn as sns
import pandas as pd
import numpy as np
from typing import Dict, Any, Optional

from ..core.data_manager import DataManager
from ..core.algorithm_engine import AlgorithmEngine


class VisualizationPanel(QWidget):
    """可视化面板"""
    
    def __init__(self, data_manager: DataManager, algorithm_engine: AlgorithmEngine):
        super().__init__()
        self.data_manager = data_manager
        self.algorithm_engine = algorithm_engine
        self.setup_ui()
        self.setup_connections()
        
    def setup_ui(self):
        """设置用户界面"""
        layout = QVBoxLayout(self)
        
        # 图表选项区域
        options_group = QGroupBox("图表选项")
        options_layout = QGridLayout(options_group)
        
        options_layout.addWidget(QLabel("图表类型:"), 0, 0)
        self.chart_type_combo = QComboBox()
        self.chart_type_combo.addItems([
            "散点图", "直方图", "热力图", "对比图"
        ])
        options_layout.addWidget(self.chart_type_combo, 0, 1)
        
        options_layout.addWidget(QLabel("采样数量:"), 1, 0)
        self.sample_size_spin = QSpinBox()
        self.sample_size_spin.setRange(1000, 100000)
        self.sample_size_spin.setValue(10000)
        self.sample_size_spin.setSingleStep(1000)
        options_layout.addWidget(self.sample_size_spin, 1, 1)
        
        self.generate_button = QPushButton("生成图表")
        options_layout.addWidget(self.generate_button, 2, 0, 1, 2)
        
        layout.addWidget(options_group)
        
        # 图表显示区域
        self.chart_widget = ChartWidget()
        layout.addWidget(self.chart_widget)
        
        # 图表控制区域
        control_group = QGroupBox("图表控制")
        control_layout = QHBoxLayout(control_group)
        
        self.save_button = QPushButton("保存图表")
        control_layout.addWidget(self.save_button)
        
        self.clear_button = QPushButton("清除图表")
        control_layout.addWidget(self.clear_button)
        
        control_layout.addStretch()
        
        layout.addWidget(control_group)
        
    def setup_connections(self):
        """设置信号连接"""
        self.generate_button.clicked.connect(self.generate_chart)
        self.save_button.clicked.connect(self.save_chart)
        self.clear_button.clicked.connect(self.clear_chart)
        
    def generate_chart(self):
        """生成图表"""
        if self.data_manager.current_data is None:
            QMessageBox.warning(self, "警告", "请先加载数据")
            return
            
        chart_type = self.chart_type_combo.currentText()
        sample_size = self.sample_size_spin.value()
        
        try:
            if chart_type == "散点图":
                self.create_scatter_plot(sample_size)
            elif chart_type == "直方图":
                self.create_histogram(sample_size)
            elif chart_type == "热力图":
                self.create_heatmap(sample_size)
            elif chart_type == "对比图":
                self.create_comparison_plot(sample_size)
                
        except Exception as e:
            QMessageBox.critical(self, "错误", f"生成图表失败: {str(e)}")
            
    def create_scatter_plot(self, sample_size: int):
        """创建散点图"""
        data = self.data_manager.get_data(sample_only=True)
        if len(data) > sample_size:
            data = data.sample(sample_size, random_state=42)
            
        fig, ax = plt.subplots(figsize=(10, 8))
        
        # 创建散点图
        scatter = ax.scatter(
            data['original_value'], 
            data['target_value'],
            c=data['change'], 
            cmap='RdYlBu_r', 
            alpha=0.6, 
            s=2
        )
        
        ax.set_xlabel('Original Value')
        ax.set_ylabel('Target Value')
        ax.set_title('Pixel Value Mapping')
        ax.grid(True, alpha=0.3)
        
        # 添加颜色条
        cbar = plt.colorbar(scatter, ax=ax)
        cbar.set_label('Change Amount')
        
        # 添加对角线
        min_val = min(data['original_value'].min(), data['target_value'].min())
        max_val = max(data['original_value'].max(), data['target_value'].max())
        ax.plot([min_val, max_val], [min_val, max_val], 'r--', alpha=0.5, label='Identity Line')
        ax.legend()
        
        self.chart_widget.set_figure(fig)
        
    def create_histogram(self, sample_size: int):
        """创建直方图"""
        data = self.data_manager.get_data(sample_only=True)
        
        fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 5))
        
        # 原始值直方图
        ax1.hist(data['original_value'], bins=50, alpha=0.7, color='blue', edgecolor='black')
        ax1.set_xlabel('Original Value')
        ax1.set_ylabel('Frequency')
        ax1.set_title('Original Value Distribution')
        ax1.grid(True, alpha=0.3)
        
        # 目标值直方图
        ax2.hist(data['target_value'], bins=50, alpha=0.7, color='red', edgecolor='black')
        ax2.set_xlabel('Target Value')
        ax2.set_ylabel('Frequency')
        ax2.set_title('Target Value Distribution')
        ax2.grid(True, alpha=0.3)
        
        plt.tight_layout()
        self.chart_widget.set_figure(fig)
        
    def create_heatmap(self, sample_size: int):
        """创建热力图"""
        data = self.data_manager.get_data(sample_only=True)
        if len(data) > sample_size:
            data = data.sample(sample_size, random_state=42)
            
        # 创建2D直方图
        fig, ax = plt.subplots(figsize=(10, 8))
        
        # 创建网格
        x_bins = np.linspace(data['original_value'].min(), data['original_value'].max(), 50)
        y_bins = np.linspace(data['target_value'].min(), data['target_value'].max(), 50)
        
        # 创建2D直方图
        hist, xedges, yedges = np.histogram2d(
            data['original_value'], 
            data['target_value'], 
            bins=[x_bins, y_bins]
        )
        
        # 显示热力图
        im = ax.imshow(hist.T, origin='lower', aspect='auto', 
                      extent=[xedges[0], xedges[-1], yedges[0], yedges[-1]],
                      cmap='hot')
        
        ax.set_xlabel('Original Value')
        ax.set_ylabel('Target Value')
        ax.set_title('Mapping Density Heatmap')
        
        # 添加颜色条
        cbar = plt.colorbar(im, ax=ax)
        cbar.set_label('Frequency')
        
        self.chart_widget.set_figure(fig)
        
    def create_comparison_plot(self, sample_size: int):
        """创建对比图"""
        data = self.data_manager.get_data(sample_only=True)
        if len(data) > sample_size:
            data = data.sample(sample_size, random_state=42)
            
        fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(12, 5))
        
        # 变化量分布
        ax1.hist(data['change'], bins=50, alpha=0.7, color='green', edgecolor='black')
        ax1.set_xlabel('Change Amount')
        ax1.set_ylabel('Frequency')
        ax1.set_title('Change Amount Distribution')
        ax1.grid(True, alpha=0.3)
        
        # 变化百分比分布
        ax2.hist(data['change_percent'], bins=50, alpha=0.7, color='orange', edgecolor='black')
        ax2.set_xlabel('Change Percent (%)')
        ax2.set_ylabel('Frequency')
        ax2.set_title('Change Percent Distribution')
        ax2.grid(True, alpha=0.3)
        
        plt.tight_layout()
        self.chart_widget.set_figure(fig)
        
    def save_chart(self):
        """保存图表"""
        if not self.chart_widget.has_figure():
            QMessageBox.warning(self, "警告", "没有可保存的图表")
            return
            
        file_path, _ = QFileDialog.getSaveFileName(
            self, "保存图表", "", 
            "PNG图片 (*.png);;JPG图片 (*.jpg);;PDF文件 (*.pdf);;所有文件 (*.*)"
        )
        
        if file_path:
            try:
                self.chart_widget.save_figure(file_path)
                QMessageBox.information(self, "成功", f"图表已保存到: {file_path}")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"保存失败: {str(e)}")
                
    def clear_chart(self):
        """清除图表"""
        self.chart_widget.clear_figure()
        
    def update_data_info(self):
        """更新数据信息"""
        # 当数据更新时调用
        pass
        
    def update_analysis_results(self, results: Dict[str, Any]):
        """更新分析结果"""
        # 当分析结果更新时调用
        pass
        
    def clear_plots(self):
        """清除所有图表"""
        self.clear_chart()


class ChartWidget(QWidget):
    """图表显示部件"""
    
    def __init__(self):
        super().__init__()
        self.figure = None
        self.canvas = None
        self.setup_ui()
        
    def setup_ui(self):
        """设置用户界面"""
        layout = QVBoxLayout(self)
        
        # 创建画布
        self.figure = Figure(figsize=(10, 8))
        self.canvas = FigureCanvas(self.figure)
        layout.addWidget(self.canvas)
        
    def set_figure(self, figure):
        """设置图表"""
        self.figure = figure
        self.canvas.figure = figure
        self.canvas.draw()
        
    def clear_figure(self):
        """清除图表"""
        if self.figure:
            self.figure.clear()
            self.canvas.draw()
            
    def has_figure(self):
        """是否有图表"""
        return self.figure is not None
        
    def save_figure(self, file_path):
        """保存图表"""
        if self.figure:
            self.figure.savefig(file_path, dpi=300, bbox_inches='tight')