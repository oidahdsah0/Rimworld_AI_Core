using System;
using System.Collections.Generic;
using RimAI.Core.Modules.Stage;
using RimAI.Core.Modules.World;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class TriggerGroupChatButton : IDebugPanelButton
    {
        public string Label => "触发群聊 (P11)";

        public void Execute(DebugPanelContext ctx)
        {
            var stage = ctx.Get<IStageService>();
            var pid = ctx.Get<IParticipantIdService>();
            // 简化：随机挑选2个参与者（用玩家+一个随机代理占位）
            var p1 = pid.GetPlayerId();
            var p2 = pid.FromVerseObject(new object()); // 生成 agent:*
            var req = new StageRequest
            {
                Participants = new List<string> { p1, p2 },
                Origin = "PawnBehavior",
                InitiatorId = p1,
                Mode = "Chat",
                Stream = false,
                UserInputOrScenario = "调试：群聊测试入口"
            };
            ctx.AppendOutput("[P11] 触发群聊…");
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await foreach (var _ in stage.StartAsync(req)) { }
                ctx.AppendOutput("[P11] 群聊触发完成");
            });
        }
    }
}


