using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.History;
using RimAI.Core.Services;
using RimAI.Core.Modules.Prompting;
using RimAI.Core.Modules.World;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Persona
{
    internal sealed class PersonaChatOptions { public string Locale = null; public bool Stream = true; public bool WriteHistory = false; }
    internal sealed class PersonaCommandOptions { public string Locale = null; public bool Stream = true; public bool RequireBoundPersona = true; public bool WriteHistory = true; }

    internal interface IPersonaConversationService
    {
        IAsyncEnumerable<Result<UnifiedChatChunk>> ChatAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaChatOptions options = null, CancellationToken ct = default);
        IAsyncEnumerable<Result<UnifiedChatChunk>> CommandAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaCommandOptions options = null, CancellationToken ct = default);
    }

    internal sealed class PersonaConversationService : IPersonaConversationService
    {
        private readonly IPersonaService _persona;
        private readonly Modules.LLM.ILLMService _llm;
        private readonly IPromptTemplateService _templates;
        private readonly IPromptComposer _composer;
        private readonly IHistoryWriteService _historyWrite;
        private readonly IHistoryQueryService _historyQuery;
        private readonly IRecapService _recap;
        private readonly IParticipantIdService _pid;
        private readonly Infrastructure.Configuration.IConfigurationService _config;

        public PersonaConversationService(IPersonaService persona,
            Modules.LLM.ILLMService llm,
            IPromptTemplateService templates,
            IPromptComposer composer,
            IHistoryWriteService historyWrite,
            IHistoryQueryService historyQuery,
            IRecapService recap,
            IParticipantIdService pid,
            Infrastructure.Configuration.IConfigurationService config)
        {
            _persona = persona;
            _llm = llm;
            _templates = templates;
            _composer = composer;
            _historyWrite = historyWrite;
            _historyQuery = historyQuery;
            _recap = recap;
            _pid = pid;
            _config = config;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ChatAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaChatOptions options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            options ??= new PersonaChatOptions();
            var locale = options.Locale ?? _templates.ResolveLocale();
            var cfg = _config.Current;

            // 1) 收集素材（轻量）
            var convKey = string.Join("|", (participantIds ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal));
            var personaPrompt = _persona.Get(personaName ?? "Default")?.SystemPrompt ?? string.Empty;
            var fixedMap = CoreServices.Locator.Get<IFixedPromptService>().GetAll(convKey);
            var recapItems = _recap.GetRecapItems(convKey).OrderByDescending(x => x.CreatedAt).Take(Math.Max(1, cfg.History?.RecapDictMaxEntries ?? 5)).Select(r => r.Text);
            var recent = await _historyQuery.GetHistoryAsync(participantIds);
            var recentLines = recent.MainHistory.SelectMany(c => c.Entries).OrderByDescending(e => e.Timestamp).Take(Math.Max(1, cfg.Prompt?.Segments?.Chat?.RecentHistoryMaxEntries ?? 6)).OrderBy(e => e.Timestamp).Select(e => $"- {e.SpeakerId}: {e.Content}");

            // 2) 组装（chat 模板）
            _composer.Begin(cfg.Prompt?.TemplateChatKey ?? "chat", locale);
            if (!string.IsNullOrWhiteSpace(personaPrompt)) _composer.Add("persona", personaPrompt);
            foreach (var kv in fixedMap) _composer.Add("fixed_prompts", $"- {_pid.GetDisplayName(kv.Key)}: {kv.Value}");
            _composer.AddRange("recap", recapItems);
            _composer.AddRange("recent_history", recentLines);
            _composer.Add("user_utterance", userInput ?? string.Empty);

            var systemPrompt = _composer.Build(Math.Max(1000, cfg.History?.MaxPromptChars ?? 4000), out var audit);

            var req = new UnifiedChatRequest { Stream = options.Stream, Messages = new List<ChatMessage> { new ChatMessage { Role = "system", Content = systemPrompt }, new ChatMessage { Role = "user", Content = userInput ?? string.Empty } } };
            if (options.Stream)
            {
                await foreach (var chunk in _llm.StreamResponseAsync(req)) yield return chunk;
            }
            else
            {
                var res = await _llm.GetResponseAsync(req);
                if (res.IsSuccess)
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = res.Value?.Message?.Content });
                else
                    yield return Result<UnifiedChatChunk>.Failure(res.Error);
            }
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> CommandAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaCommandOptions options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            options ??= new PersonaCommandOptions();
            var locale = options.Locale ?? _templates.ResolveLocale();
            var cfg = _config.Current;

            // 1) 校验玩家↔NPC + 人格绑定
            bool hasPlayer = participantIds.Any(x => x.StartsWith("player:"));
            bool hasPawn = participantIds.Any(x => x.StartsWith("pawn:"));
            if (options.RequireBoundPersona && (!hasPlayer || !hasPawn))
            {
                yield return Result<UnifiedChatChunk>.Failure("命令对话仅允许玩家↔NPC 且需绑定人格。");
                yield break;
            }

            var convKey = string.Join("|", (participantIds ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal));
            var personaPrompt = _persona.Get(personaName ?? "Default")?.SystemPrompt ?? string.Empty;

            // 2) 素材（较重）
            var fixedMap = CoreServices.Locator.Get<IFixedPromptService>().GetAll(convKey);
            var recapItems = _recap.GetRecapItems(convKey).OrderByDescending(x => x.CreatedAt).Take(Math.Max(1, cfg.History?.RecapDictMaxEntries ?? 5)).Select(r => r.Text);
            var recent = await _historyQuery.GetHistoryAsync(participantIds);
            var relatedConvs = recent.BackgroundHistory; // 简化：以 BackgroundHistory 近似“相关历史”
            int perConv = Math.Max(1, cfg.Prompt?.Segments?.Command?.RelatedMaxEntriesPerConversation ?? 5);
            var relatedLines = relatedConvs.SelectMany(c => c.Entries.OrderByDescending(e => e.Timestamp).Take(perConv).OrderBy(e => e.Timestamp)).Select(e => $"- {e.SpeakerId}: {e.Content}");

            // 3) 组装（command 模板）
            _composer.Begin(cfg.Prompt?.TemplateCommandKey ?? "command", locale);
            if (!string.IsNullOrWhiteSpace(personaPrompt)) _composer.Add("persona", personaPrompt);
            foreach (var kv in fixedMap) _composer.Add("fixed_prompts", $"- {_pid.GetDisplayName(kv.Key)}: {kv.Value}");
            if (participantIds.Count == 2 && hasPlayer && hasPawn)
            {
                var pawnId = participantIds.First(x => x.StartsWith("pawn:"));
                var bio = CoreServices.Locator.Get<IBiographyService>().List(convKey).OrderBy(b => b.CreatedAt).Select(b => "- " + b.Text);
                _composer.AddRange("biography", bio);
            }
            _composer.AddRange("recap", recapItems);
            _composer.AddRange("related_history", relatedLines);
            _composer.Add("user_utterance", userInput ?? string.Empty);

            var systemPrompt = _composer.Build(Math.Max(1000, cfg.History?.MaxPromptChars ?? 4000), out var audit);

            // 4) 走编排（五步），此处复用 OrchestrationService 外部接口
            var orchestrator = CoreServices.Locator.Get<RimAI.Core.Contracts.IOrchestrationService>();
            var stream = orchestrator.ExecuteToolAssistedQueryAsync(userInput, systemPrompt);
            string final = string.Empty;
            await foreach (var chunk in stream)
            {
                if (chunk.IsSuccess)
                {
                    final += chunk.Value?.ContentDelta ?? string.Empty;
                }
                yield return chunk;
            }
            try
            {
                if (options.WriteHistory)
                {
                    var now = DateTime.UtcNow;
                    await _historyWrite.RecordEntryAsync(participantIds, new ConversationEntry(participantIds.FirstOrDefault(id => id.StartsWith("player:")) ?? "player:__SAVE__", userInput ?? string.Empty, now));
                    await _historyWrite.RecordEntryAsync(participantIds, new ConversationEntry("assistant", final ?? string.Empty, now.AddMilliseconds(1)));
                }
            }
            catch { /* 历史写入失败不影响对话返回 */ }
        }
    }
}


