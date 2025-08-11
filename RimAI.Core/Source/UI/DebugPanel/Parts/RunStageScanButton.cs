using System.Threading.Tasks;
using RimAI.Core.Modules.Stage;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class RunStageScanButton : IDebugPanelButton
    {
        public string Label => "执行一次扫描 (P11)";

        public void Execute(DebugPanelContext ctx)
        {
            var stage = ctx.Get<IStageService>();
            ctx.AppendOutput("[P11] 扫描执行中…");
            _ = Task.Run(async () =>
            {
                await stage.RunScanOnceAsync();
                ctx.AppendOutput("[P11] 扫描完成");
            });
        }
    }
}


