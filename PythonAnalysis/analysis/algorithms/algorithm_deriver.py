"""
算法推导模块
整合derive_algorithm.py功能
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from scipy import stats
from scipy.optimize import curve_fit
from .mapping_analyzer import MappingAnalyzer


class AlgorithmDeriver:
    """算法推导器"""
    
    def __init__(self):
        self.mapping_analyzer = MappingAnalyzer()
        
    def derive_algorithm(self, data: pd.DataFrame) -> Dict[str, Any]:
        """推导算法"""
        try:
            # 首先检查是否为常量算法
            constant_result = self.mapping_analyzer.find_constant_mapping(data)
            if constant_result.get('is_constant', False):
                return {
                    'algorithm_type': 'constant',
                    'algorithm': constant_result,
                    'confidence': 1.0
                }
            
            # 检查线性算法
            linear_result = self.mapping_analyzer.analyze_linear_mapping(data)
            if linear_result.get('is_linear', False):
                return {
                    'algorithm_type': 'linear',
                    'algorithm': linear_result,
                    'confidence': linear_result.get('r_squared', 0)
                }
            
            # 检查分段线性算法
            piecewise_result = self.mapping_analyzer.find_piecewise_mapping(data)
            if piecewise_result.get('is_piecewise', False):
                return {
                    'algorithm_type': 'piecewise_linear',
                    'algorithm': piecewise_result,
                    'confidence': 0.8
                }
            
            # 如果都不是，尝试其他模型
            model_result = self._try_other_models(data)
            return {
                'algorithm_type': 'complex',
                'algorithm': model_result,
                'confidence': 0.5
            }
            
        except Exception as e:
            return {'error': f'算法推导失败: {str(e)}'}
    
    def derive_piecewise_algorithm(self, data: pd.DataFrame) -> Dict[str, Any]:
        """推导分段算法"""
        try:
            piecewise_result = self.mapping_analyzer.find_piecewise_mapping(data)
            
            if not piecewise_result.get('is_piecewise', False):
                return {
                    'is_piecewise': False,
                    'segments': 1,
                    'algorithm': 'linear'
                }
            
            # 优化分段点
            optimized_segments = self._optimize_segments(data, piecewise_result)
            
            return {
                'is_piecewise': True,
                'segments': optimized_segments,
                'algorithm': 'piecewise_linear'
            }
            
        except Exception as e:
            return {'error': f'分段算法推导失败: {str(e)}'}
    
    def _optimize_segments(self, data: pd.DataFrame, piecewise_result: Dict[str, Any]) -> List[Dict[str, Any]]:
        """优化分段点"""
        segments = piecewise_result.get('segments', [])
        
        # 这里可以实现更复杂的分段点优化算法
        # 目前直接返回原始分段
        return segments
    
    def _try_other_models(self, data: pd.DataFrame) -> Dict[str, Any]:
        """尝试其他模型"""
        try:
            x = data['original_value'].values
            y = data['target_value'].values
            
            # 尝试多项式拟合
            poly_results = self._fit_polynomial(x, y, degree=2)
            
            # 尝试指数拟合
            exp_results = self._fit_exponential(x, y)
            
            # 尝试对数拟合
            log_results = self._fit_logarithmic(x, y)
            
            # 选择最佳模型
            best_model = max([poly_results, exp_results, log_results], 
                           key=lambda x: x.get('r_squared', 0))
            
            return best_model
            
        except Exception as e:
            return {'error': f'模型拟合失败: {str(e)}'}
    
    def _fit_polynomial(self, x: np.ndarray, y: np.ndarray, degree: int = 2) -> Dict[str, Any]:
        """多项式拟合"""
        try:
            coeffs = np.polyfit(x, y, degree)
            y_pred = np.polyval(coeffs, x)
            
            # 计算R²
            ss_res = np.sum((y - y_pred) ** 2)
            ss_tot = np.sum((y - np.mean(y)) ** 2)
            r_squared = 1 - (ss_res / ss_tot)
            
            return {
                'model_type': 'polynomial',
                'degree': degree,
                'coefficients': coeffs.tolist(),
                'r_squared': r_squared,
                'formula': self._format_polynomial_formula(coeffs)
            }
            
        except Exception as e:
            return {'error': f'多项式拟合失败: {str(e)}'}
    
    def _fit_exponential(self, x: np.ndarray, y: np.ndarray) -> Dict[str, Any]:
        """指数拟合"""
        try:
            # 确保y为正值
            if np.any(y <= 0):
                return {'error': '指数拟合需要正值'}
            
            # 线性化: ln(y) = a*x + b
            log_y = np.log(y)
            
            # 线性回归
            slope, intercept, r_value, p_value, std_err = stats.linregress(x, log_y)
            
            return {
                'model_type': 'exponential',
                'a': slope,
                'b': intercept,
                'r_squared': r_value**2,
                'formula': f"y = exp({slope:.3f}x + {intercept:.1f})"
            }
            
        except Exception as e:
            return {'error': f'指数拟合失败: {str(e)}'}
    
    def _fit_logarithmic(self, x: np.ndarray, y: np.ndarray) -> Dict[str, Any]:
        """对数拟合"""
        try:
            # 确保x为正值
            if np.any(x <= 0):
                return {'error': '对数拟合需要正值'}
            
            # 线性化: y = a*ln(x) + b
            log_x = np.log(x)
            
            # 线性回归
            slope, intercept, r_value, p_value, std_err = stats.linregress(log_x, y)
            
            return {
                'model_type': 'logarithmic',
                'a': slope,
                'b': intercept,
                'r_squared': r_value**2,
                'formula': f"y = {slope:.3f}ln(x) + {intercept:.1f}"
            }
            
        except Exception as e:
            return {'error': f'对数拟合失败: {str(e)}'}
    
    def _format_polynomial_formula(self, coeffs: np.ndarray) -> str:
        """格式化多项式公式"""
        terms = []
        for i, coeff in enumerate(coeffs):
            if i == 0:
                terms.append(f"{coeff:.3f}")
            elif i == 1:
                terms.append(f"{coeff:.3f}x")
            else:
                terms.append(f"{coeff:.3f}x^{i}")
        
        return " + ".join(reversed(terms))