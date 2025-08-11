using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class AskColonyStatusButton : IDebugPanelButton
    {
        public string Label => "Ask Colony Status";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                var cfg = ctx.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                var mode = cfg?.Current?.Embedding?.Tools?.Mode ?? "Classic";
                ctx.AppendOutput($"[提示] 通过编排服务执行，受设置页影响（模式={mode}，TopK/阈值/索引/动态阈值等）。");
            }
            catch { ctx.AppendOutput("[提示] 通过编排服务执行，受设置页影响。"); }

            try
            {
                var orchestrator = ctx.Get<RimAI.Core.Contracts.IOrchestrationService>();
                var query = "殖民地概况？";
                var stream = orchestrator.ExecuteToolAssistedQueryAsync(query);
                Task.Run(async () => await ctx.HandleStreamingOutputAsync("Ask Colony Status", stream));
            }
            catch (System.Exception ex)
            {
                ctx.AppendOutput($"Ask Colony Status failed: {ex.Message}");
            }
        }
    }
}


