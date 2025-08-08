"""
报告生成器模块
"""

import pandas as pd
from typing import Dict, Any
from datetime import datetime

from ..core.data_manager import DataManager
from ..core.algorithm_engine import AlgorithmEngine


class ReportGenerator:
    """报告生成器"""
    
    def __init__(self):
        pass
    
    def generate_comprehensive_report(self, data_manager: DataManager, 
                                    algorithm_engine: AlgorithmEngine) -> str:
        """生成综合报告"""
        try:
            report = "# 像素映射分析报告\n\n"
            report += f"生成时间: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n"
            
            # 数据概况
            report += self._generate_data_overview(data_manager)
            
            # 分析结果
            if algorithm_engine.current_analysis:
                report += self._generate_analysis_results(algorithm_engine.current_analysis)
            
            # 算法推断
            report += self._generate_algorithm_inference(algorithm_engine)
            
            # 建议
            report += self._generate_recommendations(algorithm_engine)
            
            return report
            
        except Exception as e:
            return f"报告生成失败: {str(e)}"
    
    def _generate_data_overview(self, data_manager: DataManager) -> str:
        """生成数据概况"""
        report = "## 1. 数据概况\n\n"
        
        stats = data_manager.get_statistics()
        file_info = data_manager.get_file_info()
        
        # 文件信息
        if file_info:
            report += f"- **文件路径**: {file_info.get('file_path', '未知')}\n"
            report += f"- **文件格式**: {file_info.get('format_type', '未知')}\n"
            report += f"- **文件大小**: {file_info.get('file_size', 0) / 1024 / 1024:.1f} MB\n"
            report += f"- **估计数据行数**: {file_info.get('estimated_data_lines', 0):,}\n\n"
        
        # 数据统计
        if stats:
            report += f"- **总记录数**: {stats.get('total_records', 0):,}\n"
            
            if 'original_range' in stats:
                orig = stats['original_range']
                report += f"- **原始值范围**: {orig.get('min', 0):,} - {orig.get('max', 0):,}\n"
                report += f"- **原始值均值**: {orig.get('mean', 0):.1f}\n"
                report += f"- **原始值标准差**: {orig.get('std', 0):.1f}\n"
            
            if 'target_range' in stats:
                target = stats['target_range']
                report += f"- **目标值范围**: {target.get('min', 0):,} - {target.get('max', 0):,}\n"
                report += f"- **目标值均值**: {target.get('mean', 0):.1f}\n"
                report += f"- **目标值标准差**: {target.get('std', 0):.1f}\n"
            
            if 'change_stats' in stats:
                change = stats['change_stats']
                report += f"- **变化量均值**: {change.get('mean', 0):.1f}\n"
                report += f"- **变化量标准差**: {change.get('std', 0):.1f}\n"
                report += f"- **变化量范围**: {change.get('min', 0):,} - {change.get('max', 0):,}\n"
        
        report += "\n"
        return report
    
    def _generate_analysis_results(self, analysis: Dict[str, Any]) -> str:
        """生成分析结果"""
        report = "## 2. 分析结果\n\n"
        
        # 映射模式
        if 'mapping_patterns' in analysis:
            patterns = analysis['mapping_patterns']
            report += "### 2.1 映射模式分析\n\n"
            
            if patterns.get('is_global_algorithm', False):
                report += "- **算法类型**: 全局统一算法\n"
                report += "- **特点**: 所有像素应用相同的变换规则\n"
            else:
                report += "- **算法类型**: 复杂算法\n"
                report += "- **特点**: 存在多种映射规则\n"
            
            report += f"- **唯一变化值数量**: {patterns.get('unique_change_count', 0)}\n"
            report += f"- **相关性**: {patterns.get('correlation', 0):.3f}\n"
            
            if 'linear_fit' in patterns:
                linear = patterns['linear_fit']
                report += f"- **线性拟合R²**: {linear.get('r_squared', 0):.3f}\n"
            
            report += "\n"
        
        # 线性分析
        if 'linear_analysis' in analysis:
            linear = analysis['linear_analysis']
            report += "### 2.2 线性分析\n\n"
            
            if 'error' not in linear:
                report += f"- **是否线性**: {'是' if linear.get('is_linear', False) else '否'}\n"
                report += f"- **斜率**: {linear.get('slope', 0):.3f}\n"
                report += f"- **截距**: {linear.get('intercept', 0):.1f}\n"
                report += f"- **R²**: {linear.get('r_squared', 0):.3f}\n"
                report += f"- **均方误差**: {linear.get('mse', 0):.2f}\n"
                report += f"- **公式**: {linear.get('formula', '')}\n"
            else:
                report += f"- **分析失败**: {linear['error']}\n"
            
            report += "\n"
        
        # 分段分析
        if 'piecewise_analysis' in analysis:
            piecewise = analysis['piecewise_analysis']
            report += "### 2.3 分段分析\n\n"
            
            if 'error' not in piecewise:
                report += f"- **是否分段**: {'是' if piecewise.get('is_piecewise', False) else '否'}\n"
                report += f"- **分段数量**: {piecewise.get('total_segments', 0)}\n"
                
                segments = piecewise.get('segments', [])
                for i, segment in enumerate(segments):
                    report += f"- **段 {i+1}**: {segment.get('start_value', 0):,} - {segment.get('end_value', 0):,}\n"
                    if 'formula' in segment:
                        report += f"  - 公式: {segment['formula']}\n"
                    if 'r_squared' in segment:
                        report += f"  - R²: {segment['r_squared']:.3f}\n"
            else:
                report += f"- **分析失败**: {piecewise['error']}\n"
            
            report += "\n"
        
        # 模型拟合
        if 'model_fitting' in analysis:
            models = analysis['model_fitting']
            report += "### 2.4 模型拟合\n\n"
            
            if 'best_model' in models:
                best = models['best_model']
                if 'error' not in best:
                    report += f"- **最佳模型**: {best.get('model_name', '未知')}\n"
                    report += f"- **R²**: {best.get('r_squared', 0):.3f}\n"
                    
                    if 'result' in best and 'formula' in best['result']:
                        report += f"- **公式**: {best['result']['formula']}\n"
            
            report += "\n"
        
        return report
    
    def _generate_algorithm_inference(self, algorithm_engine: AlgorithmEngine) -> str:
        """生成算法推断"""
        report = "## 3. 算法推断\n\n"
        
        summary = algorithm_engine.get_analysis_summary()
        if 'error' not in summary:
            summary_data = summary.get('summary', {})
            
            report += f"- **算法类型**: {summary_data.get('algorithm_type', '未知')}\n"
            report += f"- **算法描述**: {summary_data.get('algorithm_description', '未知')}\n"
            report += f"- **置信度**: {summary_data.get('confidence', 0):.3f}\n"
            
            # 根据算法类型给出具体推断
            algo_type = summary_data.get('algorithm_type', 'unknown')
            if algo_type == 'global_constant':
                report += "\n**推断结果**: 这是一个全局常量偏移算法，所有像素都应用相同的偏移量。\n"
            elif algo_type == 'linear':
                report += "\n**推断结果**: 这是一个线性映射算法，目标值与原始值成线性关系。\n"
            elif algo_type == 'piecewise_linear':
                report += "\n**推断结果**: 这是一个分段线性算法，不同范围的像素值应用不同的线性变换。\n"
            else:
                report += "\n**推断结果**: 这是一个复杂算法，可能需要进一步分析。\n"
        
        report += "\n"
        return report
    
    def _generate_recommendations(self, algorithm_engine: AlgorithmEngine) -> str:
        """生成建议"""
        report = "## 4. 建议\n\n"
        
        summary = algorithm_engine.get_analysis_summary()
        if 'error' not in summary:
            summary_data = summary.get('summary', {})
            algo_type = summary_data.get('algorithm_type', 'unknown')
            confidence = summary_data.get('confidence', 0)
            
            if confidence > 0.9:
                report += "### 高置信度建议\n\n"
                report += "- 算法类型已确定，可以直接用于实现\n"
                report += "- 建议验证算法在边界条件下的表现\n"
                report += "- 可以考虑性能优化\n"
            elif confidence > 0.7:
                report += "### 中等置信度建议\n\n"
                report += "- 算法类型基本确定，但需要进一步验证\n"
                report += "- 建议测试更多数据点\n"
                report += "- 可能需要调整参数\n"
            else:
                report += "### 低置信度建议\n\n"
                report += "- 算法类型不确定，需要进一步分析\n"
                report += "- 建议收集更多数据\n"
                report += "- 可能需要考虑更复杂的模型\n"
            
            # 根据算法类型给出具体建议
            if algo_type == 'global_constant':
                report += "\n**实现建议**:\n"
                report += "1. 直接应用常量偏移\n"
                report += "2. 注意边界值处理\n"
                report += "3. 考虑溢出保护\n"
            elif algo_type == 'linear':
                report += "\n**实现建议**:\n"
                report += "1. 使用线性变换公式\n"
                report += "2. 注意浮点数精度\n"
                report += "3. 考虑整数化处理\n"
            elif algo_type == 'piecewise_linear':
                report += "\n**实现建议**:\n"
                report += "1. 实现分段逻辑\n"
                report += "2. 优化分段点查找\n"
                report += "3. 考虑边界条件\n"
        
        report += "\n### 通用建议\n\n"
        report += "- 建议在实际数据上验证算法\n"
        report += "- 考虑算法的性能和内存使用\n"
        report += "- 注意异常值和边界情况的处理\n"
        report += "- 建议编写单元测试\n"
        
        report += "\n"
        return report