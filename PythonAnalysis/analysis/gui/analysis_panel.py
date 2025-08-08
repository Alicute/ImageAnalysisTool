"""
分析功能面板模块
"""

from PyQt5.QtWidgets import *
from PyQt5.QtCore import *
from PyQt5.QtGui import *
from typing import Dict, Any

from ..core.algorithm_engine import AlgorithmEngine


class AnalysisPanel(QWidget):
    """分析功能面板"""
    
    def __init__(self, algorithm_engine: AlgorithmEngine):
        super().__init__()
        self.algorithm_engine = algorithm_engine
        self.setup_ui()
        self.setup_connections()
        
    def setup_ui(self):
        """设置用户界面"""
        layout = QVBoxLayout(self)
        
        # 分析选项区域
        options_group = QGroupBox("分析选项")
        options_layout = QVBoxLayout(options_group)
        
        # 分析类型选择
        analysis_layout = QHBoxLayout()
        analysis_layout.addWidget(QLabel("分析类型:"))
        
        self.analysis_type_combo = QComboBox()
        self.analysis_type_combo.addItems([
            "综合分析", "线性分析", "分段分析", "算法推导"
        ])
        analysis_layout.addWidget(self.analysis_type_combo)
        analysis_layout.addStretch()
        
        options_layout.addLayout(analysis_layout)
        
        # 分析按钮
        self.analyze_button = QPushButton("开始分析")
        options_layout.addWidget(self.analyze_button)
        
        layout.addWidget(options_group)
        
        # 分析结果区域
        results_group = QGroupBox("分析结果")
        results_layout = QVBoxLayout(results_group)
        
        # 结果显示
        self.results_text = QTextEdit()
        self.results_text.setReadOnly(True)
        self.results_text.setFont(QFont("Consolas", 10))
        results_layout.addWidget(self.results_text)
        
        layout.addWidget(results_group)
        
        # 算法信息区域
        algorithm_group = QGroupBox("算法信息")
        algorithm_layout = QFormLayout(algorithm_group)
        
        self.algorithm_type_label = QLabel("未知")
        algorithm_layout.addRow("算法类型:", self.algorithm_type_label)
        
        self.algorithm_desc_label = QLabel("-")
        algorithm_layout.addRow("算法描述:", self.algorithm_desc_label)
        
        self.confidence_label = QLabel("0.0")
        algorithm_layout.addRow("置信度:", self.confidence_label)
        
        self.formula_label = QLabel("-")
        algorithm_layout.addRow("算法公式:", self.formula_label)
        
        layout.addWidget(algorithm_group)
        
        # 操作按钮
        button_layout = QHBoxLayout()
        
        self.copy_button = QPushButton("复制结果")
        button_layout.addWidget(self.copy_button)
        
        self.save_button = QPushButton("保存结果")
        button_layout.addWidget(self.save_button)
        
        self.clear_button = QPushButton("清除结果")
        button_layout.addWidget(self.clear_button)
        
        layout.addLayout(button_layout)
        
        layout.addStretch()
        
    def setup_connections(self):
        """设置信号连接"""
        self.analyze_button.clicked.connect(self.run_analysis)
        self.copy_button.clicked.connect(self.copy_results)
        self.save_button.clicked.connect(self.save_results)
        self.clear_button.clicked.connect(self.clear_results)
        
        # 连接算法引擎信号
        self.algorithm_engine.analysis_completed.connect(self.on_analysis_completed)
        self.algorithm_engine.analysis_error.connect(self.on_analysis_error)
        self.algorithm_engine.analysis_progress.connect(self.on_analysis_progress)
        
    def run_analysis(self, analysis_type: str = None):
        """运行分析"""
        if analysis_type is None:
            analysis_type = self.analysis_type_combo.currentText()
            
        # 映射分析类型到引擎方法
        type_mapping = {
            "综合分析": "comprehensive",
            "线性分析": "linear",
            "分段分析": "piecewise",
            "算法推导": "derive"
        }
        
        engine_type = type_mapping.get(analysis_type, "comprehensive")
        
        # 显示加载状态
        self.analyze_button.setEnabled(False)
        self.results_text.setText("正在分析中...")
        
        # 使用QThread进行分析
        self.analysis_thread = AnalysisThread(self.algorithm_engine, engine_type)
        self.analysis_thread.finished.connect(self.on_analysis_completed)
        self.analysis_thread.error.connect(self.on_analysis_error)
        self.analysis_thread.start()
        
    def on_analysis_completed(self, results: Dict[str, Any]):
        """分析完成"""
        self.analyze_button.setEnabled(True)
        self.display_results(results)
        
    def on_analysis_error(self, error_message: str):
        """分析错误"""
        self.analyze_button.setEnabled(True)
        self.results_text.setText(f"分析失败: {error_message}")
        QMessageBox.critical(self, "错误", f"分析失败: {error_message}")
        
    def on_analysis_progress(self, message: str):
        """分析进度"""
        self.results_text.setText(message)
        
    def display_results(self, results: Dict[str, Any]):
        """显示分析结果"""
        if 'error' in results:
            self.results_text.setText(f"分析失败: {results['error']}")
            return
            
        # 生成结果文本
        result_text = self.generate_result_text(results)
        self.results_text.setText(result_text)
        
        # 更新算法信息
        if 'summary' in results:
            summary = results['summary']
            self.algorithm_type_label.setText(summary.get('algorithm_type', '未知'))
            self.algorithm_desc_label.setText(summary.get('algorithm_description', '-'))
            self.confidence_label.setText(f"{summary.get('confidence', 0.0):.3f}")
            
        # 更新公式
        if 'linear_analysis' in results:
            linear = results['linear_analysis']
            if 'formula' in linear:
                self.formula_label.setText(linear['formula'])
        elif 'piecewise_analysis' in results:
            piecewise = results['piecewise_analysis']
            self.formula_label.setText("分段线性算法")
            
    def generate_result_text(self, results: Dict[str, Any]) -> str:
        """生成结果文本"""
        text = "=== 分析结果 ===\n\n"
        
        # 基础统计
        if 'basic_statistics' in results:
            stats = results['basic_statistics']
            text += "## 基础统计\n"
            text += f"总记录数: {stats.get('total_records', 0):,}\n"
            
            if 'original_range' in stats:
                orig = stats['original_range']
                text += f"原始值范围: {orig.get('min', 0):,} - {orig.get('max', 0):,}\n"
                text += f"原始值均值: {orig.get('mean', 0):.1f}\n"
                text += f"原始值标准差: {orig.get('std', 0):.1f}\n"
                
            if 'target_range' in stats:
                target = stats['target_range']
                text += f"目标值范围: {target.get('min', 0):,} - {target.get('max', 0):,}\n"
                text += f"目标值均值: {target.get('mean', 0):.1f}\n"
                text += f"目标值标准差: {target.get('std', 0):.1f}\n"
                
            text += "\n"
            
        # 映射模式
        if 'mapping_patterns' in results:
            patterns = results['mapping_patterns']
            text += "## 映射模式\n"
            text += f"全局统一算法: {'是' if patterns.get('is_global_algorithm', False) else '否'}\n"
            text += f"唯一变化值数量: {patterns.get('unique_change_count', 0)}\n"
            text += f"相关性: {patterns.get('correlation', 0):.3f}\n"
            
            if 'linear_fit' in patterns:
                linear = patterns['linear_fit']
                text += f"线性拟合R²: {linear.get('r_squared', 0):.3f}\n"
                
            text += "\n"
            
        # 线性分析
        if 'linear_analysis' in results:
            linear = results['linear_analysis']
            text += "## 线性分析\n"
            if 'error' not in linear:
                text += f"是否线性: {'是' if linear.get('is_linear', False) else '否'}\n"
                text += f"斜率: {linear.get('slope', 0):.3f}\n"
                text += f"截距: {linear.get('intercept', 0):.1f}\n"
                text += f"R²: {linear.get('r_squared', 0):.3f}\n"
                text += f"公式: {linear.get('formula', '')}\n"
            else:
                text += f"线性分析失败: {linear['error']}\n"
                
            text += "\n"
            
        # 分段分析
        if 'piecewise_analysis' in results:
            piecewise = results['piecewise_analysis']
            text += "## 分段分析\n"
            if 'error' not in piecewise:
                text += f"是否分段: {'是' if piecewise.get('is_piecewise', False) else '否'}\n"
                text += f"分段数量: {piecewise.get('total_segments', 0)}\n"
                
                segments = piecewise.get('segments', [])
                for i, segment in enumerate(segments):
                    text += f"段 {i+1}: {segment.get('start_value', 0):,} - {segment.get('end_value', 0):,}\n"
                    if 'formula' in segment:
                        text += f"  公式: {segment['formula']}\n"
            else:
                text += f"分段分析失败: {piecewise['error']}\n"
                
            text += "\n"
            
        text += f"分析时间: {results.get('timestamp', '')}\n"
        
        return text
        
    def copy_results(self):
        """复制结果"""
        text = self.results_text.toPlainText()
        if text:
            clipboard = QApplication.clipboard()
            clipboard.setText(text)
            QMessageBox.information(self, "成功", "结果已复制到剪贴板")
            
    def save_results(self):
        """保存结果"""
        text = self.results_text.toPlainText()
        if not text:
            return
            
        file_path, _ = QFileDialog.getSaveFileName(
            self, "保存分析结果", "", 
            "文本文件 (*.txt);;Markdown文件 (*.md);;所有文件 (*.*)"
        )
        
        if file_path:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(text)
                QMessageBox.information(self, "成功", f"结果已保存到: {file_path}")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"保存失败: {str(e)}")
                
    def clear_results(self):
        """清除结果"""
        self.results_text.clear()
        self.algorithm_type_label.setText("未知")
        self.algorithm_desc_label.setText("-")
        self.confidence_label.setText("0.0")
        self.formula_label.setText("-")
        
    def update_data_info(self):
        """更新数据信息"""
        # 当数据更新时调用
        pass
        
    def run_comprehensive_analysis(self):
        """运行综合分析"""
        self.run_analysis("comprehensive")


class AnalysisThread(QThread):
    """分析线程"""
    
    finished = pyqtSignal(dict)
    error = pyqtSignal(str)
    
    def __init__(self, algorithm_engine: AlgorithmEngine, analysis_type: str):
        super().__init__()
        self.algorithm_engine = algorithm_engine
        self.analysis_type = analysis_type
        
    def run(self):
        """运行分析线程"""
        try:
            if self.analysis_type == "derive":
                results = self.algorithm_engine.derive_algorithm()
            else:
                results = self.algorithm_engine.analyze_mapping(self.analysis_type)
            self.finished.emit(results)
        except Exception as e:
            self.error.emit(str(e))