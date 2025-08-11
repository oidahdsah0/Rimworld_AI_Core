using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class RunToolButton : IDebugPanelButton
    {
        public string Label => "Run Tool";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.AppendOutput("[提示] 直接通过 IToolRegistryService 执行 get_colony_status（绕过编排），不受‘工具匹配模式/向量索引’影响。");
            var registry = ctx.Get<RimAI.Core.Contracts.Tooling.IToolRegistryService>();
            Task.Run(async () =>
            {
                try
                {
                    var result = await registry.ExecuteToolAsync("get_colony_status", new System.Collections.Generic.Dictionary<string, object>());
                    var json = JsonConvert.SerializeObject(result, Formatting.None);
                    ctx.AppendOutput($"Colony Status: {json}");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Run Tool failed: {ex.Message}");
                }
            });
        }
    }
}


