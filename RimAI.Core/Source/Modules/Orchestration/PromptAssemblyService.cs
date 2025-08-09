using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Modules.World;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.History;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Settings;
using InfraConfig = RimAI.Core.Infrastructure.Configuration.IConfigurationService;
using CoreSvc = RimAI.Core.Infrastructure.CoreServices;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 提示词组装服务的最小占位实现（M1）。
    /// 暂时返回空字符串；后续阶段将注入固定提示/人物传记段落/前情提要/历史片段。
    /// </summary>
    internal sealed class PromptAssemblyService : IPromptAssemblyService
    {
        private readonly IParticipantIdService _pid;
        private readonly IFixedPromptService _fixedPrompts;
        private readonly IBiographyService _bio;
        private readonly IRecapService _recap;
        private readonly IHistoryQueryService _historyQuery;
        private readonly InfraConfig _config;

        public PromptAssemblyService(IParticipantIdService pid,
                                     IFixedPromptService fixedPrompts,
                                     IBiographyService bio,
                                     IRecapService recap,
                                     IHistoryQueryService historyQuery,
                                     InfraConfig config)
        {
            _pid = pid;
            _fixedPrompts = fixedPrompts;
            _bio = bio;
            _recap = recap;
            _historyQuery = historyQuery;
            _config = config;
        }

        public Task<string> BuildSystemPromptAsync(IReadOnlyCollection<string> participantIds, CancellationToken ct = default)
        {
            if (participantIds == null || participantIds.Count == 0)
                return Task.FromResult(string.Empty);

            var convKey = string.Join("|", participantIds.OrderBy(x => x, StringComparer.Ordinal));
            var cfg = _config?.Current?.History ?? new HistoryConfig();

            var sb = new StringBuilder(1024);

            // 1) 可选：人格（若参与者中包含 persona:<name>#<rev>）
            var personaId = participantIds.FirstOrDefault(x => x.StartsWith("persona:", StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(personaId))
            {
                try
                {
                    var tail = personaId.Substring("persona:".Length);
                    var name = tail.Contains('#') ? tail.Split('#')[0] : tail;
                    var ps = CoreSvc.Locator.Get<IPersonaService>();
                    var p = ps?.Get(name);
                    if (p != null && !string.IsNullOrWhiteSpace(p.SystemPrompt))
                    {
                        sb.AppendLine("[Persona]");
                        sb.AppendLine(p.SystemPrompt.Trim());
                        sb.AppendLine();
                    }
                }
                catch { /* ignore persona errors */ }
            }

            bool isPlayerNpc = participantIds.Any(x => x.StartsWith("player:", StringComparison.Ordinal)) &&
                               participantIds.Any(x => x.StartsWith("pawn:", StringComparison.Ordinal));

            // 2) 固定提示词（仅在玩家 v NPC 场景注入）
            if (isPlayerNpc)
            {
                var map = _fixedPrompts.GetAll(convKey);
                if (map != null && map.Count > 0)
                {
                    sb.AppendLine("[FixedPrompts]");
                    foreach (var pid in participantIds)
                    {
                        if (map.TryGetValue(pid, out var text) && !string.IsNullOrWhiteSpace(text))
                        {
                            var name = _pid.GetDisplayName(pid);
                            sb.Append("- ").Append(name).Append(": ").AppendLine(text.Trim());
                        }
                    }
                    sb.AppendLine();
                }
            }

            // 3) 人物传记（仅 1v1 player↔pawn）
            if (participantIds.Count == 2 && isPlayerNpc)
            {
                var bios = _bio.List(convKey);
                if (bios != null && bios.Count > 0)
                {
                    sb.AppendLine("[Biography]");
                    foreach (var it in bios.OrderBy(b => b.CreatedAt))
                    {
                        sb.Append("- ").AppendLine(it.Text?.Trim() ?? string.Empty);
                    }
                    sb.AppendLine();
                }
            }

            // 4) 前情提要字典（按时间倒序，取最近 K 条）
            var recaps = _recap.GetRecapItems(convKey);
            if (recaps != null && recaps.Count > 0)
            {
                int k = Math.Max(1, cfg.RecapDictMaxEntries);
                var take = recaps.OrderByDescending(r => r.CreatedAt).Take(k).Reverse();
                sb.AppendLine("[Recap]");
                foreach (var r in take)
                {
                    sb.Append("- ").AppendLine(r.Text?.Trim() ?? string.Empty);
                }
                sb.AppendLine();
            }

            // 5) 相关历史最终输出片段（主线最近 N 条）
            try
            {
                var ctxTask = _historyQuery.GetHistoryAsync(participantIds.ToList());
                ctxTask.Wait(ct);
                var ctx = ctxTask.Result;
                var lastEntries = ctx.MainHistory
                    .SelectMany(c => c.Entries)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(10)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
                if (lastEntries.Count > 0)
                {
                    sb.AppendLine("[RecentHistory]");
                    foreach (var e in lastEntries)
                    {
                        sb.Append("- ").Append(e.SpeakerId).Append(": ").AppendLine(e.Content);
                    }
                    sb.AppendLine();
                }
            }
            catch { /* ignore history errors */ }

            // 裁剪到预算
            var textOut = sb.ToString();
            int budget = Math.Max(1000, cfg.MaxPromptChars);
            if (textOut.Length > budget)
            {
                textOut = textOut.Substring(0, budget);
            }
            return Task.FromResult(textOut);
        }
    }
}


