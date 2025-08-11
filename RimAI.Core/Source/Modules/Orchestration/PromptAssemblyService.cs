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
using RimAI.Core.Contracts.Eventing;
using Newtonsoft.Json;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 提示词组装服务（M4）。统一基于模板与 Composer 组装 Chat/Command 的 system 提示。
    /// </summary>
    internal sealed class PromptAssemblyService : IPromptAssemblyService
    {
        private readonly IParticipantIdService _pid;
        private readonly IFixedPromptService _fixedPrompts;
        private readonly IBiographyService _bio;
        private readonly RimAI.Core.Modules.Persona.IPersonalBeliefsAndIdeologyService _beliefs;
        private readonly IRecapService _recap;
        private readonly IHistoryQueryService _historyQuery;
        private readonly InfraConfig _config;
        private readonly RimAI.Core.Modules.Prompting.IPromptComposer _composer;
        private readonly RimAI.Core.Modules.Prompting.IPromptTemplateService _templates;

        public PromptAssemblyService(IParticipantIdService pid,
                                     IFixedPromptService fixedPrompts,
                                      IBiographyService bio,
                                      RimAI.Core.Modules.Persona.IPersonalBeliefsAndIdeologyService beliefs,
                                     IRecapService recap,
                                     IHistoryQueryService historyQuery,
                                     InfraConfig config,
                                     RimAI.Core.Modules.Prompting.IPromptComposer composer,
                                     RimAI.Core.Modules.Prompting.IPromptTemplateService templates)
        {
            _pid = pid;
            _fixedPrompts = fixedPrompts;
            _bio = bio;
            _beliefs = beliefs;
            _recap = recap;
            _historyQuery = historyQuery;
            _config = config;
            _composer = composer;
            _templates = templates;
        }

        public async Task<string> BuildSystemPromptAsync(IReadOnlyCollection<string> participantIds, PromptAssemblyMode mode, string userInput, string locale = null, CancellationToken ct = default)
        {
            if (participantIds == null || participantIds.Count == 0)
                return string.Empty;

            var convKey = string.Join("|", participantIds.OrderBy(x => x, StringComparer.Ordinal));
            var cfg = _config?.Current;
            var histCfg = cfg?.History ?? new HistoryConfig();
            var localeToUse = locale ?? _templates.ResolveLocale();

            // Persona（若参与者包含 persona:）
            string personaPrompt = string.Empty;
            var personaId = participantIds.FirstOrDefault(x => x.StartsWith("persona:", StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(personaId))
            {
                try
                {
                    var tail = personaId.Substring("persona:".Length);
                    var name = tail.Contains('#') ? tail.Split('#')[0] : tail;
                    var ps = CoreSvc.Locator.Get<IPersonaService>();
                    personaPrompt = ps?.Get(name)?.SystemPrompt ?? string.Empty;
                }
                catch { }
            }

            bool isPlayerNpc = participantIds.Any(x => x.StartsWith("player:", StringComparison.Ordinal)) && participantIds.Any(x => x.StartsWith("pawn:", StringComparison.Ordinal));
            if (mode == PromptAssemblyMode.Chat)
            {
                var chatSeg = cfg?.Prompt?.Segments?.Chat;
                _composer.Begin(cfg?.Prompt?.TemplateChatKey ?? "chat", localeToUse);
                // 注入个性化观点（合并到 persona 段，避免模板缺失）
                if (chatSeg?.IncludePersona ?? true)
                {
                    foreach (var pid in participantIds)
                    {
                        if (!pid.StartsWith("pawn:", StringComparison.Ordinal)) continue;
                        try
                        {
                            var b = _beliefs?.GetByPawn(pid);
                            if (b != null)
                            {
                                var beliefText = BuildBeliefsBlock(_pid.GetDisplayName(pid), b);
                                if (!string.IsNullOrWhiteSpace(beliefText)) _composer.Add("persona", beliefText);
                            }
                        }
                        catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(personaPrompt)) _composer.Add("persona", personaPrompt);
                }

                if (chatSeg?.IncludeFixedPrompts ?? true)
                {
                    foreach (var pid in participantIds)
                    {
                        if (!pid.StartsWith("pawn:", StringComparison.Ordinal)) continue;
                        var text = _fixedPrompts.GetByPawn(pid);
                        if (!string.IsNullOrWhiteSpace(text)) _composer.Add("fixed_prompts", $"- {_pid.GetDisplayName(pid)}: {text}");
                    }
                }
                if (chatSeg?.IncludeRecap ?? true)
                {
                    IReadOnlyList<RecapSnapshotItem> recaps = Array.Empty<RecapSnapshotItem>();
                    try
                    {
                        var writer = CoreSvc.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                        var list = await writer.FindByConvKeyAsync(convKey);
                        var cid = list?.LastOrDefault();
                        if (!string.IsNullOrWhiteSpace(cid)) recaps = _recap.GetRecapItems(cid);
                    }
                    catch { }
                    if (recaps != null && recaps.Count > 0)
                    {
                        int k = Math.Max(1, histCfg.RecapDictMaxEntries);
                        var lines = recaps.OrderByDescending(r => r.CreatedAt).Take(k).Reverse().Select(r => r.Text);
                        _composer.AddRange("recap", lines);
                    }
                }
                if (chatSeg?.IncludeRecentHistory ?? true)
                {
                    var ctx = await _historyQuery.GetHistoryAsync(participantIds.ToList());
                    var lines = ctx.MainHistory.SelectMany(c => c.Entries).OrderByDescending(e => e.Timestamp).Take(Math.Max(1, chatSeg?.RecentHistoryMaxEntries ?? 6)).OrderBy(e => e.Timestamp).Select(e => $"- {e.SpeakerId}: {e.Content}");
                    _composer.AddRange("recent_history", lines);
                }
                _composer.Add("user_utterance", userInput ?? string.Empty);
                var output = _composer.Build(Math.Max(1000, histCfg.MaxPromptChars), out var audit);
                try
                {
                    CoreSvc.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                    {
                        Source = nameof(PromptAssemblyService),
                        Stage = "PromptAudit",
                        Message = "Chat",
                        PayloadJson = JsonConvert.SerializeObject(audit)
                    });
                }
                catch { }
                return output;
            }
            else
            {
                var cmdSeg = cfg?.Prompt?.Segments?.Command;
                _composer.Begin(cfg?.Prompt?.TemplateCommandKey ?? "command", localeToUse);
                if (cmdSeg?.IncludePersona ?? true)
                {
                    foreach (var pid in participantIds)
                    {
                        if (!pid.StartsWith("pawn:", StringComparison.Ordinal)) continue;
                        try
                        {
                            var b = _beliefs?.GetByPawn(pid);
                            if (b != null)
                            {
                                var beliefText = BuildBeliefsBlock(_pid.GetDisplayName(pid), b);
                                if (!string.IsNullOrWhiteSpace(beliefText)) _composer.Add("persona", beliefText);
                            }
                        }
                        catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(personaPrompt)) _composer.Add("persona", personaPrompt);
                }
                if (cmdSeg?.IncludeFixedPrompts ?? true)
                {
                    foreach (var pid in participantIds)
                    {
                        if (!pid.StartsWith("pawn:", StringComparison.Ordinal)) continue;
                        var text = _fixedPrompts.GetByPawn(pid);
                        if (!string.IsNullOrWhiteSpace(text)) _composer.Add("fixed_prompts", $"- {_pid.GetDisplayName(pid)}: {text}");
                    }
                }
                if ((cmdSeg?.IncludeBiography ?? true) && participantIds.Count == 2 && isPlayerNpc)
                {
                    var pawnId = participantIds.First(x => x.StartsWith("pawn:", StringComparison.Ordinal));
                    var bio = _bio.ListByPawn(pawnId).OrderBy(b => b.CreatedAt).Select(b => "- " + b.Text);
                    _composer.AddRange("biography", bio);
                }
                if (cmdSeg?.IncludeRecap ?? true)
                {
                    IReadOnlyList<RecapSnapshotItem> recaps = Array.Empty<RecapSnapshotItem>();
                    try
                    {
                        var writer = CoreSvc.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                        var list = await writer.FindByConvKeyAsync(convKey);
                        var cid = list?.LastOrDefault();
                        if (!string.IsNullOrWhiteSpace(cid)) recaps = _recap.GetRecapItems(cid);
                    }
                    catch { }
                    if (recaps != null && recaps.Count > 0)
                    {
                        int k = Math.Max(1, histCfg.RecapDictMaxEntries);
                        var lines = recaps.OrderByDescending(r => r.CreatedAt).Take(k).Reverse().Select(r => r.Text);
                        _composer.AddRange("recap", lines);
                    }
                }
                if (cmdSeg?.IncludeRelatedHistory ?? true)
                {
                    var ctx = await _historyQuery.GetHistoryAsync(participantIds.ToList());
                    int perConv = Math.Max(1, cmdSeg?.RelatedMaxEntriesPerConversation ?? 5);
                    var relatedLines = ctx.BackgroundHistory.SelectMany(c => c.Entries.OrderByDescending(e => e.Timestamp).Take(perConv).OrderBy(e => e.Timestamp)).Select(e => $"- {e.SpeakerId}: {e.Content}");
                    _composer.AddRange("related_history", relatedLines);
                }
                _composer.Add("user_utterance", userInput ?? string.Empty);
                var output = _composer.Build(Math.Max(1000, histCfg.MaxPromptChars), out var audit);
                try
                {
                    CoreSvc.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                    {
                        Source = nameof(PromptAssemblyService),
                        Stage = "PromptAudit",
                        Message = "Command",
                        PayloadJson = JsonConvert.SerializeObject(audit)
                    });
                }
                catch { }
                return output;
            }
        }

        private static string BuildBeliefsBlock(string displayName, RimAI.Core.Modules.Persona.PersonalBeliefs b)
        {
            try
            {
                var name = string.IsNullOrWhiteSpace(displayName) ? "该角色" : displayName;
                var lines = new List<string>(8);
                if (!string.IsNullOrWhiteSpace(b.Worldview)) lines.Add($"- {name}的世界观: {b.Worldview}");
                if (!string.IsNullOrWhiteSpace(b.Values)) lines.Add($"- {name}的价值观: {b.Values}");
                if (!string.IsNullOrWhiteSpace(b.CodeOfConduct)) lines.Add($"- {name}的行为准则: {b.CodeOfConduct}");
                if (!string.IsNullOrWhiteSpace(b.TraitsText)) lines.Add($"- {name}的人格特质: {b.TraitsText}");
                return string.Join("\n", lines);
            }
            catch { return string.Empty; }
        }
    }
}


