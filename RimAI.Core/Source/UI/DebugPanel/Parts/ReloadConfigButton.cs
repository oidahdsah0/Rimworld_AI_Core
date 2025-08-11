using System;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ReloadConfigButton : IDebugPanelButton
    {
        public string Label => "Reload Config";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                var configSvc = ctx.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                configSvc.Reload();
                ctx.AppendOutput($"Config Reloaded – New Temperature: {configSvc.Current.LLM.Temperature}");
            }
            catch (Exception ex)
            {
                ctx.AppendOutput($"Reload Config failed: {ex.Message}");
            }
        }
    }
}


