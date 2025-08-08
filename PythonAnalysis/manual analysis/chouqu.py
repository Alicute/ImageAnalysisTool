# process_data.py

import re
import pandas as pd
from collections import defaultdict
import time

# --- 配置 ---
# 已将您的文件路径直接填入，注意路径前的 'r' 是为了防止反斜杠被转义
# input_filename = r'C:\Users\lkdev\Desktop\完整像素数据_20250806_130112.txt'
# input_filename = r'C:\Users\lkdev\Desktop\完整像素数据_20250806_203718.txt'
input_filename = r'C:\Users\lkdev\Desktop\完整像素数据_20250807_133418.txt'
# 这是将要生成的摘要文件的名称，会保存在您运行脚本的相同目录下
output_filename = 'summary_mapping4.csv'

# --- 主程序 ---

# 使用正则表达式来精确匹配数据行并提取数字
line_pattern = re.compile(r'原值(\d+)\s*→\s*新值(\d+)')

# 使用一个字典来收集每个原值对应的所有新值
data_map = defaultdict(list)

print(f"开始处理文件: {input_filename}")
start_time = time.time()

try:
    with open(input_filename, 'r', encoding='utf-8') as f:
        for i, line in enumerate(f):
            match = line_pattern.search(line)
            if match:
                original_value = int(match.group(1))
                target_value = int(match.group(2))
                data_map[original_value].append(target_value)

            if (i + 1) % 1000000 == 0:
                print(f"  已处理 {i + 1:,} 行...")

    parsing_time = time.time()
    print(f"文件解析完成。耗时: {parsing_time - start_time:.2f} 秒。")

    if not data_map:
        print("错误：在文件中没有找到任何有效的数据行。请检查文件格式。")
    else:
        print("开始聚合数据...")
        # 将字典转换为Pandas DataFrame进行统计分析
        # 为了优化内存，我们直接从字典创建聚合结果，避免生成巨大的中间DataFrame

        summary_data = []
        for original_val, target_list in data_map.items():
            # 为了计算均值和标准差，我们需要一个临时的Series
            s = pd.Series(target_list)
            summary_data.append({
                'original_value': original_val,
                'target_mean': s.mean(),
                'target_std': s.std(),
                'count': len(target_list)
            })

        summary_df = pd.DataFrame(summary_data)

        # 排序和格式化
        summary_df.sort_values('original_value', inplace=True)
        summary_df['target_mean'] = summary_df['target_mean'].round(2)
        summary_df['target_std'] = summary_df['target_std'].round(2)
        summary_df['target_std'].fillna(0, inplace=True)

        # 保存到CSV文件
        summary_df.to_csv(output_filename, index=False, encoding='utf-8-sig')

        aggregation_time = time.time()
        print(f"数据聚合完成。耗时: {aggregation_time - parsing_time:.2f} 秒。")
        print("-" * 30)
        print(f"处理成功！总耗时: {aggregation_time - start_time:.2f} 秒。")
        print(f"聚合后的摘要已保存到: {output_filename}")
        print("\n摘要数据预览:")
        print(summary_df.head())
        print("...")
        print(summary_df.tail())
        print(f"\n总共聚合了 {len(summary_df)} 个独特的原始灰度值。")


except FileNotFoundError:
    print(f"错误：找不到文件 '{input_filename}'。请确保文件路径正确。")
except Exception as e:
    print(f"处理过程中发生未知错误: {e}")

