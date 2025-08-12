using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Modules.Orchestration;
using RimAI.Core.Modules.World;

namespace RimAI.Core.Modules.Orchestration.PromptOrganizers
{
    /// <summary>
    /// Chat-闲聊 场景的提示词组织者：只读采集素材，不触达网络与写入。
    /// </summary>
    internal sealed class ChatIdlePromptOrganizer : IPromptOrganizer
    {
        private readonly IPersonaService _persona;
        private readonly IBiographyService _biography;
        private readonly IPersonalBeliefsAndIdeologyService _beliefs;
        private readonly IFixedPromptService _fixedPrompts;
        private readonly IHistoryQueryService _historyQuery;
        private readonly IRecapService _recap;
        private readonly IParticipantIdService _pid;
        private readonly IWorldDataService _world;
        private readonly RimAI.Core.Modules.Prompting.IPromptTemplateService _templates;
        private readonly RimAI.Core.Infrastructure.Configuration.IConfigurationService _config;

        public ChatIdlePromptOrganizer(
            IPersonaService persona,
            IBiographyService biography,
            IPersonalBeliefsAndIdeologyService beliefs,
            IFixedPromptService fixedPrompts,
            IHistoryQueryService historyQuery,
            IRecapService recap,
            IParticipantIdService pid,
            IWorldDataService world,
            RimAI.Core.Modules.Prompting.IPromptTemplateService templates,
            RimAI.Core.Infrastructure.Configuration.IConfigurationService config)
        {
            _persona = persona;
            _biography = biography;
            _beliefs = beliefs;
            _fixedPrompts = fixedPrompts;
            _historyQuery = historyQuery;
            _recap = recap;
            _pid = pid;
            _world = world;
            _templates = templates;
            _config = config;
        }

        public string Name => "ChatIdle";

        public async Task<PromptAssemblyInput> BuildAsync(PromptContext ctx, CancellationToken ct = default)
        {
            if (ctx == null) ctx = new PromptContext();
            var locale = string.IsNullOrWhiteSpace(ctx.Locale) ? _templates.ResolveLocale() : ctx.Locale;
            var participants = (ctx.ParticipantIds ?? Array.Empty<string>()).ToList();
            var convKey = ctx.ConvKey;
            if (string.IsNullOrWhiteSpace(convKey))
            {
                convKey = string.Join("|", participants.OrderBy(x => x, StringComparer.Ordinal));
            }

            var input = new PromptAssemblyInput
            {
                Mode = PromptMode.Chat,
                Locale = locale,
                MaxPromptChars = ctx.MaxPromptChars
            };

            // Persona（默认人格）
            try { input.PersonaSystemPrompt = _persona?.Get("Default")?.SystemPrompt ?? string.Empty; } catch { }

            // Beliefs（对每个 pawn 合并到一个 BeliefsModel）
            try
            {
                foreach (var id in participants)
                {
                    if (!id.StartsWith("pawn:")) continue;
                    var b = _beliefs?.GetByPawn(id);
                    if (b == null) continue;
                    input.Beliefs ??= new BeliefsModel();
                    if (string.IsNullOrWhiteSpace(input.Beliefs.Worldview)) input.Beliefs.Worldview = b.Worldview;
                    if (string.IsNullOrWhiteSpace(input.Beliefs.Values)) input.Beliefs.Values = b.Values;
                    if (string.IsNullOrWhiteSpace(input.Beliefs.CodeOfConduct)) input.Beliefs.CodeOfConduct = b.CodeOfConduct;
                    if (string.IsNullOrWhiteSpace(input.Beliefs.TraitsText)) input.Beliefs.TraitsText = b.TraitsText;
                }
            }
            catch { }

            // Fixed prompts → Extras；会话级 Scenario 覆盖 → FixedPromptOverride
            try
            {
                foreach (var id in participants)
                {
                    if (!id.StartsWith("pawn:")) continue;
                    var text = _fixedPrompts?.GetByPawn(id);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var label = _pid?.GetDisplayName(id) ?? id;
                        input.Extras.Add("- " + label + ": " + text);
                    }
                }
                var scenarioOverride = _fixedPrompts?.GetConvKeyOverride(convKey);
                if (!string.IsNullOrWhiteSpace(scenarioOverride)) input.FixedPromptOverride = scenarioOverride;
            }
            catch { }

            // Recap（按最近会话生成的摘要字典取 K 段）
            try
            {
                var writer = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                var lastIds = await writer.FindByConvKeyAsync(convKey);
                var last = lastIds?.LastOrDefault();
                if (!string.IsNullOrWhiteSpace(last))
                {
                    var histCfg = _config?.Current?.History;
                    var k = Math.Max(1, histCfg?.RecapDictMaxEntries ?? 3);
                    var recaps = _recap?.GetRecapItems(last);
                    if (recaps != null)
                    {
                        foreach (var r in recaps.OrderByDescending(r => r.CreatedAt).Take(k).Reverse())
                        {
                            input.RecapSegments.Add(r.Text);
                        }
                    }
                }
            }
            catch { }

            // Main recent history（近 N 条）
            try
            {
                var ctxHist = await _historyQuery.GetHistoryAsync(participants);
                foreach (var e in ctxHist.MainHistory.SelectMany(c => c.Entries).OrderByDescending(e => e.Timestamp).Take(6).OrderBy(e => e.Timestamp))
                {
                    input.HistorySnippets.Add("- " + e.SpeakerId + ": " + (e.Content ?? string.Empty));
                }
            }
            catch { }

            // World facts（最小示例）
            try
            {
                var playerName = await _world.GetPlayerNameAsync();
                if (!string.IsNullOrWhiteSpace(playerName))
                {
                    input.WorldFacts.Add("- 玩家派系：" + playerName);
                }
            }
            catch { }

            return input;
        }
    }
}


