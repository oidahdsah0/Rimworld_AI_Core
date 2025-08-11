using Verse;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class OpenPersonalityWindowButton : IDebugPanelButton
    {
        public string Label => "Open Personality";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                Find.WindowStack.Add(new RimAI.Core.UI.Personality.MainTabWindow_Personality());
            }
            catch (System.Exception ex)
            {
                ctx.AppendOutput($"Open Personality failed: {ex.Message}");
            }
        }
    }
}


