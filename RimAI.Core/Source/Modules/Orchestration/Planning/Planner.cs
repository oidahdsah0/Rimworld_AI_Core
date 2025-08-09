using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Infrastructure;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Orchestration.Planning
{
    /// <summary>
    /// 轻量“串联递归”规划器（最小实现）。
    /// 默认串行，不引入并发，仅对已有上下文进行要点整合，产出 FinalPrompt。
    /// </summary>
    internal sealed class Planner
    {
        public async Task<FinalPromptResult> BuildFinalPromptAsync(string userQuery, string personaPrompt, IEnumerable<string> ragSnippets,
            IEnumerable<string> toolSummaries, int maxChars, IPlanProgressReporter progress = null)
        {
            await Task.CompletedTask;
            var blackboard = new PlanBlackboard();
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(personaPrompt))
            {
                sb.AppendLine(personaPrompt.Trim());
            }

            // 汇总 RAG 片段
            if (ragSnippets != null)
            {
                var list = ragSnippets.Where(s => !string.IsNullOrWhiteSpace(s)).Take(5).ToList();
                if (list.Count > 0)
                {
                    progress?.Report(new PlanProgressUpdate { Source = nameof(Planner), Stage = "RAG", Message = $"命中 {list.Count} 条上下文", Payload = list });
                    sb.AppendLine();
                    sb.AppendLine("[检索上下文要点]");
                    foreach (var s in list)
                    {
                        var line = Truncate(s.Trim(), 200);
                        blackboard.KeyFindings.Add(line);
                        sb.AppendLine("- " + line);
                    }
                }
            }

            // 工具结果要点（若有）
            if (toolSummaries != null)
            {
                var list = toolSummaries.Where(s => !string.IsNullOrWhiteSpace(s)).Take(5).ToList();
                if (list.Count > 0)
                {
                    progress?.Report(new PlanProgressUpdate { Source = nameof(Planner), Stage = "ToolSummaries", Message = $"纳入 {list.Count} 条工具要点", Payload = list });
                    sb.AppendLine();
                    sb.AppendLine("[工具结果要点]");
                    foreach (var s in list)
                    {
                        var line = Truncate(s.Trim(), 200);
                        blackboard.KeyFindings.Add(line);
                        sb.AppendLine("- " + line);
                    }
                }
            }

            // 限长
            var merged = sb.ToString();
            if (maxChars > 0 && merged.Length > maxChars)
            {
                merged = merged.Substring(0, maxChars);
            }

            var result = new FinalPromptResult
            {
                FinalPrompt = merged,
                Blackboard = blackboard
            };

            progress?.Report(new PlanProgressUpdate { Source = nameof(Planner), Stage = "FinalPrompt", Message = $"生成 final_prompt（{merged.Length} 字符）", Payload = null });
            return result;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "…";
        }
    }
}


