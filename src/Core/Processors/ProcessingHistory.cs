using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace ImageAnalysisTool.Core.Processors
{
    /// <summary>
    /// 处理历史管理器 - 管理撤销/重做操作
    /// </summary>
    public class ProcessingHistory
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        
        private readonly Stack<ProcessingRule> undoStack;
        private readonly Stack<ProcessingRule> redoStack;
        private readonly List<ProcessingRule> allHistory;
        private readonly int maxHistorySize;

        /// <summary>
        /// 当前历史记录数量
        /// </summary>
        public int Count => allHistory.Count;

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => undoStack.Count > 0;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => redoStack.Count > 0;

        /// <summary>
        /// 历史记录变化事件
        /// </summary>
        public event EventHandler HistoryChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="maxHistorySize">最大历史记录数量</param>
        public ProcessingHistory(int maxHistorySize = 100)
        {
            this.maxHistorySize = maxHistorySize;
            undoStack = new Stack<ProcessingRule>();
            redoStack = new Stack<ProcessingRule>();
            allHistory = new List<ProcessingRule>();
        }

        /// <summary>
        /// 添加处理步骤
        /// </summary>
        /// <param name="rule">处理规则</param>
        public void AddStep(ProcessingRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            try
            {
                // 添加到撤销栈
                undoStack.Push(rule);
                
                // 清空重做栈（新操作会使重做无效）
                redoStack.Clear();
                
                // 添加到完整历史记录
                allHistory.Add(rule);
                
                // 限制历史记录大小
                while (allHistory.Count > maxHistorySize)
                {
                    allHistory.RemoveAt(0);
                }
                
                // 限制撤销栈大小
                if (undoStack.Count > maxHistorySize)
                {
                    var tempList = undoStack.ToList();
                    undoStack.Clear();
                    for (int i = tempList.Count - maxHistorySize; i < tempList.Count; i++)
                    {
                        undoStack.Push(tempList[i]);
                    }
                }

                logger.Debug($"添加处理步骤: {rule.RuleName}, 当前历史数量: {Count}");
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"添加处理步骤失败: {rule.RuleName}");
                throw;
            }
        }

        /// <summary>
        /// 撤销操作
        /// </summary>
        /// <returns>被撤销的处理规则，如果无法撤销则返回null</returns>
        public ProcessingRule Undo()
        {
            if (!CanUndo)
            {
                logger.Warn("无法撤销：撤销栈为空");
                return null;
            }

            try
            {
                var rule = undoStack.Pop();
                redoStack.Push(rule);
                
                logger.Debug($"撤销操作: {rule.RuleName}");
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke(this, EventArgs.Empty);
                
                return rule;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "撤销操作失败");
                throw;
            }
        }

        /// <summary>
        /// 重做操作
        /// </summary>
        /// <returns>被重做的处理规则，如果无法重做则返回null</returns>
        public ProcessingRule Redo()
        {
            if (!CanRedo)
            {
                logger.Warn("无法重做：重做栈为空");
                return null;
            }

            try
            {
                var rule = redoStack.Pop();
                undoStack.Push(rule);
                
                logger.Debug($"重做操作: {rule.RuleName}");
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke(this, EventArgs.Empty);
                
                return rule;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "重做操作失败");
                throw;
            }
        }

        /// <summary>
        /// 获取历史记录列表（按时间顺序）
        /// </summary>
        /// <returns>历史记录列表</returns>
        public List<ProcessingRule> GetHistory()
        {
            return new List<ProcessingRule>(allHistory);
        }

        /// <summary>
        /// 获取最近的N个历史记录
        /// </summary>
        /// <param name="count">记录数量</param>
        /// <returns>最近的历史记录</returns>
        public List<ProcessingRule> GetRecentHistory(int count)
        {
            if (count <= 0) return new List<ProcessingRule>();
            
            int startIndex = Math.Max(0, allHistory.Count - count);
            return allHistory.Skip(startIndex).ToList();
        }

        /// <summary>
        /// 获取撤销栈中的规则列表（最新的在前）
        /// </summary>
        /// <returns>撤销栈规则列表</returns>
        public List<ProcessingRule> GetUndoStack()
        {
            return undoStack.ToList();
        }

        /// <summary>
        /// 获取重做栈中的规则列表（最新的在前）
        /// </summary>
        /// <returns>重做栈规则列表</returns>
        public List<ProcessingRule> GetRedoStack()
        {
            return redoStack.ToList();
        }

        /// <summary>
        /// 清空所有历史记录
        /// </summary>
        public void Clear()
        {
            try
            {
                undoStack.Clear();
                redoStack.Clear();
                allHistory.Clear();
                
                logger.Info("清空所有历史记录");
                
                // 触发历史记录变化事件
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "清空历史记录失败");
                throw;
            }
        }

        /// <summary>
        /// 获取历史记录摘要
        /// </summary>
        /// <returns>历史记录摘要字符串</returns>
        public string GetHistorySummary()
        {
            var summary = $"处理历史摘要 (总计: {Count} 步)\n";
            summary += $"可撤销: {undoStack.Count} 步\n";
            summary += $"可重做: {redoStack.Count} 步\n\n";

            if (allHistory.Count > 0)
            {
                summary += "最近的处理步骤:\n";
                var recentSteps = GetRecentHistory(10);
                for (int i = 0; i < recentSteps.Count; i++)
                {
                    var rule = recentSteps[i];
                    summary += $"{i + 1}. {rule.RuleName} ({rule.Type}) - {rule.CreateTime:HH:mm:ss}\n";
                }
            }
            else
            {
                summary += "暂无处理历史记录\n";
            }

            return summary;
        }

        /// <summary>
        /// 根据规则ID查找历史记录
        /// </summary>
        /// <param name="ruleId">规则ID</param>
        /// <returns>找到的规则，如果不存在则返回null</returns>
        public ProcessingRule FindRuleById(string ruleId)
        {
            return allHistory.FirstOrDefault(r => r.Id == ruleId);
        }

        /// <summary>
        /// 根据规则名称查找历史记录
        /// </summary>
        /// <param name="ruleName">规则名称</param>
        /// <returns>找到的规则列表</returns>
        public List<ProcessingRule> FindRulesByName(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName))
                return new List<ProcessingRule>();

            return allHistory.Where(r => r.RuleName.Contains(ruleName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// 获取处理类型统计
        /// </summary>
        /// <returns>处理类型统计字典</returns>
        public Dictionary<ProcessingType, int> GetTypeStatistics()
        {
            return allHistory.GroupBy(r => r.Type)
                           .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// 导出历史记录为文本
        /// </summary>
        /// <returns>历史记录文本</returns>
        public string ExportHistoryAsText()
        {
            var export = "=== 图像处理历史记录导出 ===\n\n";
            export += $"导出时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            export += $"总记录数: {Count}\n\n";

            for (int i = 0; i < allHistory.Count; i++)
            {
                export += $"--- 步骤 {i + 1} ---\n";
                export += allHistory[i].GetDetailedInfo();
                export += "\n";
            }

            // 添加统计信息
            export += "=== 统计信息 ===\n";
            var typeStats = GetTypeStatistics();
            foreach (var stat in typeStats)
            {
                export += $"{stat.Key}: {stat.Value} 次\n";
            }

            return export;
        }
    }
}
