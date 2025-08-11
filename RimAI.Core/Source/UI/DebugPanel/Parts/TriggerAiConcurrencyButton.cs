using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Modules.Stage;
using RimAI.Core.Modules.World;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class TriggerAiConcurrencyButton : IDebugPanelButton
    {
        public string Label => "双源并发 (P11)";

        public void Execute(DebugPanelContext ctx)
        {
            var stage = ctx.Get<IStageService>();
            var pid = ctx.Get<IParticipantIdService>();
            var p1 = pid.GetPlayerId();
            var p2 = pid.FromVerseObject(new object());
            var participants = new List<string> { p1, p2 };
            string convKey = string.Join("|", participants);
            string idem = ctx.ComputeShortHash("concurrency:" + convKey);

            var r1 = new StageRequest { Participants = participants, Origin = "AIServer", InitiatorId = "server:A", SourceId = "A", IdempotencyKey = idem, Mode = "Chat", Stream = false, Priority = 1 };
            var r2 = new StageRequest { Participants = participants, Origin = "AIServer", InitiatorId = "server:B", SourceId = "B", IdempotencyKey = idem, Mode = "Chat", Stream = false, Priority = 2 };

            ctx.AppendOutput("[P11] 双源并发触发中…");
            _ = Task.Run(async () =>
            {
                var t1 = Task.Run(async () => { await foreach (var _ in stage.StartAsync(r1)) { } });
                var t2 = Task.Run(async () => { await foreach (var _ in stage.StartAsync(r2)) { } });
                await Task.WhenAll(t1, t2);
                ctx.AppendOutput("[P11] 双源并发触发完成");
            });
        }
    }
}


