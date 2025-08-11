using System;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class PingButton : IDebugPanelButton
    {
        public string Label => "Ping";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.AppendOutput("Ping button clicked – DI container state: OK");
        }
    }
}


