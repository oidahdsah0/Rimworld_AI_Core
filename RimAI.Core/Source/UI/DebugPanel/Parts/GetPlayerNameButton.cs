using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class GetPlayerNameButton : IDebugPanelButton
    {
        public string Label => "Get Player Name";

        public void Execute(DebugPanelContext ctx)
        {
            var world = ctx.Get<RimAI.Core.Modules.World.IWorldDataService>();
            Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var name = await world.GetPlayerNameAsync();
                    sw.Stop();
                    var ms = sw.Elapsed.TotalMilliseconds;
                    var green = ms <= 1.0;
                    var tagOpen = green ? "<color=green>" : "<color=red>";
                    var tagClose = "</color>";
                    ctx.AppendOutput($"{tagOpen}Player Faction Name: {name} (Δ {ms:F2} ms){tagClose}");
                }
                catch (Exception ex)
                {
                    ctx.AppendOutput($"Get Player Name failed: {ex.Message}");
                }
            });
        }
    }
}


