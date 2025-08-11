namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ClearOutputButton : IDebugPanelButton
    {
        public string Label => "Clear Output";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.ClearOutput();
            ctx.AppendOutput("Output cleared.");
        }
    }
}


