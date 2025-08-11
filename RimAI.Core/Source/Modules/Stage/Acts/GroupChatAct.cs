using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Modules.Persona;
using RimAI.Framework.Contracts;

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

            for (int i = 0; i < rounds; i++)
            {
                foreach (var speakerId in order)
                {
                    if (ct.IsCancellationRequested) return new ActResult { Completed = false, Reason = "Cancelled", Rounds = i };

                    var turnInstruction = $"轮到{ctx.ParticipantId.GetDisplayName(speakerId)}发言。请简洁表达观点，并与上文保持连贯。";
                    string final = string.Empty;
                    string error = null;

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                    {
                        cts.CancelAfter(Math.Max(1000, ctx.Options?.MaxLatencyMsPerTurn ?? 5000));
                        try
                        {
                            var opts = new PersonaChatOptions { Stream = false, Locale = locale, WriteHistory = false };
                            await foreach (var chunk in ctx.Persona.ChatAsync(ctx.Participants, personaName: null, userInput: turnInstruction, options: opts, ct: cts.Token))
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
                        Publish(ctx, "TurnCompleted", ctx.ConvKey, new { ok = false, speakerId, error });
                        continue; // 跳过该发言
                    }

                    try
                    {
                        var idsByKey = await ctx.History.FindByConvKeyAsync(ctx.ConvKey);
                        var convId = idsByKey?.LastOrDefault();
                        if (string.IsNullOrWhiteSpace(convId)) convId = ctx.History.CreateConversation(ctx.Participants);
                        await ctx.History.AppendEntryAsync(convId, new RimAI.Core.Contracts.Models.ConversationEntry(speakerId, final, DateTime.UtcNow));
                    }
                    catch { }

                    Publish(ctx, "TurnCompleted", ctx.ConvKey, new { ok = true, speakerId, len = final.Length, round = i + 1 });
                }
            }

            return new ActResult { Completed = true, Reason = "MaxRoundsReached", Rounds = rounds };
        }

        private static IReadOnlyList<string> StableShuffle(IReadOnlyList<string> list, int seed)
        {
            var arr = list?.ToList() ?? new List<string>();
            var rnd = new Random(seed ^ 0x85ebca6b);
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
                ctx?.Events?.Publish(new StageProgressEvent
                {
                    Stage = stage,
                    ConvKey = convKey,
                    Message = stage,
                    PayloadJson = payload == null ? string.Empty : JsonConvert.SerializeObject(payload)
                });
            }
            catch { }
        }
    }
}


