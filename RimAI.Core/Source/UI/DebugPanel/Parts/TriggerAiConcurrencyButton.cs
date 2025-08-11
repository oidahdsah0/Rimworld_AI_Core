using System;
using System.Collections.Generic;
using System.Linq;
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
            var kernel = ctx.Get<RimAI.Core.Modules.Stage.Kernel.IStageKernel>();
            var trigger = RimAI.Core.Infrastructure.ServiceContainer.Get<IEnumerable<RimAI.Core.Modules.Stage.Triggers.IStageTrigger>>()
                .FirstOrDefault(t => string.Equals(t.TargetActName, "GroupChat", StringComparison.OrdinalIgnoreCase));
            if (trigger == null)
            {
                ctx.AppendOutput("[P11.5] 未找到 GroupChatTrigger");
                return;
            }
            ctx.AppendOutput("[P11.5] 并发触发 GroupChatTrigger 两次…");
            _ = Task.Run(async () =>
            {
                var t1 = Task.Run(async () => { await trigger.RunOnceAsync(stage, kernel, default); });
                var t2 = Task.Run(async () => { await trigger.RunOnceAsync(stage, kernel, default); });
                await Task.WhenAll(t1, t2);
                ctx.AppendOutput("[P11.5] 双源并发触发完成");
            });
        }
    }
}


