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

class LoadThread(QThread):
    """数据加载线程"""
    finished = pyqtSignal(object)
    error = pyqtSignal(str)
    progress = pyqtSignal(str)
    
    def __init__(self, file_path, sample_size=None):
        super().__init__()
        self.file_path = file_path
        self.sample_size = sample_size
    
    def run(self):
        try:
            analyzer = PixelDataAnalyzer()
            
            # 重写load_data方法以支持进度信号
            self.progress.emit(f"开始加载 {self.file_path}")
            
            import psutil
            import gc
            
            self.progress.emit(f"初始内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            
            # 流式读取文件
            data = []
            total_lines = 0
            
            # 如果指定了采样大小，计算采样间隔
            sample_interval = 1
            if self.sample_size:
                # 先快速扫描文件总行数
                with open(self.file_path, 'r', encoding='utf-8') as f:
                    for _ in f:
                        total_lines += 1
                
                # 估算匹配行数
                estimated_matches = int(total_lines * 0.8)
                if estimated_matches > self.sample_size:
                    sample_interval = max(1, estimated_matches // self.sample_size)
                    self.progress.emit(f"将采样数据，每 {sample_interval} 行取1行")
            
            # 正式读取数据
            with open(self.file_path, 'r', encoding='utf-8') as f:
                line_count = 0
                for line in f:
                    line_count += 1
                    
                    # 采样逻辑
                    if sample_interval > 1 and (line_count % sample_interval != 0):
                        continue
                    
                    # 解析像素数据行
                    pixel_pattern = r'\[(\d+)\] 位置\((\d+),(\d+)\) 原值(\d+) → 新值(\d+) \(变化: ([\-\d]+), ([\-\d.]+)%\)'
                    match = re.search(pixel_pattern, line.strip())
                    
                    if match:
                        idx, x, y, orig, new, change, percent = match.groups()
                        data.append({
                            'Index': int(idx),
                            'X': int(x),
                            'Y': int(y),
                            'OriginalValue': int(orig),
                            'TargetValue': int(new),
                            'Change': int(change),
                            'ChangePercent': float(percent)
                        })
                        
                        # 定期发送进度
                        if len(data) % 100000 == 0:
                            self.progress.emit(f"已处理 {len(data)} 条记录...")
                            gc.collect()
                            self.progress.emit(f"内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            
            if not data:
                raise ValueError("无法解析像素数据格式")
            
            # 创建DataFrame
            self.progress.emit("创建DataFrame...")
            df = pd.DataFrame(data)
            
            # 优化数据类型
            df['Index'] = pd.to_numeric(df['Index'], downcast='unsigned')
            df['X'] = pd.to_numeric(df['X'], downcast='unsigned')
            df['Y'] = pd.to_numeric(df['Y'], downcast='unsigned')
            df['OriginalValue'] = pd.to_numeric(df['OriginalValue'], downcast='unsigned')
            df['TargetValue'] = pd.to_numeric(df['TargetValue'], downcast='unsigned')
            df['Change'] = pd.to_numeric(df['Change'], downcast='integer')
            df['ChangePercent'] = pd.to_numeric(df['ChangePercent'], downcast='float')
            
            # 清理内存
            del data
            gc.collect()
            
            self.progress.emit(f"加载完成: {len(df)} 条记录")
            self.progress.emit(f"最终内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            
            self.finished.emit(df)
            
        except Exception as e:
            self.error.emit(str(e))

class PixelDataAnalyzer:
    """像素数据分析器"""
    
    def __init__(self):
        self.data = None
        self.mapping_stats = None
        self.exact_mapping = None
        
    def load_data(self, file_path, sample_size=None):
        """加载像素数据文件，支持采样以节省内存"""
        try:
            import psutil
            import gc
            
            print(f"开始加载数据文件: {file_path}")
            print(f"初始内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            
            # 流式读取文件，避免一次性加载到内存
            data = []
            total_lines = 0
            matched_lines = 0
            
            # 如果指定了采样大小，计算采样间隔
            sample_interval = 1
            if sample_size:
                # 先快速扫描文件总行数
                with open(file_path, 'r', encoding='utf-8') as f:
                    for _ in f:
                        total_lines += 1
                
                # 估算匹配行数（假设约80%的行包含像素数据）
                estimated_matches = int(total_lines * 0.8)
                if estimated_matches > sample_size:
                    sample_interval = max(1, estimated_matches // sample_size)
                    print(f"将采样数据，每 {sample_interval} 行取1行，目标样本数: {sample_size}")
            
            # 正式读取数据
            with open(file_path, 'r', encoding='utf-8') as f:
                line_count = 0
                for line in f:
                    line_count += 1
                    
                    # 采样逻辑
                    if sample_interval > 1 and (line_count % sample_interval != 0):
                        continue
                    
                    # 解析像素数据行
                    pixel_pattern = r'\[(\d+)\] 位置\((\d+),(\d+)\) 原值(\d+) → 新值(\d+) \(变化: ([\-\d]+), ([\-\d.]+)%\)'
                    match = re.search(pixel_pattern, line.strip())
                    
                    if match:
                        matched_lines += 1
                        idx, x, y, orig, new, change, percent = match.groups()
                        data.append({
                            'Index': int(idx),
                            'X': int(x),
                            'Y': int(y),
                            'OriginalValue': int(orig),
                            'TargetValue': int(new),
                            'Change': int(change),
                            'ChangePercent': float(percent)
                        })
                        
                        # 定期清理内存和显示进度
                        if len(data) % 100000 == 0:
                            print(f"已处理 {len(data)} 条记录...")
                            gc.collect()
                            print(f"当前内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            
            if not data:
                raise ValueError("无法解析像素数据格式")
            
            # 优化DataFrame内存使用
            print("创建DataFrame...")
            self.data = pd.DataFrame(data)
            
            # 优化数据类型以减少内存使用
            self.data['Index'] = pd.to_numeric(self.data['Index'], downcast='unsigned')
            self.data['X'] = pd.to_numeric(self.data['X'], downcast='unsigned')
            self.data['Y'] = pd.to_numeric(self.data['Y'], downcast='unsigned')
            self.data['OriginalValue'] = pd.to_numeric(self.data['OriginalValue'], downcast='unsigned')
            self.data['TargetValue'] = pd.to_numeric(self.data['TargetValue'], downcast='unsigned')
            self.data['Change'] = pd.to_numeric(self.data['Change'], downcast='integer')
            self.data['ChangePercent'] = pd.to_numeric(self.data['ChangePercent'], downcast='float')
            
            # 清理临时数据
            del data
            gc.collect()
            
            print(f"成功加载数据: {len(self.data)} 像素")
            print(f"最终内存使用: {psutil.Process().memory_info().rss / 1024 / 1024:.1f} MB")
            print(f"DataFrame内存占用: {self.data.memory_usage(deep=True).sum() / 1024 / 1024:.1f} MB")
            
            return True
            
        except Exception as e:
            print(f"加载数据失败: {e}")
            import traceback
            traceback.print_exc()
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
        region_stats = self.data.groupby('Region', observed=False)['Change'].agg(['mean', 'std'])
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
        
        # 1. 原始值 vs 目标值散点图 - 使用颜色密度显示
        sample_size = min(20000, len(self.data))
        sample_data = self.data.sample(sample_size)
        
        # 使用颜色密度显示数据点分布
        scatter = ax1.scatter(sample_data['OriginalValue'], sample_data['TargetValue'], 
                            c=sample_data['Change'], cmap='RdYlBu_r', alpha=0.6, s=2)
        ax1.set_xlabel('Original Value')
        ax1.set_ylabel('Target Value')
        ax1.set_title('Pixel Value Mapping (Color = Change Amount)')
        ax1.grid(True, alpha=0.3)
        
        # 添加颜色条
        cbar1 = plt.colorbar(scatter, ax=ax1, shrink=0.8)
        cbar1.set_label('Change Amount', rotation=270, labelpad=15)
        
        # 添加对角线参考线
        min_val = min(sample_data['OriginalValue'].min(), sample_data['TargetValue'].min())
        max_val = max(sample_data['OriginalValue'].max(), sample_data['TargetValue'].max())
        ax1.plot([min_val, max_val], [min_val, max_val], 'k--', alpha=0.5, label='No Change')
        
        # 将图例放在右上角，避免重叠
        ax1.legend(loc='upper right', fontsize=8)
        
        # 2. 原始值分布直方图
        ax2.hist(self.data['OriginalValue'], bins=50, alpha=0.7, label='Original Value')
        ax2.hist(self.data['TargetValue'], bins=50, alpha=0.7, label='Target Value')
        ax2.set_xlabel('Pixel Value')
        ax2.set_ylabel('Frequency')
        ax2.set_title('Pixel Value Distribution')
        ax2.legend()
        ax2.grid(True, alpha=0.3)
        
        # 3. 变化量分布 - 突出主要变化区间
        # 计算统计信息用于标注
        change_mean = self.data['Change'].mean()
        change_std = self.data['Change'].std()
        change_median = self.data['Change'].median()
        
        # 使用更多bins来显示细节
        ax3.hist(self.data['Change'], bins=100, alpha=0.7, color='steelblue', edgecolor='black', linewidth=0.5)
        
        # 添加统计信息垂直线
        ax3.axvline(change_mean, color='red', linestyle='--', linewidth=2, label=f'Mean: {change_mean:.0f}')
        ax3.axvline(change_median, color='orange', linestyle='--', linewidth=2, label=f'Median: {change_median:.0f}')
        
        # 突出显示主要区间 (-18000 to -10000)
        ax3.axvspan(-18000, -10000, alpha=0.2, color='yellow', label='Main Range (-18k to -10k)')
        
        ax3.set_xlabel('Change Amount')
        ax3.set_ylabel('Frequency')
        ax3.set_title('Change Distribution (Most pixels reduced by 10k-18k)')
        ax3.grid(True, alpha=0.3)
        ax3.legend()
        
        # 添加统计文本
        stats_text = f'Mean: {change_mean:.0f}\nStd: {change_std:.0f}\nMedian: {change_median:.0f}'
        ax3.text(0.02, 0.98, stats_text, transform=ax3.transAxes, verticalalignment='top',
                bbox=dict(boxstyle='round', facecolor='white', alpha=0.8))
        
        # 4. 空间分布热力图 - 修复颜色显示
        resolution = 100
        x_bins = pd.cut(self.data['X'], bins=resolution, labels=False)
        y_bins = pd.cut(self.data['Y'], bins=resolution, labels=False)
        spatial_data = self.data.groupby([x_bins, y_bins])['Change'].mean().unstack()
        
        # 不填充缺失数据，让它们显示为白色，保持颜色对比度
        # 使用更强的颜色对比度
        im = ax4.imshow(spatial_data, cmap='RdYlBu_r', aspect='auto',
                       vmin=self.data['Change'].quantile(0.05),  # 使用5%分位数避免极值影响
                       vmax=self.data['Change'].quantile(0.95))  # 使用95%分位数避免极值影响
        ax4.set_title('Spatial Change Pattern')
        ax4.set_xlabel('X Position')
        ax4.set_ylabel('Y Position')
        
        # 添加颜色条
        cbar4 = plt.colorbar(im, ax=ax4, shrink=0.8)
        cbar4.set_label('Change Amount', rotation=270, labelpad=15)
        
        # 添加网格线以便更好地定位
        ax4.grid(True, alpha=0.3)
        
        plt.tight_layout()
        return fig

class MainWindow(QMainWindow):
    """主窗口"""
    
    def __init__(self):
        super().__init__()
        self.analyzer = PixelDataAnalyzer()
        self.current_page = 0
        self.page_size = 50
        self.total_pages = 0
        self.init_ui()
        
    def init_ui(self):
        self.setWindowTitle('Pixel Mapping Analyzer - AI Reverse Engineering Tool')
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
        
        # 采样选项
        toolbar.addWidget(QLabel('采样模式:'))
        self.sample_combo = QComboBox()
        self.sample_combo.addItems(['全量数据', '采样10万条', '采样50万条', '采样100万条'])
        self.sample_combo.setCurrentText('全量数据')
        toolbar.addWidget(self.sample_combo)
        
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
        data_preview_widget = QWidget()
        data_preview_layout = QVBoxLayout(data_preview_widget)
        
        self.data_preview = QTextEdit()
        self.data_preview.setReadOnly(True)
        data_preview_layout.addWidget(self.data_preview)
        
        # 分页控件
        page_control_layout = QHBoxLayout()
        
        self.prev_btn = QPushButton('上一页')
        self.prev_btn.clicked.connect(self.prev_page)
        self.prev_btn.setEnabled(False)
        page_control_layout.addWidget(self.prev_btn)
        
        self.page_label = QLabel('第 1 页，共 1 页')
        self.page_label.setAlignment(Qt.AlignCenter)
        page_control_layout.addWidget(self.page_label)
        
        self.next_btn = QPushButton('下一页')
        self.next_btn.clicked.connect(self.next_page)
        self.next_btn.setEnabled(False)
        page_control_layout.addWidget(self.next_btn)
        
        # 页面大小选择
        page_size_layout = QHBoxLayout()
        page_size_layout.addWidget(QLabel('每页显示:'))
        self.page_size_combo = QComboBox()
        self.page_size_combo.addItems(['50', '100', '200', '500'])
        self.page_size_combo.setCurrentText('50')
        self.page_size_combo.currentTextChanged.connect(self.change_page_size)
        page_size_layout.addWidget(self.page_size_combo)
        page_size_layout.addWidget(QLabel('条记录'))
        
        page_control_layout.addLayout(page_size_layout)
        page_control_layout.addStretch()
        
        data_preview_layout.addLayout(page_control_layout)
        self.tab_widget.addTab(data_preview_widget, '数据预览')
        
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
            
            # 获取采样设置
            sample_text = self.sample_combo.currentText()
            sample_size = None
            if sample_text == '采样10万条':
                sample_size = 100000
            elif sample_text == '采样50万条':
                sample_size = 500000
            elif sample_text == '采样100万条':
                sample_size = 1000000
            
            # 禁用控件，显示加载状态
            self.load_btn.setEnabled(False)
            self.sample_combo.setEnabled(False)
            
            # 在后台线程中加载数据
            self.load_thread = LoadThread(file_path, sample_size)
            self.load_thread.finished.connect(self.on_load_finished)
            self.load_thread.error.connect(self.on_load_error)
            self.load_thread.progress.connect(self.on_load_progress)
            self.load_thread.start()
    
    def on_load_progress(self, message):
        """加载进度更新"""
        self.status_bar.showMessage(message)
    
    def on_load_finished(self, data):
        """加载完成"""
        self.analyzer.data = data
        
        self.data_preview.setText(f"成功加载数据文件:\n\n")
        
        # 初始化分页
        self.current_page = 0
        if self.analyzer.data is not None:
            self.total_pages = (len(self.analyzer.data) + self.page_size - 1) // self.page_size
            self.update_data_preview()
            self.update_page_controls()
        
        self.analyze_btn.setEnabled(True)
        self.load_btn.setEnabled(True)
        self.sample_combo.setEnabled(True)
        self.status_bar.showMessage('数据加载完成')
    
    def on_load_error(self, error_message):
        """加载错误"""
        QMessageBox.warning(self, '错误', f'数据加载失败: {error_message}')
        self.load_btn.setEnabled(True)
        self.sample_combo.setEnabled(True)
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
    
    def update_data_preview(self):
        """更新数据预览显示"""
        if self.analyzer.data is None:
            return
        
        # 计算当前页的数据范围
        start_idx = self.current_page * self.page_size
        end_idx = min(start_idx + self.page_size, len(self.analyzer.data))
        
        # 获取当前页数据
        page_data = self.analyzer.data.iloc[start_idx:end_idx]
        
        # 更新显示
        self.data_preview.setText(f"数据预览 (第 {start_idx+1}-{end_idx} 条，共 {len(self.analyzer.data):,} 条):\n\n")
        self.data_preview.append(page_data.to_string())
    
    def update_page_controls(self):
        """更新分页控件状态"""
        # 更新页码标签
        self.page_label.setText(f'第 {self.current_page + 1} 页，共 {self.total_pages} 页')
        
        # 更新按钮状态
        self.prev_btn.setEnabled(self.current_page > 0)
        self.next_btn.setEnabled(self.current_page < self.total_pages - 1)
    
    def prev_page(self):
        """上一页"""
        if self.current_page > 0:
            self.current_page -= 1
            self.update_data_preview()
            self.update_page_controls()
    
    def next_page(self):
        """下一页"""
        if self.current_page < self.total_pages - 1:
            self.current_page += 1
            self.update_data_preview()
            self.update_page_controls()
    
    def change_page_size(self, size_text):
        """改变页面大小"""
        new_size = int(size_text)
        if new_size != self.page_size:
            self.page_size = new_size
            self.current_page = 0  # 回到第一页
            if self.analyzer.data is not None:
                self.total_pages = (len(self.analyzer.data) + self.page_size - 1) // self.page_size
                self.update_data_preview()
                self.update_page_controls()

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