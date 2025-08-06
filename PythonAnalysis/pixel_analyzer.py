import sys
import os
import re
import pandas as pd
import numpy as np
from PyQt5.QtWidgets import *
from PyQt5.QtCore import *
from PyQt5.QtGui import *
import matplotlib.pyplot as plt
from matplotlib.backends.backend_qt5agg import FigureCanvasQTAgg as FigureCanvas
import seaborn as sns

class PixelDataAnalyzer:
    """像素数据分析器"""
    
    def __init__(self):
        self.data = None
        self.mapping_stats = None
        self.exact_mapping = None
        
    def load_data(self, file_path):
        """加载像素数据文件"""
        try:
            # 解析文件格式
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # 提取基本信息
            info_lines = content.split('===详细像素映射信息===')[0].split('\n')
            total_pixels = 0
            for line in info_lines:
                if '总像素数:' in line:
                    total_pixels = int(line.split('总像素数:')[1].split(',')[0])
            
            # 解析像素数据
            pixel_pattern = r'\[(\d+)\]位置\((\d+),(\d+)\)原值(\d+)→新值(\d+)\(变化:([-\d]+),([-\d.]+)%\)'
            matches = re.findall(pixel_pattern, content)
            
            if not matches:
                raise ValueError("无法解析像素数据格式")
            
            # 创建DataFrame
            data = []
            for match in matches:
                idx, x, y, orig, new, change, percent = match
                data.append({
                    'Index': int(idx),
                    'X': int(x),
                    'Y': int(y),
                    'OriginalValue': int(orig),
                    'TargetValue': int(new),
                    'Change': int(change),
                    'ChangePercent': float(percent)
                })
            
            self.data = pd.DataFrame(data)
            print(f"成功加载数据: {len(self.data)} 像素")
            return True
            
        except Exception as e:
            print(f"加载数据失败: {e}")
            return False
    
    def analyze_mapping_patterns(self):
        """分析映射模式"""
        if self.data is None:
            return None
            
        print("=== 分析映射模式 ===")
        
        # 1. 基本统计
        stats = {
            '总像素数': len(self.data),
            '原始值范围': f"{self.data['OriginalValue'].min()} - {self.data['OriginalValue'].max()}",
            '目标值范围': f"{self.data['TargetValue'].min()} - {self.data['TargetValue'].max()}",
            '平均变化': f"{self.data['Change'].mean():.2f}",
            '变化标准差': f"{self.data['Change'].std():.2f}"
        }
        
        print("基本统计:")
        for key, value in stats.items():
            print(f"  {key}: {value}")
        
        # 2. 精确映射关系
        print("\n=== 精确映射关系分析 ===")
        mapping_groups = self.data.groupby('OriginalValue')['TargetValue'].agg(['unique', 'count'])
        
        # 一对一映射
        one_to_one = mapping_groups[mapping_groups['unique'].apply(len) == 1]
        print(f"一对一映射关系: {len(one_to_one)} 种")
        
        # 一对多映射
        one_to_many = mapping_groups[mapping_groups['unique'].apply(len) > 1]
        print(f"一对多映射关系: {len(one_to_many)} 种")
        
        # 3. 检查是否为全局统一算法
        unique_changes = self.data['Change'].unique()
        print(f"\n=== 算法模式分析 ===")
        print(f"不同的变化值数量: {len(unique_changes)}")
        
        if len(unique_changes) == 1:
            print("✓ 检测到全局统一算法: 所有像素变化相同")
            print(f"  算法: 目标值 = 原始值 {unique_changes[0]:+d}")
        else:
            print("✗ 检测到非统一算法，可能存在多种映射规则")
            
            # 检查是否为分段线性
            change_by_value = self.data.groupby('OriginalValue')['Change'].mean()
            if len(change_by_value) > 1:
                correlation = change_by_value.corr(pd.Series(change_by_value.index))
                print(f"  原始值与变化量的相关性: {correlation:.3f}")
                
                if abs(correlation) > 0.8:
                    print("✓ 可能是线性映射算法")
                elif abs(correlation) > 0.5:
                    print("? 可能是非线性映射算法")
                else:
                    print("? 可能是复杂映射算法")
        
        # 4. 空间分布分析
        print("\n=== 空间分布分析 ===")
        
        # 检查不同区域的变化差异
        self.data['Region'] = pd.cut(self.data['X'], bins=4, labels=['左', '中左', '中右', '右'])
        region_stats = self.data.groupby('Region')['Change'].agg(['mean', 'std'])
        print("按X轴分区的变化统计:")
        print(region_stats)
        
        # 检查是否有位置相关的算法
        if region_stats['std'].max() > 1:
            print("✗ 检测到位置相关的算法（不同区域处理不同）")
        else:
            print("✓ 未检测到位置相关性，可能是全局算法")
        
        return {
            'stats': stats,
            'one_to_one_mapping': one_to_one,
            'one_to_many_mapping': one_to_many,
            'unique_changes': unique_changes,
            'region_stats': region_stats
        }
    
    def generate_ai_analysis_report(self):
        """生成AI分析报告"""
        if self.data is None:
            return "请先加载数据"
            
        analysis_results = self.analyze_mapping_patterns()
        if analysis_results is None:
            return "分析失败"
        
        report = "# 像素映射算法分析报告\n\n"
        
        # 1. 数据概况
        report += "## 1. 数据概况\n"
        report += f"- 总像素数: {analysis_results['stats']['总像素数']:,}\n"
        report += f"- 原始值范围: {analysis_results['stats']['原始值范围']}\n"
        report += f"- 目标值范围: {analysis_results['stats']['目标值范围']}\n"
        report += f"- 平均变化: {analysis_results['stats']['平均变化']}\n"
        report += f"- 变化标准差: {analysis_results['stats']['变化标准差']}\n\n"
        
        # 2. 映射关系
        report += "## 2. 映射关系分析\n"
        report += f"- 一对一映射: {len(analysis_results['one_to_one_mapping'])} 种\n"
        report += f"- 一对多映射: {len(analysis_results['one_to_many_mapping'])} 种\n\n"
        
        # 3. 算法推断
        report += "## 3. 算法推断\n"
        
        if len(analysis_results['unique_changes']) == 1:
            change = analysis_results['unique_changes'][0]
            report += f"### 检测到全局统一算法\n"
            report += f"```\n"
            report += f"目标值 = 原始值 {change:+d}\n"
            report += f"```\n\n"
            report += f"**算法说明**: 所有像素都应用了相同的偏移量 {change:+d}\n"
        else:
            report += "### 检测到复杂算法\n"
            
            # 分析变化模式
            change_by_value = self.data.groupby('OriginalValue')['Change'].mean()
            correlation = change_by_value.corr(pd.Series(change_by_value.index))
            
            if abs(correlation) > 0.8:
                report += f"- 线性相关系数: {correlation:.3f} (强线性关系)\n"
                
                # 尝试线性拟合
                from scipy import stats
                slope, intercept, r_value, p_value, std_err = stats.linregress(
                    change_by_value.index, change_by_value.values)
                
                report += f"- 线性拟合: 目标值 = {slope:.3f} × 原始值 + {intercept:.1f}\n"
                report += f"- 拟合优度 R² = {r_value**2:.3f}\n"
                
            else:
                report += f"- 相关性: {correlation:.3f} (非线性关系)\n"
                report += "- 建议进一步分析可能的分段函数或查找表映射\n"
        
        # 4. 空间分析
        report += "\n## 4. 空间分布分析\n"
        if analysis_results['region_stats']['std'].max() > 1:
            report += "- 检测到位置相关的算法，不同区域处理方式不同\n"
            report += "- 可能是基于局部特征的算法\n"
        else:
            report += "- 未检测到位置相关性，算法是全局统一的\n"
        
        # 5. AI逆向工程建议
        report += "\n## 5. AI逆向工程建议\n"
        
        if len(analysis_results['unique_changes']) == 1:
            report += "**已确定算法**: 这是一个简单的全局偏移算法\n"
            report += "**验证方法**: 随机抽取几个像素验证算法正确性\n"
        else:
            report += "**需要进一步分析**:\n"
            report += "1. 提取完整的映射关系表\n"
            report += "2. 分析可能的数学函数关系\n"
            report += "3. 检查是否为标准图像处理算法（如gamma校正、对比度调整等）\n"
            report += "4. 如果映射关系复杂，可能需要机器学习方法来拟合\n"
        
        return report
    
    def create_visualization(self):
        """创建数据可视化"""
        if self.data is None:
            return None
            
        # 创建图表
        fig, ((ax1, ax2), (ax3, ax4)) = plt.subplots(2, 2, figsize=(15, 12))
        
        # 1. 原始值 vs 目标值散点图
        sample_size = min(10000, len(self.data))
        sample_data = self.data.sample(sample_size)
        ax1.scatter(sample_data['OriginalValue'], sample_data['TargetValue'], 
                   alpha=0.1, s=1)
        ax1.set_xlabel('原始值')
        ax1.set_ylabel('目标值')
        ax1.set_title('像素值映射关系')
        ax1.grid(True, alpha=0.3)
        
        # 2. 原始值分布直方图
        ax2.hist(self.data['OriginalValue'], bins=50, alpha=0.7, label='原始值')
        ax2.hist(self.data['TargetValue'], bins=50, alpha=0.7, label='目标值')
        ax2.set_xlabel('像素值')
        ax2.set_ylabel('频次')
        ax2.set_title('像素值分布')
        ax2.legend()
        ax2.grid(True, alpha=0.3)
        
        # 3. 变化量分布
        ax3.hist(self.data['Change'], bins=50, alpha=0.7, color='green')
        ax3.set_xlabel('变化量')
        ax3.set_ylabel('频次')
        ax3.set_title('变化量分布')
        ax3.grid(True, alpha=0.3)
        
        # 4. 空间分布热力图
        # 创建一个小的汇总图像来显示空间变化模式
        resolution = 50
        x_bins = pd.cut(self.data['X'], bins=resolution, labels=False)
        y_bins = pd.cut(self.data['Y'], bins=resolution, labels=False)
        spatial_data = self.data.groupby([x_bins, y_bins])['Change'].mean().unstack()
        
        im = ax4.imshow(spatial_data, cmap='RdYlBu', aspect='auto')
        ax4.set_title('空间变化模式')
        ax4.set_xlabel('X位置')
        ax4.set_ylabel('Y位置')
        plt.colorbar(im, ax=ax4)
        
        plt.tight_layout()
        return fig

class MainWindow(QMainWindow):
    """主窗口"""
    
    def __init__(self):
        super().__init__()
        self.analyzer = PixelDataAnalyzer()
        self.init_ui()
        
    def init_ui(self):
        self.setWindowTitle('像素映射分析器 - AI逆向工程工具')
        self.setGeometry(100, 100, 1200, 800)
        
        # 创建中心部件
        central_widget = QWidget()
        self.setCentralWidget(central_widget)
        
        # 创建布局
        layout = QVBoxLayout(central_widget)
        
        # 工具栏
        toolbar = QHBoxLayout()
        
        self.load_btn = QPushButton('加载数据文件')
        self.load_btn.clicked.connect(self.load_file)
        toolbar.addWidget(self.load_btn)
        
        self.analyze_btn = QPushButton('分析映射模式')
        self.analyze_btn.clicked.connect(self.analyze_data)
        self.analyze_btn.setEnabled(False)
        toolbar.addWidget(self.analyze_btn)
        
        self.visualize_btn = QPushButton('生成可视化')
        self.visualize_btn.clicked.connect(self.create_visualization)
        self.visualize_btn.setEnabled(False)
        toolbar.addWidget(self.visualize_btn)
        
        self.export_btn = QPushButton('导出分析报告')
        self.export_btn.clicked.connect(self.export_report)
        self.export_btn.setEnabled(False)
        toolbar.addWidget(self.export_btn)
        
        toolbar.addStretch()
        layout.addLayout(toolbar)
        
        # 创建标签页
        self.tab_widget = QTabWidget()
        layout.addWidget(self.tab_widget)
        
        # 数据预览标签页
        self.data_preview = QTextEdit()
        self.data_preview.setReadOnly(True)
        self.tab_widget.addTab(self.data_preview, '数据预览')
        
        # 分析结果标签页
        self.analysis_result = QTextEdit()
        self.analysis_result.setReadOnly(True)
        self.tab_widget.addTab(self.analysis_result, '分析结果')
        
        # 可视化标签页
        self.viz_widget = QWidget()
        self.viz_layout = QVBoxLayout(self.viz_widget)
        self.viz_label = QLabel('点击"生成可视化"按钮创建图表')
        self.viz_label.setAlignment(Qt.AlignCenter)
        self.viz_layout.addWidget(self.viz_label)
        self.tab_widget.addTab(self.viz_widget, '数据可视化')
        
        # 状态栏
        self.status_bar = self.statusBar()
        self.status_bar.showMessage('准备就绪')
        
    def load_file(self):
        """加载数据文件"""
        file_path, _ = QFileDialog.getOpenFileName(
            self, '选择像素数据文件', '', '文本文件 (*.txt);;所有文件 (*.*)')
        
        if file_path:
            self.status_bar.showMessage('正在加载数据...')
            
            if self.analyzer.load_data(file_path):
                self.data_preview.setText(f"成功加载数据文件:\n{file_path}\n\n")
                self.data_preview.append(f"数据预览 (前10行):\n")
                
                # 显示数据预览
                if self.analyzer.data is not None:
                    preview_data = self.analyzer.data.head(10).to_string()
                    self.data_preview.append(preview_data)
                
                self.analyze_btn.setEnabled(True)
                self.status_bar.showMessage('数据加载完成')
            else:
                QMessageBox.warning(self, '错误', '数据加载失败')
                self.status_bar.showMessage('数据加载失败')
    
    def analyze_data(self):
        """分析数据"""
        if self.analyzer.data is None:
            return
            
        self.status_bar.showMessage('正在分析数据...')
        
        try:
            report = self.analyzer.generate_ai_analysis_report()
            self.analysis_result.setText(report)
            
            self.visualize_btn.setEnabled(True)
            self.export_btn.setEnabled(True)
            self.status_bar.showMessage('分析完成')
            
        except Exception as e:
            QMessageBox.critical(self, '分析错误', f'分析过程中发生错误: {str(e)}')
            self.status_bar.showMessage('分析失败')
    
    def create_visualization(self):
        """创建可视化"""
        self.status_bar.showMessage('正在生成可视化...')
        
        try:
            fig = self.analyzer.create_visualization()
            if fig:
                # 清除之前的可视化
                for i in reversed(range(self.viz_layout.count())): 
                    self.viz_layout.itemAt(i).widget().setParent(None)
                
                # 创建matplotlib画布
                canvas = FigureCanvas(fig)
                self.viz_layout.addWidget(canvas)
                
                self.status_bar.showMessage('可视化生成完成')
            else:
                QMessageBox.warning(self, '错误', '可视化生成失败')
                
        except Exception as e:
            QMessageBox.critical(self, '可视化错误', f'生成可视化时发生错误: {str(e)}')
            self.status_bar.showMessage('可视化生成失败')
    
    def export_report(self):
        """导出分析报告"""
        if self.analyzer.data is None:
            return
            
        file_path, _ = QFileDialog.getSaveFileName(
            self, '保存分析报告', '', 'Markdown文件 (*.md);;文本文件 (*.txt)')
        
        if file_path:
            try:
                report = self.analyzer.generate_ai_analysis_report()
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(report)
                
                QMessageBox.information(self, '成功', f'分析报告已保存到:\n{file_path}')
                self.status_bar.showMessage('报告导出完成')
                
            except Exception as e:
                QMessageBox.critical(self, '导出错误', f'导出报告时发生错误: {str(e)}')
                self.status_bar.showMessage('报告导出失败')

def main():
    app = QApplication(sys.argv)
    app.setStyle('Fusion')
    
    # 设置应用程序信息
    app.setApplicationName('像素映射分析器')
    app.setApplicationVersion('1.0')
    
    window = MainWindow()
    window.show()
    
    sys.exit(app.exec_())

if __name__ == '__main__':
    main()