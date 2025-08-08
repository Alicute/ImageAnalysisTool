"""
数据管理器模块
统一数据管理，整合chouqu.py功能
支持流式数据处理和内存优化
"""

import pandas as pd
import numpy as np
from typing import Dict, List, Optional, Tuple, Any
from collections import defaultdict
import gc
import time
import os
import threading
from .file_parser import FileParser


class DataManager:
    """统一数据管理器"""
    
    def __init__(self):
        self.file_parser = FileParser()
        self.current_data = None
        self.current_file_info = None
        self.sample_data = None
        self.statistics_cache = {}
        self._data_lock = threading.Lock()
        
    def load_data(self, file_path: str, sample_size: Optional[int] = None, 
                  sample_rate: float = 1.0) -> Tuple[bool, str]:
        """加载数据文件"""
        try:
            print(f"开始加载文件: {file_path}")
            start_time = time.time()
            
            # 获取文件信息
            self.current_file_info = self.file_parser.get_file_info(file_path)
            if 'error' in self.current_file_info:
                return False, f"文件信息获取失败: {self.current_file_info['error']}"
            
            # 解析数据
            if sample_size:
                # 按采样数量加载
                self.current_data = self.file_parser.parse_to_dataframe(
                    file_path, max_rows=sample_size, sample_rate=1.0
                )
            else:
                # 按采样率加载
                self.current_data = self.file_parser.parse_to_dataframe(
                    file_path, sample_rate=sample_rate
                )
            
            if self.current_data.empty:
                return False, "未找到有效数据"
            
            # 生成采样数据用于可视化
            self._generate_sample_data()
            
            # 计算基本统计信息
            self._calculate_basic_statistics()
            
            load_time = time.time() - start_time
            print(f"数据加载完成。耗时: {load_time:.2f} 秒")
            print(f"数据量: {len(self.current_data):,} 条记录")
            
            return True, "数据加载成功"
            
        except Exception as e:
            return False, f"数据加载失败: {str(e)}"
    
    def _generate_sample_data(self, max_samples: int = 10000):
        """生成采样数据用于可视化"""
        if len(self.current_data) <= max_samples:
            self.sample_data = self.current_data.copy()
        else:
            # 随机采样
            self.sample_data = self.current_data.sample(n=max_samples, random_state=42)
    
    def _calculate_basic_statistics(self):
        """计算基本统计信息 - 优化版本，避免内存溢出"""
        if self.current_data is None:
            return
        
        # 使用分块计算统计信息
        chunk_size = 500000  # 每块50万行
        
        try:
            # 如果数据量较小，直接计算
            if len(self.current_data) <= chunk_size:
                self.statistics_cache = {
                    'total_records': len(self.current_data),
                    'original_range': {
                        'min': self.current_data['original_value'].min(),
                        'max': self.current_data['original_value'].max(),
                        'mean': self.current_data['original_value'].mean(),
                        'std': self.current_data['original_value'].std()
                    },
                    'target_range': {
                        'min': self.current_data['target_value'].min(),
                        'max': self.current_data['target_value'].max(),
                        'mean': self.current_data['target_value'].mean(),
                        'std': self.current_data['target_value'].std()
                    },
                    'change_stats': {
                        'mean': self.current_data['change'].mean(),
                        'std': self.current_data['change'].std(),
                        'min': self.current_data['change'].min(),
                        'max': self.current_data['change'].max()
                    }
                }
                return
            
            # 大数据集使用分块计算
            total_stats = {
                'original_sum': 0,
                'original_sum_sq': 0,
                'target_sum': 0,
                'target_sum_sq': 0,
                'change_sum': 0,
                'change_sum_sq': 0,
                'count': 0,
                'orig_min': float('inf'),
                'orig_max': float('-inf'),
                'target_min': float('inf'),
                'target_max': float('-inf'),
                'change_min': float('inf'),
                'change_max': float('-inf')
            }
            
            for i in range(0, len(self.current_data), chunk_size):
                chunk = self.current_data.iloc[i:i + chunk_size]
                
                # 累加统计信息
                total_stats['original_sum'] += chunk['original_value'].sum()
                total_stats['original_sum_sq'] += (chunk['original_value'] ** 2).sum()
                total_stats['target_sum'] += chunk['target_value'].sum()
                total_stats['target_sum_sq'] += (chunk['target_value'] ** 2).sum()
                total_stats['change_sum'] += chunk['change'].sum()
                total_stats['change_sum_sq'] += (chunk['change'] ** 2).sum()
                total_stats['count'] += len(chunk)
                
                # 更新最小最大值
                total_stats['orig_min'] = min(total_stats['orig_min'], chunk['original_value'].min())
                total_stats['orig_max'] = max(total_stats['orig_max'], chunk['original_value'].max())
                total_stats['target_min'] = min(total_stats['target_min'], chunk['target_value'].min())
                total_stats['target_max'] = max(total_stats['target_max'], chunk['target_value'].max())
                total_stats['change_min'] = min(total_stats['change_min'], chunk['change'].min())
                total_stats['change_max'] = max(total_stats['change_max'], chunk['change'].max())
                
                # 释放内存
                del chunk
                import gc
                gc.collect()
            
            # 计算最终统计值
            n = total_stats['count']
            
            # 计算标准差，避免负数开方
            def safe_std(sum_sq, sum_val, count):
                variance = sum_sq / count - (sum_val / count) ** 2
                return max(0, variance) ** 0.5
            
            self.statistics_cache = {
                'total_records': n,
                'original_range': {
                    'min': total_stats['orig_min'],
                    'max': total_stats['orig_max'],
                    'mean': total_stats['original_sum'] / n,
                    'std': safe_std(total_stats['original_sum_sq'], total_stats['original_sum'], n)
                },
                'target_range': {
                    'min': total_stats['target_min'],
                    'max': total_stats['target_max'],
                    'mean': total_stats['target_sum'] / n,
                    'std': safe_std(total_stats['target_sum_sq'], total_stats['target_sum'], n)
                },
                'change_stats': {
                    'mean': total_stats['change_sum'] / n,
                    'std': safe_std(total_stats['change_sum_sq'], total_stats['change_sum'], n),
                    'min': total_stats['change_min'],
                    'max': total_stats['change_max']
                }
            }
            
        except Exception as e:
            print(f"统计计算失败，使用简化版本: {e}")
            # 如果分块计算失败，使用采样统计
            sample_size = min(100000, len(self.current_data))
            sample_data = self.current_data.sample(n=sample_size, random_state=42)
            
            self.statistics_cache = {
                'total_records': len(self.current_data),
                'original_range': {
                    'min': sample_data['original_value'].min(),
                    'max': sample_data['original_value'].max(),
                    'mean': sample_data['original_value'].mean(),
                    'std': sample_data['original_value'].std()
                },
                'target_range': {
                    'min': sample_data['target_value'].min(),
                    'max': sample_data['target_value'].max(),
                    'mean': sample_data['target_value'].mean(),
                    'std': sample_data['target_value'].std()
                },
                'change_stats': {
                    'mean': sample_data['change'].mean(),
                    'std': sample_data['change'].std(),
                    'min': sample_data['change'].min(),
                    'max': sample_data['change'].max()
                }
            }
    
    def get_data(self, sample_only: bool = False) -> pd.DataFrame:
        """获取数据 - 线程安全版本"""
        with self._data_lock:
            if sample_only:
                return self.sample_data.copy() if self.sample_data is not None else pd.DataFrame()
            return self.current_data.copy() if self.current_data is not None else pd.DataFrame()
    
    def get_statistics(self) -> Dict[str, Any]:
        """获取统计信息 - 线程安全版本"""
        with self._data_lock:
            return self.statistics_cache.copy()
    
    def get_file_info(self) -> Dict[str, Any]:
        """获取文件信息"""
        return self.current_file_info.copy() if self.current_file_info else {}
    
    def get_mapping_summary(self) -> pd.DataFrame:
        """获取映射关系摘要（类似chouqu.py的功能）"""
        if self.current_data is None:
            return pd.DataFrame()
        
        # 按原始值分组统计
        mapping_summary = self.current_data.groupby('original_value').agg({
            'target_value': ['count', 'mean', 'std', 'min', 'max'],
            'change': ['mean', 'std']
        }).round(2)
        
        # 扁平化列名
        mapping_summary.columns = ['_'.join(col).strip() for col in mapping_summary.columns]
        mapping_summary = mapping_summary.reset_index()
        
        return mapping_summary
    
    def get_unique_mappings(self) -> pd.DataFrame:
        """获取唯一映射关系"""
        if self.current_data is None:
            return pd.DataFrame()
        
        # 获取唯一的原始值-目标值对
        unique_mappings = self.current_data.drop_duplicates(subset=['original_value', 'target_value'])
        
        # 统计每个原始值的映射数量
        mapping_counts = self.current_data.groupby('original_value')['target_value'].nunique()
        
        # 标记一对一和多对一映射
        unique_mappings['mapping_type'] = unique_mappings['original_value'].map(
            lambda x: 'one_to_one' if mapping_counts.get(x, 0) == 1 else 'one_to_many'
        )
        
        return unique_mappings
    
    def analyze_mapping_patterns(self) -> Dict[str, Any]:
        """分析映射模式"""
        if self.current_data is None:
            return {}
        
        # 检查是否为全局统一算法
        unique_changes = self.current_data['change'].unique()
        is_global_algorithm = len(unique_changes) == 1
        
        # 计算相关性
        correlation = self.current_data['original_value'].corr(self.current_data['target_value'])
        
        # 检查线性关系
        from scipy import stats
        slope, intercept, r_value, p_value, std_err = stats.linregress(
            self.current_data['original_value'], 
            self.current_data['target_value']
        )
        
        return {
            'is_global_algorithm': is_global_algorithm,
            'unique_change_count': len(unique_changes),
            'correlation': correlation,
            'linear_fit': {
                'slope': slope,
                'intercept': intercept,
                'r_squared': r_value**2,
                'p_value': p_value,
                'std_error': std_err
            },
            'data_quality': {
                'total_records': len(self.current_data),
                'unique_original_values': self.current_data['original_value'].nunique(),
                'unique_target_values': self.current_data['target_value'].nunique(),
                'missing_values': self.current_data.isnull().sum().sum()
            }
        }
    
    def export_mapping_summary(self, output_path: str):
        """导出映射摘要到CSV文件"""
        try:
            mapping_summary = self.get_mapping_summary()
            mapping_summary.to_csv(output_path, index=False, encoding='utf-8-sig')
            return True, f"映射摘要已导出到: {output_path}"
        except Exception as e:
            return False, f"导出失败: {str(e)}"
    
    def clear_data(self):
        """清除数据，释放内存"""
        self.current_data = None
        self.sample_data = None
        self.current_file_info = None
        self.statistics_cache = {}
        gc.collect()
    
    def get_memory_usage(self) -> Dict[str, float]:
        """获取内存使用情况 - 简化版本，避免线程冲突"""
        try:
            # 简化的内存计算，避免使用psutil和deep=True
            current_data_mb = 0
            if self.current_data is not None:
                # 估算内存使用：行数 × 列数 × 8字节（float64）
                current_data_mb = len(self.current_data) * len(self.current_data.columns) * 8 / 1024 / 1024
            
            sample_data_mb = 0
            if self.sample_data is not None:
                sample_data_mb = len(self.sample_data) * len(self.sample_data.columns) * 8 / 1024 / 1024
            
            return {
                'rss_mb': current_data_mb + sample_data_mb,  # 估算值
                'vms_mb': current_data_mb + sample_data_mb,  # 估算值
                'current_data_mb': current_data_mb,
                'sample_data_mb': sample_data_mb
            }
        except Exception:
            # 如果计算失败，返回默认值
            return {
                'rss_mb': 0,
                'vms_mb': 0,
                'current_data_mb': 0,
                'sample_data_mb': 0
            }