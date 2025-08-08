"""
算法引擎核心模块
整合所有分析算法功能
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from PyQt5.QtCore import QObject, pyqtSignal
from .data_manager import DataManager
from ..algorithms.mapping_analyzer import MappingAnalyzer
from ..algorithms.algorithm_deriver import AlgorithmDeriver
from ..algorithms.model_fitter import ModelFitter
from ..algorithms.comparator import AlgorithmComparator


class AlgorithmEngine(QObject):
    """算法引擎核心类"""
    
    # 信号定义
    analysis_completed = pyqtSignal(dict)
    analysis_error = pyqtSignal(str)
    analysis_progress = pyqtSignal(str)
    
    def __init__(self):
        super().__init__()
        self.data_manager = DataManager()
        self.mapping_analyzer = MappingAnalyzer()
        self.algorithm_deriver = AlgorithmDeriver()
        self.model_fitter = ModelFitter()
        self.comparator = AlgorithmComparator()
        self.current_analysis = {}
    
    def analyze_mapping(self, analysis_type: str = 'comprehensive') -> Dict[str, Any]:
        """分析映射关系"""
        if self.data_manager.current_data is None:
            error_msg = {'error': '未加载数据'}
            self.analysis_error.emit('未加载数据')
            return error_msg
        
        try:
            self.analysis_progress.emit(f'开始{analysis_type}分析...')
            
            if analysis_type == 'comprehensive':
                result = self._comprehensive_analysis()
            elif analysis_type == 'linear':
                result = self._linear_analysis()
            elif analysis_type == 'piecewise':
                result = self._piecewise_analysis()
            else:
                result = {'error': f'不支持的分析类型: {analysis_type}'}
                self.analysis_error.emit(f'不支持的分析类型: {analysis_type}')
                return result
            
            self.analysis_progress.emit('分析完成')
            self.analysis_completed.emit(result)
            return result
        
        except Exception as e:
            error_msg = {'error': f'分析失败: {str(e)}'}
            self.analysis_error.emit(str(e))
            return error_msg
    
    def _comprehensive_analysis(self) -> Dict[str, Any]:
        """综合分析"""
        # 基础统计
        basic_stats = self.data_manager.get_statistics()
        
        # 映射模式分析
        mapping_patterns = self.data_manager.analyze_mapping_patterns()
        
        # 线性分析
        linear_result = self.mapping_analyzer.analyze_linear_mapping(
            self.data_manager.current_data
        )
        
        # 分段分析
        piecewise_result = self.algorithm_deriver.derive_piecewise_algorithm(
            self.data_manager.current_data
        )
        
        # 模型拟合
        model_results = self.model_fitter.fit_multiple_models(
            self.data_manager.current_data
        )
        
        self.current_analysis = {
            'analysis_type': 'comprehensive',
            'basic_statistics': basic_stats,
            'mapping_patterns': mapping_patterns,
            'linear_analysis': linear_result,
            'piecewise_analysis': piecewise_result,
            'model_fitting': model_results,
            'timestamp': pd.Timestamp.now().isoformat()
        }
        
        return self.current_analysis
    
    def _linear_analysis(self) -> Dict[str, Any]:
        """线性分析"""
        linear_result = self.mapping_analyzer.analyze_linear_mapping(
            self.data_manager.current_data
        )
        
        return {
            'analysis_type': 'linear',
            'linear_analysis': linear_result,
            'timestamp': pd.Timestamp.now().isoformat()
        }
    
    def _piecewise_analysis(self) -> Dict[str, Any]:
        """分段分析"""
        piecewise_result = self.algorithm_deriver.derive_piecewise_algorithm(
            self.data_manager.current_data
        )
        
        return {
            'analysis_type': 'piecewise',
            'piecewise_analysis': piecewise_result,
            'timestamp': pd.Timestamp.now().isoformat()
        }
    
    def derive_algorithm(self) -> Dict[str, Any]:
        """推导算法"""
        if self.data_manager.current_data is None:
            error_msg = {'error': '未加载数据'}
            self.analysis_error.emit('未加载数据')
            return error_msg
        
        try:
            self.analysis_progress.emit('开始算法推导...')
            
            algorithm = self.algorithm_deriver.derive_algorithm(
                self.data_manager.current_data
            )
            
            result = {
                'algorithm': algorithm,
                'timestamp': pd.Timestamp.now().isoformat()
            }
            
            self.analysis_progress.emit('算法推导完成')
            self.analysis_completed.emit(result)
            return result
        
        except Exception as e:
            error_msg = {'error': f'算法推导失败: {str(e)}'}
            self.analysis_error.emit(str(e))
            return error_msg
    
    def compare_algorithms(self, other_data: pd.DataFrame) -> Dict[str, Any]:
        """比较算法"""
        if self.data_manager.current_data is None:
            error_msg = {'error': '未加载数据'}
            self.analysis_error.emit('未加载数据')
            return error_msg
        
        try:
            self.analysis_progress.emit('开始算法比较...')
            
            comparison = self.comparator.compare_algorithms(
                self.data_manager.current_data,
                other_data
            )
            
            result = {
                'comparison': comparison,
                'timestamp': pd.Timestamp.now().isoformat()
            }
            
            self.analysis_progress.emit('算法比较完成')
            self.analysis_completed.emit(result)
            return result
        
        except Exception as e:
            error_msg = {'error': f'算法比较失败: {str(e)}'}
            self.analysis_error.emit(str(e))
            return error_msg
    
    def get_analysis_summary(self) -> Dict[str, Any]:
        """获取分析摘要"""
        if not self.current_analysis:
            return {'error': '未进行分析'}
        
        return {
            'summary': self._generate_summary(),
            'timestamp': pd.Timestamp.now().isoformat()
        }
    
    def _generate_summary(self) -> Dict[str, Any]:
        """生成分析摘要"""
        if not self.current_analysis:
            return {}
        
        analysis = self.current_analysis
        
        summary = {
            'data_overview': {
                'total_records': analysis.get('basic_statistics', {}).get('total_records', 0),
                'unique_original_values': analysis.get('mapping_patterns', {}).get('data_quality', {}).get('unique_original_values', 0),
                'unique_target_values': analysis.get('mapping_patterns', {}).get('data_quality', {}).get('unique_target_values', 0),
            },
            'algorithm_type': 'unknown',
            'algorithm_description': '未知算法',
            'confidence': 0.0
        }
        
        # 判断算法类型
        mapping_patterns = analysis.get('mapping_patterns', {})
        if mapping_patterns.get('is_global_algorithm', False):
            summary['algorithm_type'] = 'global_constant'
            summary['algorithm_description'] = '全局常量算法'
            summary['confidence'] = 1.0
        elif mapping_patterns.get('linear_fit', {}).get('r_squared', 0) > 0.95:
            summary['algorithm_type'] = 'linear'
            summary['algorithm_description'] = '线性映射算法'
            summary['confidence'] = mapping_patterns['linear_fit']['r_squared']
        elif analysis.get('piecewise_analysis'):
            summary['algorithm_type'] = 'piecewise_linear'
            summary['algorithm_description'] = '分段线性算法'
            summary['confidence'] = 0.8
        
        return summary
    
    def clear_analysis(self):
        """清除分析结果"""
        self.current_analysis = {}