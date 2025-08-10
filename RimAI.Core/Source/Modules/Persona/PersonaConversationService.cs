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
using RimAI.Core.Contracts.Eventing;
using Newtonsoft.Json;
using RimAI.Core.Modules.Orchestration;

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
        private readonly IPromptAssemblyService _assembler;
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
            Infrastructure.Configuration.IConfigurationService config,
            IPromptAssemblyService assembler)
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
            _assembler = assembler;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ChatAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaChatOptions options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            options ??= new PersonaChatOptions();
            var locale = options.Locale ?? _templates.ResolveLocale();
            var cfg = _config.Current;

            // 组装（使用统一 PromptAssemblyService）。若提供 personaName，则临时注入 persona:ID 以参与组装
            var ids = (participantIds ?? Array.Empty<string>()).ToList();
            if (!string.IsNullOrWhiteSpace(personaName)) ids.Add($"persona:{personaName}#0");
            var systemPrompt = await _assembler.BuildSystemPromptAsync(ids, PromptAssemblyMode.Chat, userInput, locale, ct);

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

            // 1) 校验玩家↔NPC + 人格绑定（避免在 try/catch 内 yield）
            bool hasPlayer = participantIds.Any(x => x.StartsWith("player:"));
            bool hasPawn = participantIds.Any(x => x.StartsWith("pawn:"));
            if (options.RequireBoundPersona && (!hasPlayer || !hasPawn))
            {
                yield return Result<UnifiedChatChunk>.Failure("命令对话仅允许玩家↔NPC 且需绑定人格。");
                yield break;
            }
            if (options.RequireBoundPersona)
            {
                string bindError = null;
                bool bindingOk = false;
                try
                {
                    var binder = CoreServices.Locator.Get<IPersonaBindingService>();
                    var pawnId = participantIds.FirstOrDefault(x => x.StartsWith("pawn:"));
                    var binding = string.IsNullOrWhiteSpace(pawnId) ? null : binder?.GetBinding(pawnId);
                    bindingOk = binding != null && !string.IsNullOrWhiteSpace(binding.PersonaName);
                    if (!bindingOk) bindError = "该 NPC 未绑定人格，请先在人格管理中绑定。";
                }
                catch
                {
                    bindError = "人格绑定校验失败，请稍后重试或先完成绑定。";
                }
                if (!bindingOk)
                {
                    yield return Result<UnifiedChatChunk>.Failure(bindError ?? "人格绑定校验失败。");
                    yield break;
                }
            }

            var convKey = string.Join("|", (participantIds ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal));
            var personaPrompt = _persona.Get(personaName ?? "Default")?.SystemPrompt ?? string.Empty;

            // 组装（使用统一 PromptAssemblyService）。若提供 personaName，则临时注入 persona:ID 以参与组装
            var ids = (participantIds ?? Array.Empty<string>()).ToList();
            if (!string.IsNullOrWhiteSpace(personaName)) ids.Add($"persona:{personaName}#0");
            var systemPrompt = await _assembler.BuildSystemPromptAsync(ids, PromptAssemblyMode.Command, userInput, locale, ct);

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
                    var convId = _historyWrite.CreateConversation(participantIds);
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(participantIds.FirstOrDefault(id => id.StartsWith("player:")) ?? _pid.GetPlayerId(), userInput ?? string.Empty, now));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry("assistant", final ?? string.Empty, now.AddMilliseconds(1)));
                }
            }
            catch { /* 历史写入失败不影响对话返回 */ }
        }
    }
}


