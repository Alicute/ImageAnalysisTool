"""
模型拟合模块
整合model_fitting.py功能
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Any, Optional, Tuple
from scipy import stats
from scipy.optimize import curve_fit


class ModelFitter:
    """模型拟合器"""
    
    def __init__(self):
        pass
    
    def fit_multiple_models(self, data: pd.DataFrame) -> Dict[str, Any]:
        """拟合多种模型"""
        try:
            x = data['original_value'].values
            y = data['target_value'].values
            
            results = {}
            
            # 线性模型
            results['linear'] = self._fit_linear(x, y)
            
            # 多项式模型
            results['polynomial'] = self._fit_polynomial(x, y, degree=2)
            
            # 幂函数模型
            results['power'] = self._fit_power(x, y)
            
            # 对数模型
            results['logarithmic'] = self._fit_logarithmic(x, y)
            
            # 指数模型
            results['exponential'] = self._fit_exponential(x, y)
            
            # 找出最佳模型
            best_model = self._find_best_model(results)
            results['best_model'] = best_model
            
            return results
            
        except Exception as e:
            return {'error': f'模型拟合失败: {str(e)}'}
    
    def _fit_linear(self, x: np.ndarray, y: np.ndarray) -> Dict[str, Any]:
        """线性拟合"""
        try:
            slope, intercept, r_value, p_value, std_err = stats.linregress(x, y)
            
            return {
                'model_type': 'linear',
                'slope': slope,
                'intercept': intercept,
                'r_squared': r_value**2,
                'p_value': p_value,
                'std_error': std_err,
                'formula': f"y = {slope:.3f}x + {intercept:.1f}"
            }
            
        except Exception as e:
            return {'error': f'线性拟合失败: {str(e)}'}
    
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
    
    def _fit_power(self, x: np.ndarray, y: np.ndarray) -> Dict[str, Any]:
        """幂函数拟合"""
        try:
            # 确保x和y为正值
            if np.any(x <= 0) or np.any(y <= 0):
                return {'error': '幂函数拟合需要正值'}
            
            # 线性化: ln(y) = a*ln(x) + b
            log_x = np.log(x)
            log_y = np.log(y)
            
            # 线性回归
            slope, intercept, r_value, p_value, std_err = stats.linregress(log_x, log_y)
            
            return {
                'model_type': 'power',
                'a': slope,
                'b': intercept,
                'r_squared': r_value**2,
                'formula': f"y = {np.exp(intercept):.3f}x^{slope:.3f}"
            }
            
        except Exception as e:
            return {'error': f'幂函数拟合失败: {str(e)}'}
    
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
    
    def _find_best_model(self, results: Dict[str, Any]) -> Dict[str, Any]:
        """找出最佳模型"""
        best_model = None
        best_r_squared = -1
        
        for model_name, result in results.items():
            if model_name == 'best_model':
                continue
                
            if 'error' not in result:
                r_squared = result.get('r_squared', 0)
                if r_squared > best_r_squared:
                    best_r_squared = r_squared
                    best_model = {
                        'model_name': model_name,
                        'r_squared': r_squared,
                        'result': result
                    }
        
        return best_model or {'error': '没有可用的模型'}