using System;
using System.Linq;
using System.Collections.Generic;
using RimAI.Core.Modules.Stage;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class TriggerGroupChatButton : IDebugPanelButton
    {
        public string Label => "触发群聊 (P11.5)";

        public void Execute(DebugPanelContext ctx)
        {
            var stage = ctx.Get<IStageService>();
            var kernel = ctx.Get<RimAI.Core.Modules.Stage.Kernel.IStageKernel>();
            var trigger = RimAI.Core.Infrastructure.ServiceContainer.Get<IEnumerable<RimAI.Core.Modules.Stage.Triggers.IStageTrigger>>()
                .FirstOrDefault(t => string.Equals(t.TargetActName, "GroupChat", StringComparison.OrdinalIgnoreCase));
            if (trigger == null)
            {
                ctx.AppendOutput("[P11.5] 未找到 GroupChatTrigger");
                return;
            }
            ctx.AppendOutput("[P11.5] 执行 GroupChatTrigger.RunOnceAsync…");
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await trigger.RunOnceAsync(stage, kernel, default);
                ctx.AppendOutput("[P11.5] GroupChatTrigger 执行完成");
            });
        }
    }
}


