"""
比较器模块
整合所有compare功能
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from scipy import stats


class AlgorithmComparator:
    """算法比较器"""
    
    def __init__(self):
        pass
    
    def compare_algorithms(self, data1: pd.DataFrame, data2: pd.DataFrame) -> Dict[str, Any]:
        """比较两个算法"""
        try:
            comparison = {
                'data1_stats': self._get_data_stats(data1),
                'data2_stats': self._get_data_stats(data2),
                'algorithm_similarity': self._compare_algorithm_similarity(data1, data2),
                'distribution_comparison': self._compare_distributions(data1, data2),
                'performance_comparison': self._compare_performance(data1, data2)
            }
            
            return comparison
            
        except Exception as e:
            return {'error': f'算法比较失败: {str(e)}'}
    
    def _get_data_stats(self, data: pd.DataFrame) -> Dict[str, Any]:
        """获取数据统计信息"""
        return {
            'total_records': len(data),
            'original_range': {
                'min': data['original_value'].min(),
                'max': data['original_value'].max(),
                'mean': data['original_value'].mean(),
                'std': data['original_value'].std()
            },
            'target_range': {
                'min': data['target_value'].min(),
                'max': data['target_value'].max(),
                'mean': data['target_value'].mean(),
                'std': data['target_value'].std()
            },
            'change_stats': {
                'mean': data['change'].mean(),
                'std': data['change'].std(),
                'min': data['change'].min(),
                'max': data['change'].max()
            }
        }
    
    def _compare_algorithm_similarity(self, data1: pd.DataFrame, data2: pd.DataFrame) -> Dict[str, Any]:
        """比较算法相似性"""
        try:
            # 计算线性拟合参数
            def get_linear_params(data):
                x = data['original_value'].values
                y = data['target_value'].values
                slope, intercept, r_value, p_value, std_err = stats.linregress(x, y)
                return slope, intercept, r_value**2
            
            slope1, intercept1, r2_1 = get_linear_params(data1)
            slope2, intercept2, r2_2 = get_linear_params(data2)
            
            # 计算参数差异
            slope_diff = abs(slope1 - slope2)
            intercept_diff = abs(intercept1 - intercept2)
            r2_diff = abs(r2_1 - r2_2)
            
            # 计算相似度分数
            similarity_score = 1.0 - (slope_diff + intercept_diff * 0.01 + r2_diff) / 3.0
            
            return {
                'slope_similarity': 1.0 - min(slope_diff, 1.0),
                'intercept_similarity': 1.0 - min(intercept_diff * 0.01, 1.0),
                'r2_similarity': 1.0 - min(r2_diff, 1.0),
                'overall_similarity': max(0.0, similarity_score),
                'is_similar': similarity_score > 0.8
            }
            
        except Exception as e:
            return {'error': f'算法相似性比较失败: {str(e)}'}
    
    def _compare_distributions(self, data1: pd.DataFrame, data2: pd.DataFrame) -> Dict[str, Any]:
        """比较分布"""
        try:
            # 比较原始值分布
            orig1 = data1['original_value'].values
            orig2 = data2['original_value'].values
            
            # 比较目标值分布
            target1 = data1['target_value'].values
            target2 = data2['target_value'].values
            
            # Kolmogorov-Smirnov检验
            orig_ks_stat, orig_ks_p = stats.ks_2samp(orig1, orig2)
            target_ks_stat, target_ks_p = stats.ks_2samp(target1, target2)
            
            return {
                'original_distribution': {
                    'ks_statistic': orig_ks_stat,
                    'p_value': orig_ks_p,
                    'is_similar': orig_ks_p > 0.05
                },
                'target_distribution': {
                    'ks_statistic': target_ks_stat,
                    'p_value': target_ks_p,
                    'is_similar': target_ks_p > 0.05
                },
                'overall_similarity': (orig_ks_p > 0.05) and (target_ks_p > 0.05)
            }
            
        except Exception as e:
            return {'error': f'分布比较失败: {str(e)}'}
    
    def _compare_performance(self, data1: pd.DataFrame, data2: pd.DataFrame) -> Dict[str, Any]:
        """比较性能"""
        try:
            # 计算变化量的统计特性
            change1 = data1['change'].values
            change2 = data2['change'].values
            
            # 计算变化的一致性
            change_corr = np.corrcoef(change1, change2)[0, 1] if len(change1) == len(change2) else 0
            
            return {
                'change_correlation': change_corr,
                'change_consistency': abs(change_corr) > 0.8,
                'performance_similarity': abs(change_corr)
            }
            
        except Exception as e:
            return {'error': f'性能比较失败: {str(e)}'}