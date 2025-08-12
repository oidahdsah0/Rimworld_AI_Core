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
        
        private readonly IParticipantIdService _pid;
        private readonly Infrastructure.Configuration.IConfigurationService _config;

        public PersonaConversationService(IPersonaService persona,
            Modules.LLM.ILLMService llm,
            IPromptTemplateService templates,
            IPromptComposer composer,
            IParticipantIdService pid,
            Infrastructure.Configuration.IConfigurationService config,
            IPromptAssemblyService assembler)
        {
            _persona = persona;
            _llm = llm;
            _templates = templates;
            _composer = composer;
            _pid = pid;
            _config = config;
            _assembler = assembler;
        }

        private static string ComputeShortHash(string input)
        {
            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
                    var hash = sha1.ComputeHash(bytes);
                    var sb = new System.Text.StringBuilder(20);
                    for (int i = 0; i < Math.Min(hash.Length, 10); i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "0000000000"; }
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ChatAsync(IReadOnlyList<string> participantIds, string personaName, string userInput, PersonaChatOptions options = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            options ??= new PersonaChatOptions();
            var locale = options.Locale ?? _templates.ResolveLocale();
            var cfg = _config.Current;

            // 组装（使用统一 PromptAssemblyService）。若提供 personaName，则临时注入 persona:ID 以参与组装
            var ids = (participantIds ?? Array.Empty<string>()).ToList();
            if (!string.IsNullOrWhiteSpace(personaName)) ids.Add($"persona:{personaName}#0");
            // 过渡实现：仍在会话服务内拉取素材并构造输入（D2/D3 将迁至 Organizer）
            var input = await BuildPromptInputForChatAsync(ids, userInput, locale, ct);
            var systemPrompt = await _assembler.ComposeSystemPromptAsync(input, ct);

            var req = new UnifiedChatRequest { Stream = options.Stream, Messages = new List<ChatMessage> { new ChatMessage { Role = "system", Content = systemPrompt }, new ChatMessage { Role = "user", Content = userInput ?? string.Empty } } };
            // 使用参与者集合形成稳定 convKey，再派生对外 ConversationId
            try
            {
                var convKey = string.Join("|", ids.OrderBy(x => x, StringComparer.Ordinal));
                req.ConversationId = $"chat:{ComputeShortHash(convKey)}";
            }
            catch { /* 保底由 LLMService 补充 */ }
            if (options.Stream)
            {
                await foreach (var chunk in _llm.StreamResponseAsync(req, ct)) yield return chunk;
            }
            else
            {
                var res = await _llm.GetResponseAsync(req);
                if (res.IsSuccess)
                {
                    var final = res.Value?.Message?.Content ?? string.Empty;
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = final });
                }
                else
                {
                    yield return Result<UnifiedChatChunk>.Failure(res.Error);
                }
            }
        }

        internal sealed class StageChatOverrides
        {
            public int? MaxOutputTokens = null; // 近似截断
            public int? MaxOutputChars = null;  // 硬字符上限（优先生效）
            public string Model = null;         // 预留：后续可穿透到 Provider
            public double? Temperature = null;  // 预留
            public System.Collections.Generic.Dictionary<string, object> ProviderParameters = null; // 预留
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ChatForStageAsync(
            IReadOnlyList<string> participantIds,
            string personaName,
            string userInput,
            string locale,
            StageChatOverrides overrides = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            overrides ??= new StageChatOverrides();
            // 统一走非流式，便于在服务端收敛并截断
            string final = string.Empty;
            string error = null;
            await foreach (var chunk in ChatAsync(participantIds, personaName, userInput, new PersonaChatOptions { Stream = false, WriteHistory = false, Locale = locale }, ct))
            {
                if (ct.IsCancellationRequested) yield break;
                if (chunk.IsSuccess)
                {
                    final += chunk.Value?.ContentDelta ?? string.Empty;
                }
                else
                {
                    error = chunk.Error;
                }
            }
            if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(final))
            {
                yield return Result<UnifiedChatChunk>.Failure(error);
                yield break;
            }
            // 应用覆盖（不影响常规 ChatAsync 调用）
            if (overrides?.MaxOutputChars is int maxChars && maxChars > 0)
            {
                final = TruncateByChars(final, maxChars);
            }
            else if (overrides?.MaxOutputTokens is int maxTok && maxTok > 0)
            {
                final = TruncateByApproxTokens(final, maxTok, locale);
            }
            yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = final });
        }

        private static string TruncateByApproxTokens(string text, int maxTokens, string locale)
        {
            text = text ?? string.Empty;
            if (maxTokens <= 0) return text;
            // 近似策略：
            // - 中文（zh）按字符≈token，硬截断至 maxTokens 字符
            // - 其他语言按 ~4 字符/词估算，硬截断至 maxTokens*4 字符
            var isZh = (locale ?? string.Empty).StartsWith("zh", StringComparison.OrdinalIgnoreCase);
            var maxChars = isZh ? maxTokens : Math.Max(1, maxTokens * 4);
            if (text.Length <= maxChars) return text;
            return text.Substring(0, Math.Max(0, maxChars)).TrimEnd() + "…";
        }

        private static string TruncateByChars(string text, int maxChars)
        {
            text = text ?? string.Empty;
            if (maxChars <= 0 || text.Length <= maxChars) return text;
            return text.Substring(0, Math.Max(0, maxChars)).TrimEnd() + "…";
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
                    if (!bindingOk) bindError = "人物未任职，命令无法下达";
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
            var input = await BuildPromptInputForCommandAsync(ids, userInput, locale, ct);
            var systemPrompt = await _assembler.ComposeSystemPromptAsync(input, ct);

            // 4) 走编排（五步），此处复用 OrchestrationService 外部接口
            var orchestrator = CoreServices.Locator.Get<RimAI.Core.Contracts.IOrchestrationService>();
            string final = string.Empty;

            try
            {
                // 执行工具仅编排（显式模式 Classic），以便保持最小可运行
                var toolRes = await orchestrator.ExecuteAsync(userInput, participantIds, mode: "Classic", ct: ct);
                final = Newtonsoft.Json.JsonConvert.SerializeObject(toolRes);
                yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = final });
            }
            catch (System.Exception ex)
            {
                yield return Result<UnifiedChatChunk>.Failure(ex.Message);
            }
            // 历史写入职责上移：调用方（UI/服务）负责落盘。此处仅返回结果。
        }

        private async Task<RimAI.Core.Modules.Orchestration.PromptAssemblyInput> BuildPromptInputForChatAsync(
            List<string> participantIds,
            string userInput,
            string locale,
            CancellationToken ct)
        {
            // 复用旧逻辑在此处拉取素材，D2/D3 将迁至 Organizer
            var cfg = _config?.Current;
            var histCfg = cfg?.History ?? new RimAI.Core.Settings.HistoryConfig();
            var localeToUse = locale ?? _templates.ResolveLocale();

            string convKey = string.Join("|", participantIds.OrderBy(x => x, StringComparer.Ordinal));

            string personaPrompt = string.Empty;
            try
            {
                var pid = participantIds.FirstOrDefault(x => x.StartsWith("persona:", StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(pid))
                {
                    var tail = pid.Substring("persona:".Length);
                    var name = tail.Contains('#') ? tail.Split('#')[0] : tail;
                    personaPrompt = _persona?.Get(name)?.SystemPrompt ?? string.Empty;
                }
            }
            catch { }

            var input = new RimAI.Core.Modules.Orchestration.PromptAssemblyInput
            {
                Mode = RimAI.Core.Modules.Orchestration.PromptMode.Chat,
                Locale = localeToUse,
                PersonaSystemPrompt = personaPrompt,
            };

            // Beliefs 合并到 persona 段
            try
            {
                foreach (var id in participantIds)
                {
                    if (!id.StartsWith("pawn:")) continue;
                    var b = CoreServices.Locator.Get<IPersonalBeliefsAndIdeologyService>()?.GetByPawn(id);
                    if (b != null)
                    {
                        input.Beliefs ??= new RimAI.Core.Modules.Orchestration.BeliefsModel();
                        input.Beliefs.Worldview ??= b.Worldview;
                        input.Beliefs.Values ??= b.Values;
                        input.Beliefs.CodeOfConduct ??= b.CodeOfConduct;
                        input.Beliefs.TraitsText ??= b.TraitsText;
                    }
                }
            }
            catch { }

            // Fixed prompts + 会话级覆盖
            try
            {
                var fixedSvc = CoreServices.Locator.Get<IFixedPromptService>();
                foreach (var id in participantIds)
                {
                    if (!id.StartsWith("pawn:")) continue;
                    var text = fixedSvc?.GetByPawn(id);
                    if (!string.IsNullOrWhiteSpace(text)) input.Extras.Add($"- {CoreServices.Locator.Get<IParticipantIdService>()?.GetDisplayName(id)}: {text}");
                }
                var scenarioOverride = fixedSvc?.GetConvKeyOverride(convKey);
                if (!string.IsNullOrWhiteSpace(scenarioOverride)) input.FixedPromptOverride = scenarioOverride;
            }
            catch { }

            // Recap & recent history
            try
            {
                var writer = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                var list = await writer.FindByConvKeyAsync(convKey);
                var cid = list?.LastOrDefault();
                if (!string.IsNullOrWhiteSpace(cid))
                {
                    var recaps = CoreServices.Locator.Get<RimAI.Core.Modules.History.IRecapService>()?.GetRecapItems(cid);
                    if (recaps != null)
                    {
                        int k = Math.Max(1, histCfg.RecapDictMaxEntries);
                        foreach (var r in recaps.OrderByDescending(r => r.CreatedAt).Take(k).Reverse())
                            input.RecapSegments.Add(r.Text);
                    }
                }
            }
            catch { }
            try
            {
                var ctx = await CoreServices.Locator.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>()
                    .GetHistoryAsync(participantIds);
                foreach (var e in ctx.MainHistory.SelectMany(c => c.Entries).OrderByDescending(e => e.Timestamp).Take(6).OrderBy(e => e.Timestamp))
                    input.HistorySnippets.Add($"- {e.SpeakerId}: {e.Content}");
            }
            catch { }

            // userInput 不加入 system，由模板方负责在最终消息中注入 user role
            return input;
        }

        private async Task<RimAI.Core.Modules.Orchestration.PromptAssemblyInput> BuildPromptInputForCommandAsync(
            List<string> participantIds,
            string userInput,
            string locale,
            CancellationToken ct)
        {
            var input = await BuildPromptInputForChatAsync(participantIds, userInput, locale, ct);
            input.Mode = RimAI.Core.Modules.Orchestration.PromptMode.Command;
            // Command 增补：相关历史（背景）与传记（仅 1v1 player↔pawn）
            try
            {
                var cfg = _config?.Current;
                var cmdSeg = cfg?.Prompt?.Segments?.Command;
                bool isPlayerNpc = participantIds.Any(x => x.StartsWith("player:")) && participantIds.Any(x => x.StartsWith("pawn:"));
                if (isPlayerNpc)
                {
                    var pawnId = participantIds.First(x => x.StartsWith("pawn:"));
                    var bioSvc = CoreServices.Locator.Get<IBiographyService>();
                    foreach (var b in bioSvc?.ListByPawn(pawnId) ?? new List<BiographyItem>())
                        input.BiographyParagraphs.Add("- " + b.Text);
                }
                var ctxHist = await CoreServices.Locator.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>()
                    .GetHistoryAsync(participantIds);
                int perConv = Math.Max(1, cmdSeg?.RelatedMaxEntriesPerConversation ?? 5);
                foreach (var e in ctxHist.BackgroundHistory.SelectMany(c => c.Entries.OrderByDescending(e => e.Timestamp).Take(perConv).OrderBy(e => e.Timestamp)))
                    input.HistorySnippets.Add($"- {e.SpeakerId}: {e.Content}");
            }
            catch { }
            return input;
        }
    }
}


