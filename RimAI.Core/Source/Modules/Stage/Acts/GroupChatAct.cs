using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Infrastructure;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Modules.Persona;
using RimAI.Framework.Contracts;
using RimAI.Core.Modules.Stage.Topic;

namespace RimAI.Core.Modules.Stage.Acts
{
    internal sealed class GroupChatAct : IStageAct
    {
        public string Name => "GroupChat";

        public bool IsEligible(ActContext ctx)
        {
            if (ctx == null) return false;
            var n = ctx.Participants?.Count ?? 0;
            return n >= Math.Max(2, ctx?.Options?.MinParticipants ?? 2);
        }

        public async Task<ActResult> RunAsync(ActContext ctx, CancellationToken ct = default)
        {
            var rounds = Math.Max(1, ctx?.Options?.GroupChatMaxRounds ?? 2);
            var order = StableShuffle(ctx.Participants, ctx.Seed);
            string locale = ctx.Locale;

            try { CoreServices.Logger.Info($"[Stage/GroupChat] Start convKey={ctx.ConvKey} rounds={rounds} participants={string.Join(",", ctx.Participants ?? new List<string>())}"); } catch { }

            // 选题与会话级场景提示（下沉至 Act）
            try
            {
                var topicSvc = CoreServices.Locator.Get<ITopicService>();
                var weights = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>()?.Current?.Stage?.Topic?.Sources;
                var topicCtx = new TopicContext { ConvKey = ctx.ConvKey, Participants = ctx.Participants, Seed = ctx.Seed, Locale = locale };
                var selected = await topicSvc.SelectAsync(topicCtx, weights, ct);
                if (!string.IsNullOrWhiteSpace(selected?.ScenarioText))
                {
                    var fixedSvc = CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>();
                    fixedSvc?.UpsertConvKeyOverride(ctx.ConvKey, selected.ScenarioText);
                    Publish(ctx, "TopicSelected", ctx.ConvKey, new { topic = selected.Topic, scenarioChars = selected.ScenarioText?.Length ?? 0 });
                }
            }
            catch { }

            for (int i = 0; i < rounds; i++)
            {
                foreach (var speakerId in order)
                {
                    if (ct.IsCancellationRequested) return new ActResult { Completed = false, Reason = "Cancelled", Rounds = i };

                    var turnInstruction = $"轮到{speakerId}发言。请简洁表达观点，并与上文保持连贯。";
                    string final = string.Empty;
                    string error = null;

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        cts.CancelAfter(Math.Max(1000, ctx.Options?.MaxLatencyMsPerTurn ?? 5000));
                        try
                        {
                            try { CoreServices.Logger.Info($"[Stage/GroupChat] TurnStart convKey={ctx.ConvKey} round={i + 1} speaker={speakerId}"); } catch { }
                            var persona = CoreServices.Locator.Get<IPersonaConversationService>();
                            var overrides = new PersonaConversationService.StageChatOverrides { MaxOutputTokens = 100 };
                            await foreach (var chunk in ((PersonaConversationService)persona).ChatForStageAsync(ctx.Participants, personaName: null, userInput: turnInstruction, locale: locale, overrides: overrides, ct: cts.Token))
                            {
                                if (cts.IsCancellationRequested) break;
                                if (chunk.IsSuccess) final += chunk.Value?.ContentDelta ?? string.Empty; else error = chunk.Error;
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            if (string.IsNullOrEmpty(final)) error = "Timeout";
                        }
                        catch (Exception ex)
                        {
                            error = ex.Message;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(final) && !string.IsNullOrWhiteSpace(error))
                    {
                        try { CoreServices.Logger.Warn($"[Stage/GroupChat] TurnFailed convKey={ctx.ConvKey} round={i + 1} speaker={speakerId} error={error}"); } catch { }
                        Publish(ctx, "TurnCompleted", ctx.ConvKey, new { ok = false, speakerId, error });
                        continue; // 跳过该发言
                    }

                    try
                    {
                        var history = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                        var idsByKey = await history.FindByConvKeyAsync(ctx.ConvKey);
                        var convId = idsByKey?.LastOrDefault();
                        if (string.IsNullOrWhiteSpace(convId)) convId = history.CreateConversation(ctx.Participants);
                        await history.AppendEntryAsync(convId, new RimAI.Core.Contracts.Models.ConversationEntry(speakerId, final, DateTime.UtcNow));
                        try { CoreServices.Logger.Info($"[Stage/GroupChat] HistoryAppended convKey={ctx.ConvKey} round={i + 1} speaker={speakerId} len={final.Length}"); } catch { }
                    }
                    catch { }

                    try { CoreServices.Logger.Info($"[Stage/GroupChat] TurnCompleted convKey={ctx.ConvKey} round={i + 1} speaker={speakerId} len={final.Length}"); } catch { }
                    Publish(ctx, "TurnCompleted", ctx.ConvKey, new { ok = true, speakerId, len = final.Length, round = i + 1, text = final });
                }
            }

            try { CoreServices.Logger.Info($"[Stage/GroupChat] Completed convKey={ctx.ConvKey} reason=MaxRoundsReached rounds={rounds}"); } catch { }
            var finalSummary = $"群聊已结束，共 {rounds} 轮。";
            try
            {
                // 清理会话级场景提示覆盖
                CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>()?.DeleteConvKeyOverride(ctx.ConvKey);
            }
            catch { }
            return new ActResult { Completed = true, Reason = "MaxRoundsReached", Rounds = rounds, FinalText = finalSummary };
        }

        private static IReadOnlyList<string> StableShuffle(IReadOnlyList<string> list, int seed)
        {
            var arr = list?.ToList() ?? new List<string>();
            var rnd = new Random(unchecked(seed ^ (int)0x85ebca6b));
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
            return arr;
        }

        private static void Publish(ActContext ctx, string stage, string convKey, object payload)
        {
            try
            {
                ctx?.Events?.Publish(new OrchestrationProgressEvent
                {
                    Source = nameof(GroupChatAct),
                    Stage = stage,
                    Message = convKey,
                    PayloadJson = payload == null ? string.Empty : JsonConvert.SerializeObject(payload)
                });
            }
            catch { }
        }
    }
}


