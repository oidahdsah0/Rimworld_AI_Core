using Verse;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class OpenHistoryManagerButton : IDebugPanelButton
    {
        public string Label => "History Manager";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                string preset = $"{ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId()}|pawn:DEMO";
                Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager(preset));
            }
            catch (System.Exception ex)
            {
                ctx.AppendOutput($"Open History Manager failed: {ex.Message}");
            }
        }
    }
}


