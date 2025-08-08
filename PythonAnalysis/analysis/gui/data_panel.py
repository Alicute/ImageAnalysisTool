"""
数据处理面板模块
"""

import os
import pandas as pd
from PyQt5.QtWidgets import *
from PyQt5.QtCore import *
from PyQt5.QtGui import *
from typing import Optional

from ..core.data_manager import DataManager


class DataPanel(QWidget):
    """数据处理面板"""
    
    # 信号定义
    data_loaded = pyqtSignal(bool, str)
    data_cleared = pyqtSignal()
    
    def __init__(self, data_manager: DataManager):
        super().__init__()
        self.data_manager = data_manager
        self.setup_ui()
        self.setup_connections()
        
    def setup_ui(self):
        """设置用户界面"""
        layout = QVBoxLayout(self)
        
        # 文件选择区域
        file_group = QGroupBox("文件选择")
        file_layout = QHBoxLayout(file_group)
        
        self.file_path_edit = QLineEdit()
        self.file_path_edit.setPlaceholderText("选择数据文件...")
        file_layout.addWidget(self.file_path_edit)
        
        self.browse_button = QPushButton("浏览...")
        file_layout.addWidget(self.browse_button)
        
        self.load_button = QPushButton("加载数据")
        self.load_button.setEnabled(False)
        file_layout.addWidget(self.load_button)
        
        layout.addWidget(file_group)
        
        # 加载选项区域
        options_group = QGroupBox("加载选项")
        options_layout = QGridLayout(options_group)
        
        options_layout.addWidget(QLabel("采样方式:"), 0, 0)
        self.sample_mode_combo = QComboBox()
        self.sample_mode_combo.addItems(["全部加载", "按数量采样", "按比例采样"])
        options_layout.addWidget(self.sample_mode_combo, 0, 1)
        
        options_layout.addWidget(QLabel("采样数量:"), 1, 0)
        self.sample_size_spin = QSpinBox()
        self.sample_size_spin.setRange(1000, 1000000)
        self.sample_size_spin.setValue(10000)
        self.sample_size_spin.setSingleStep(1000)
        options_layout.addWidget(self.sample_size_spin, 1, 1)
        
        options_layout.addWidget(QLabel("采样比例:"), 2, 0)
        self.sample_rate_spin = QDoubleSpinBox()
        self.sample_rate_spin.setRange(0.01, 1.0)
        self.sample_rate_spin.setValue(1.0)
        self.sample_rate_spin.setSingleStep(0.1)
        self.sample_rate_spin.setSuffix("x")
        options_layout.addWidget(self.sample_rate_spin, 2, 1)
        
        layout.addWidget(options_group)
        
        # 数据信息区域
        info_group = QGroupBox("数据信息")
        info_layout = QFormLayout(info_group)
        
        self.file_info_label = QLabel("未选择文件")
        info_layout.addRow("文件信息:", self.file_info_label)
        
        self.data_size_label = QLabel("0")
        info_layout.addRow("数据量:", self.data_size_label)
        
        self.original_range_label = QLabel("-")
        info_layout.addRow("原始值范围:", self.original_range_label)
        
        self.target_range_label = QLabel("-")
        info_layout.addRow("目标值范围:", self.target_range_label)
        
        self.memory_usage_label = QLabel("0 MB")
        info_layout.addRow("内存使用:", self.memory_usage_label)
        
        layout.addWidget(info_group)
        
        # 数据预览区域
        preview_group = QGroupBox("数据预览")
        preview_layout = QVBoxLayout(preview_group)
        
        self.preview_table = QTableWidget()
        self.preview_table.setEditTriggers(QTableWidget.NoEditTriggers)
        self.preview_table.setSelectionBehavior(QTableWidget.SelectRows)
        preview_layout.addWidget(self.preview_table)
        
        layout.addWidget(preview_group)
        
        # 操作按钮
        button_layout = QHBoxLayout()
        
        self.clear_button = QPushButton("清除数据")
        button_layout.addWidget(self.clear_button)
        
        self.export_button = QPushButton("导出数据")
        self.export_button.setEnabled(False)
        button_layout.addWidget(self.export_button)
        
        layout.addLayout(button_layout)
        
        layout.addStretch()
        
    def setup_connections(self):
        """设置信号连接"""
        self.browse_button.clicked.connect(self.browse_file)
        self.load_button.clicked.connect(self.load_data)
        self.clear_button.clicked.connect(self.clear_data)
        self.export_button.clicked.connect(self.export_data)
        
        self.file_path_edit.textChanged.connect(self.on_file_path_changed)
        self.sample_mode_combo.currentTextChanged.connect(self.on_sample_mode_changed)
        
    def browse_file(self):
        """浏览文件"""
        file_path, _ = QFileDialog.getOpenFileName(
            self, "选择数据文件", "", 
            "文本文件 (*.txt);;所有文件 (*.*)"
        )
        
        if file_path:
            self.file_path_edit.setText(file_path)
            
    def on_file_path_changed(self, path: str):
        """文件路径改变"""
        self.load_button.setEnabled(bool(path))
        
    def on_sample_mode_changed(self, mode: str):
        """采样模式改变"""
        if mode == "全部加载":
            self.sample_size_spin.setEnabled(False)
            self.sample_rate_spin.setEnabled(False)
        elif mode == "按数量采样":
            self.sample_size_spin.setEnabled(True)
            self.sample_rate_spin.setEnabled(False)
        elif mode == "按比例采样":
            self.sample_size_spin.setEnabled(False)
            self.sample_rate_spin.setEnabled(True)
            
    def load_data(self):
        """加载数据"""
        file_path = self.file_path_edit.text()
        if not file_path:
            return
            
        # 获取采样参数
        sample_mode = self.sample_mode_combo.currentText()
        sample_size = None
        sample_rate = 1.0
        
        if sample_mode == "按数量采样":
            sample_size = self.sample_size_spin.value()
        elif sample_mode == "按比例采样":
            sample_rate = self.sample_rate_spin.value()
            
        # 禁用按钮，显示加载状态
        self.set_loading_state(True)
        
        # 使用QThread加载数据
        self.load_thread = LoadThread(self.data_manager, file_path, sample_size, sample_rate)
        self.load_thread.finished.connect(self.on_load_finished)
        self.load_thread.error.connect(self.on_load_error)
        self.load_thread.progress.connect(self.on_load_progress)
        self.load_thread.start()
        
    def set_loading_state(self, loading: bool):
        """设置加载状态"""
        self.load_button.setEnabled(not loading)
        self.browse_button.setEnabled(not loading)
        self.sample_mode_combo.setEnabled(not loading)
        
    def on_load_progress(self, message: str):
        """加载进度更新"""
        self.file_info_label.setText(message)
        
    def on_load_finished(self, success: bool, message: str):
        """加载完成"""
        self.set_loading_state(False)
        
        if success:
            # 使用延迟更新，避免线程冲突
            QTimer.singleShot(100, self._delayed_update_ui)
            self.data_loaded.emit(True, message)
        else:
            self.data_loaded.emit(False, message)
    
    def _delayed_update_ui(self):
        """延迟更新UI，避免线程冲突"""
        try:
            self.update_data_info()
            self.update_preview_table()
            self.export_button.setEnabled(True)
        except Exception as e:
            print(f"UI更新失败: {e}")
            # 如果更新失败，至少启用导出按钮
            self.export_button.setEnabled(True)
            
    def on_load_error(self, error_message: str):
        """加载错误"""
        self.set_loading_state(False)
        self.data_loaded.emit(False, error_message)
        
    def update_data_info(self):
        """更新数据信息"""
        stats = self.data_manager.get_statistics()
        file_info = self.data_manager.get_file_info()
        memory_info = self.data_manager.get_memory_usage()
        
        # 文件信息
        if file_info:
            info_text = f"{os.path.basename(file_info.get('file_path', ''))}"
            info_text += f" ({file_info.get('format_type', 'unknown')})"
            self.file_info_label.setText(info_text)
            
        # 数据量
        self.data_size_label.setText(f"{stats.get('total_records', 0):,}")
        
        # 原始值范围
        orig_range = stats.get('original_range', {})
        if orig_range:
            self.original_range_label.setText(
                f"{orig_range.get('min', 0):,} - {orig_range.get('max', 0):,}"
            )
            
        # 目标值范围
        target_range = stats.get('target_range', {})
        if target_range:
            self.target_range_label.setText(
                f"{target_range.get('min', 0):,} - {target_range.get('max', 0):,}"
            )
            
        # 内存使用
        self.memory_usage_label.setText(f"{memory_info.get('rss_mb', 0):.1f} MB")
        
    def update_preview_table(self):
        """更新预览表格"""
        data = self.data_manager.get_data(sample_only=True)
        if data.empty:
            return
            
        # 限制显示行数
        preview_data = data.head(100)
        
        self.preview_table.setRowCount(len(preview_data))
        self.preview_table.setColumnCount(len(preview_data.columns))
        self.preview_table.setHorizontalHeaderLabels(preview_data.columns)
        
        for i, row in preview_data.iterrows():
            for j, value in enumerate(row):
                item = QTableWidgetItem(str(value))
                self.preview_table.setItem(i, j, item)
                
        self.preview_table.resizeColumnsToContents()
        
    def clear_data(self):
        """清除数据"""
        self.data_manager.clear_data()
        self.file_path_edit.clear()
        self.update_data_info()
        self.preview_table.setRowCount(0)
        self.export_button.setEnabled(False)
        self.data_cleared.emit()
        
    def export_data(self):
        """导出数据"""
        data = self.data_manager.get_data()
        if data.empty:
            return
            
        file_path, _ = QFileDialog.getSaveFileName(
            self, "导出数据", "", 
            "CSV文件 (*.csv);;所有文件 (*.*)"
        )
        
        if file_path:
            try:
                data.to_csv(file_path, index=False, encoding='utf-8-sig')
                QMessageBox.information(self, "成功", f"数据已导出到: {file_path}")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"导出失败: {str(e)}")


class LoadThread(QThread):
    """数据加载线程"""
    
    finished = pyqtSignal(bool, str)
    error = pyqtSignal(str)
    progress = pyqtSignal(str)
    
    def __init__(self, data_manager: DataManager, file_path: str, 
                 sample_size: Optional[int] = None, sample_rate: float = 1.0):
        super().__init__()
        self.data_manager = data_manager
        self.file_path = file_path
        self.sample_size = sample_size
        self.sample_rate = sample_rate
        
    def run(self):
        """运行加载线程"""
        try:
            success, message = self.data_manager.load_data(
                self.file_path, self.sample_size, self.sample_rate
            )
            self.finished.emit(success, message)
        except Exception as e:
            self.error.emit(str(e))