"""
映射分析模块
整合find_mapping_logic.py功能
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from scipy import stats
from scipy.optimize import curve_fit


class MappingAnalyzer:
    """映射分析器"""
    
    def __init__(self):
        pass
    
    def analyze_linear_mapping(self, data: pd.DataFrame) -> Dict[str, Any]:
        """分析线性映射"""
        try:
            x = data['original_value'].values
            y = data['target_value'].values
            
            # 线性回归
            slope, intercept, r_value, p_value, std_err = stats.linregress(x, y)
            
            # 计算预测值和误差
            y_pred = slope * x + intercept
            errors = y - y_pred
            mse = np.mean(errors**2)
            rmse = np.sqrt(mse)
            mae = np.mean(np.abs(errors))
            
            return {
                'is_linear': r_value**2 > 0.95,
                'slope': slope,
                'intercept': intercept,
                'r_squared': r_value**2,
                'p_value': p_value,
                'std_error': std_err,
                'mse': mse,
                'rmse': rmse,
                'mae': mae,
                'formula': f"目标值 = {slope:.3f} × 原始值 + {intercept:.1f}",
                'correlation': r_value
            }
        
        except Exception as e:
            return {'error': f'线性分析失败: {str(e)}'}
    
    def find_constant_mapping(self, data: pd.DataFrame) -> Dict[str, Any]:
        """查找常量映射"""
        try:
            changes = data['change'].unique()
            
            if len(changes) == 1:
                constant_change = changes[0]
                return {
                    'is_constant': True,
                    'constant_change': constant_change,
                    'formula': f"目标值 = 原始值 {constant_change:+d}",
                    'confidence': 1.0
                }
            else:
                return {
                    'is_constant': False,
                    'unique_changes': len(changes),
                    'change_range': [changes.min(), changes.max()]
                }
        
        except Exception as e:
            return {'error': f'常量映射分析失败: {str(e)}'}
    
    def find_piecewise_mapping(self, data: pd.DataFrame, 
                              max_segments: int = 5) -> Dict[str, Any]:
        """查找分段映射"""
        try:
            # 按原始值排序
            sorted_data = data.sort_values('original_value').copy()
            
            # 寻找转折点
            turning_points = self._find_turning_points(sorted_data)
            
            if len(turning_points) == 0:
                return {
                    'is_piecewise': False,
                    'segments': 1
                }
            
            # 限制段数
            if len(turning_points) > max_segments - 1:
                turning_points = turning_points[:max_segments - 1]
            
            # 分析每一段
            segments = []
            start_idx = 0
            
            for tp in turning_points:
                end_idx = tp
                segment_data = sorted_data.iloc[start_idx:end_idx+1]
                
                if len(segment_data) > 10:  # 确保有足够的数据点
                    segment_analysis = self.analyze_linear_mapping(segment_data)
                    segment_analysis['start_value'] = segment_data['original_value'].iloc[0]
                    segment_analysis['end_value'] = segment_data['original_value'].iloc[-1]
                    segment_analysis['data_count'] = len(segment_data)
                    segments.append(segment_analysis)
                
                start_idx = end_idx + 1
            
            # 最后一段
            if start_idx < len(sorted_data):
                segment_data = sorted_data.iloc[start_idx:]
                if len(segment_data) > 10:
                    segment_analysis = self.analyze_linear_mapping(segment_data)
                    segment_analysis['start_value'] = segment_data['original_value'].iloc[0]
                    segment_analysis['end_value'] = segment_data['original_value'].iloc[-1]
                    segment_analysis['data_count'] = len(segment_data)
                    segments.append(segment_analysis)
            
            return {
                'is_piecewise': len(segments) > 1,
                'segments': segments,
                'turning_points': turning_points,
                'total_segments': len(segments)
            }
        
        except Exception as e:
            return {'error': f'分段映射分析失败: {str(e)}'}
    
    def _find_turning_points(self, data: pd.DataFrame, 
                           window_size: int = 1000, 
                           threshold: float = 0.1) -> List[int]:
        """寻找转折点"""
        try:
            if len(data) < window_size * 2:
                return []
            
            # 计算滑动窗口的斜率
            slopes = []
            for i in range(len(data) - window_size + 1):
                window_data = data.iloc[i:i+window_size]
                slope, _, r_value, _, _ = stats.linregress(
                    window_data['original_value'], 
                    window_data['target_value']
                )
                slopes.append((slope, r_value**2, i + window_size//2))
            
            # 寻找斜率变化点
            turning_points = []
            for i in range(1, len(slopes)):
                if abs(slopes[i][0] - slopes[i-1][0]) > threshold:
                    turning_points.append(slopes[i][2])
            
            # 去重和过滤
            turning_points = sorted(list(set(turning_points)))
            filtered_points = []
            
            for tp in turning_points:
                if 100 < tp < len(data) - 100:  # 避免边缘点
                    filtered_points.append(tp)
            
            return filtered_points
        
        except Exception as e:
            print(f"转折点检测失败: {e}")
            return []
    
    def analyze_mapping_distribution(self, data: pd.DataFrame) -> Dict[str, Any]:
        """分析映射分布"""
        try:
            # 原始值分布
            orig_dist = {
                'min': data['original_value'].min(),
                'max': data['original_value'].max(),
                'mean': data['original_value'].mean(),
                'std': data['original_value'].std(),
                'median': data['original_value'].median(),
                'quartiles': data['original_value'].quantile([0.25, 0.5, 0.75]).to_dict()
            }
            
            # 目标值分布
            target_dist = {
                'min': data['target_value'].min(),
                'max': data['target_value'].max(),
                'mean': data['target_value'].mean(),
                'std': data['target_value'].std(),
                'median': data['target_value'].median(),
                'quartiles': data['target_value'].quantile([0.25, 0.5, 0.75]).to_dict()
            }
            
            # 变化量分布
            change_dist = {
                'min': data['change'].min(),
                'max': data['change'].max(),
                'mean': data['change'].mean(),
                'std': data['change'].std(),
                'median': data['change'].median(),
                'quartiles': data['change'].quantile([0.25, 0.5, 0.75]).to_dict()
            }
            
            return {
                'original_distribution': orig_dist,
                'target_distribution': target_dist,
                'change_distribution': change_dist,
                'unique_original_values': data['original_value'].nunique(),
                'unique_target_values': data['target_value'].nunique(),
                'unique_mappings': len(data.drop_duplicates(['original_value', 'target_value']))
            }
        
        except Exception as e:
            return {'error': f'分布分析失败: {str(e)}'}