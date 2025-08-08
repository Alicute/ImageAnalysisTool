"""
文件解析器模块
支持流式读取多种格式的像素映射数据文件
"""

import re
import pandas as pd
from collections import defaultdict
from typing import Iterator, Dict, List, Optional, Tuple
import time


class FileParser:
    """统一文件解析器，支持流式读取"""
    
    def __init__(self):
        self.supported_formats = {
            'pixel_mapping': self._parse_pixel_mapping,
            'simple_mapping': self._parse_simple_mapping,
            'dicom_format': self._parse_dicom_format
        }
    
    def detect_format(self, file_path: str) -> str:
        """自动检测文件格式"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                first_lines = [f.readline().strip() for _ in range(10)]
                
            # 检查DICOM格式
            if any('===完整16位DICOM像素映射数据===' in line for line in first_lines):
                return 'dicom_format'
            
            # 检查像素映射格式
            if any('原值' in line and '→' in line for line in first_lines):
                return 'simple_mapping'
            
            # 检查标准像素映射格式
            if any('位置(' in line and '原值' in line and '→' in line for line in first_lines):
                return 'pixel_mapping'
                
        except Exception as e:
            print(f"格式检测失败: {e}")
            
        return 'unknown'
    
    def parse_line_by_line(self, file_path: str, format_type: str = None) -> Iterator[Dict]:
        """流式逐行解析文件"""
        if format_type is None:
            format_type = self.detect_format(file_path)
        
        if format_type not in self.supported_formats:
            raise ValueError(f"不支持的文件格式: {format_type}")
        
        parser = self.supported_formats[format_type]
        yield from parser(file_path)
    
    def _parse_pixel_mapping(self, file_path: str) -> Iterator[Dict]:
        """解析标准像素映射格式"""
        pixel_pattern = re.compile(r'\[(\d+)\]位置\((\d+),(\d+)\)原值(\d+)→新值(\d+)\(变化:([-\d]+),([-\d.]+)%\)')
        
        with open(file_path, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                match = pixel_pattern.search(line.strip())
                if match:
                    idx, x, y, orig, new, change, percent = match.groups()
                    yield {
                        'line_number': line_num,
                        'index': int(idx),
                        'x': int(x),
                        'y': int(y),
                        'original_value': int(orig),
                        'target_value': int(new),
                        'change': int(change),
                        'change_percent': float(percent)
                    }
    
    def _parse_simple_mapping(self, file_path: str) -> Iterator[Dict]:
        """解析简单映射格式（原值X→新值Y）"""
        line_pattern = re.compile(r'原值(\d+)\s*→\s*新值(\d+)')
        
        with open(file_path, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                match = line_pattern.search(line.strip())
                if match:
                    original_value = int(match.group(1))
                    target_value = int(match.group(2))
                    yield {
                        'line_number': line_num,
                        'original_value': original_value,
                        'target_value': target_value,
                        'change': target_value - original_value,
                        'change_percent': ((target_value - original_value) / original_value * 100) if original_value != 0 else 0
                    }
    
    def _parse_dicom_format(self, file_path: str) -> Iterator[Dict]:
        """解析DICOM格式文件"""
        pixel_pattern = re.compile(r'\[(\d+)\] 位置\((\d+),(\d+)\) 原值(\d+) → 新值(\d+) \(变化: ([\-\d]+), ([\-\d.]+)%\)')
        
        with open(file_path, 'r', encoding='utf-8') as f:
            for line_num, line in enumerate(f, 1):
                match = pixel_pattern.search(line.strip())
                if match:
                    idx, x, y, orig, new, change, percent = match.groups()
                    yield {
                        'line_number': line_num,
                        'index': int(idx),
                        'x': int(x),
                        'y': int(y),
                        'original_value': int(orig),
                        'target_value': int(new),
                        'change': int(change),
                        'change_percent': float(percent)
                    }
    
    def get_file_info(self, file_path: str) -> Dict:
        """获取文件基本信息"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                total_lines = sum(1 for _ in f)
            
            format_type = self.detect_format(file_path)
            
            # 快速估算数据行数
            sample_lines = 0
            for i, data in enumerate(self.parse_line_by_line(file_path, format_type)):
                sample_lines += 1
                if i >= 1000:  # 只采样前1000行
                    break
            
            # 估算总数据行数
            if sample_lines > 0:
                estimated_data_lines = int(total_lines * (sample_lines / min(1000, total_lines)))
            else:
                estimated_data_lines = 0
            
            return {
                'file_path': file_path,
                'total_lines': total_lines,
                'format_type': format_type,
                'estimated_data_lines': estimated_data_lines,
                'file_size': self._get_file_size(file_path)
            }
            
        except Exception as e:
            return {
                'file_path': file_path,
                'error': str(e)
            }
    
    def _get_file_size(self, file_path: str) -> int:
        """获取文件大小（字节）"""
        try:
            import os
            return os.path.getsize(file_path)
        except:
            return 0
    
    def parse_to_dataframe(self, file_path: str, max_rows: Optional[int] = None, 
                          sample_rate: float = 1.0) -> pd.DataFrame:
        """解析文件到DataFrame - 优化版本，避免内存溢出"""
        # 使用分块处理避免内存溢出
        chunk_size = 100000  # 每块10万行
        chunks = []
        current_chunk = []
        processed_count = 0
        
        for i, record in enumerate(self.parse_line_by_line(file_path)):
            if max_rows and processed_count >= max_rows:
                break
                
            # 采样逻辑
            if sample_rate < 1.0 and (i % int(1/sample_rate) != 0):
                continue
                
            current_chunk.append(record)
            processed_count += 1
            
            # 分块处理
            if len(current_chunk) >= chunk_size:
                chunk_df = pd.DataFrame(current_chunk)
                chunks.append(chunk_df)
                current_chunk = []
                
                # 立即释放内存
                del chunk_df
                import gc
                gc.collect()
                
                # 进度反馈
                if processed_count % 1000000 == 0:
                    print(f"已处理 {processed_count:,} 条记录...")
        
        # 处理最后一块
        if current_chunk:
            chunk_df = pd.DataFrame(current_chunk)
            chunks.append(chunk_df)
        
        if not chunks:
            return pd.DataFrame()
        
        # 合并所有块
        df = pd.concat(chunks, ignore_index=True)
        
        # 优化数据类型
        self._optimize_dtypes(df)
        
        return df
    
    def _optimize_dtypes(self, df):
        """优化DataFrame数据类型"""
        if 'index' in df.columns:
            df['index'] = pd.to_numeric(df['index'], downcast='unsigned')
        if 'x' in df.columns:
            df['x'] = pd.to_numeric(df['x'], downcast='unsigned')
        if 'y' in df.columns:
            df['y'] = pd.to_numeric(df['y'], downcast='unsigned')
        if 'original_value' in df.columns:
            df['original_value'] = pd.to_numeric(df['original_value'], downcast='unsigned')
        if 'target_value' in df.columns:
            df['target_value'] = pd.to_numeric(df['target_value'], downcast='unsigned')
        if 'change' in df.columns:
            df['change'] = pd.to_numeric(df['change'], downcast='integer')
        if 'change_percent' in df.columns:
            df['change_percent'] = pd.to_numeric(df['change_percent'], downcast='float')