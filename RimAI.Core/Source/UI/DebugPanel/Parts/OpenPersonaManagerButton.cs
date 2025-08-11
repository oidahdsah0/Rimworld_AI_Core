using Verse;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class OpenPersonaManagerButton : IDebugPanelButton
    {
        public string Label => "Persona Manager";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                Find.WindowStack.Add(new RimAI.Core.UI.PersonaManager.MainTabWindow_PersonaManager());
            }
            catch (System.Exception ex)
            {
                ctx.AppendOutput($"Open Persona Manager failed: {ex.Message}");
            }
        }
    }
}


