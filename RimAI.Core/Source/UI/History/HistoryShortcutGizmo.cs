using RimWorld;
using Verse;

namespace RimAI.Core.UI.History
{
    /// <summary>
    /// 在 Pawn 的检查面板添加“历史记录”按钮，点击打开 History Manager。
    /// 简易实现：通过 Gizmo 注入。
    /// </summary>
    public static class HistoryShortcutGizmo
    {
        public static Command_Action CreateForPawn(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "历史记录",
                defaultDesc = "打开 AI 历史记录管理窗口。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Buttons/OpenHistory"),
                action = () =>
                {
                    try
                    {
                        string pawnId = Infrastructure.CoreServices.Locator.Get<Modules.World.IParticipantIdService>().FromVerseObject(pawn);
                        string playerId = Infrastructure.CoreServices.Locator.Get<Modules.World.IParticipantIdService>().GetPlayerId();
                        string convKey = string.Join("|", new[] { pawnId, playerId });
                        Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager(convKey));
                    }
                    catch
                    {
                        Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager());
                    }
                }
            };
        }
    }
}


