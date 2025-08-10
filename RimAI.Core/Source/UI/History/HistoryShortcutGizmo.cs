using RimWorld;
using Verse;
using System.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Modules.Persona;

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
                        // 将当前键写入全局 UI 状态，供窗口与后续切换使用
                        RimAI.Core.UI.HistoryManager.HistoryUIState.CurrentConvKey = convKey;
                        Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager(convKey));
                    }
                    catch
                    {
                        Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager());
                    }
                }
            };
        }

        public static Command_Action CreateSmalltalkForPawn(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "闲聊",
                defaultDesc = "与该殖民者进行闲聊。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Buttons/OpenChat"),
                action = () =>
                {
                    try
                    {
                        var pidSvc = Infrastructure.CoreServices.Locator.Get<Modules.World.IParticipantIdService>();
                        string pawnId = pidSvc.FromVerseObject(pawn);
                        string playerId = pidSvc.GetPlayerId();
                        string convKey = string.Join("|", new[] { pawnId, playerId });
                        var chat = new RimAI.Core.UI.Chat.MainTabWindow_Chat(convKey, "闲聊");
                        Find.WindowStack.Add(chat);
                    }
                    catch
                    {
                        // 失败时也打开一个空窗口（无需崩溃）
                        Find.WindowStack.Add(new RimAI.Core.UI.Chat.MainTabWindow_Chat(string.Empty, "闲聊"));
                    }
                }
            };
        }

        public static Command_Action CreateCommandForPawn(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "命令",
                defaultDesc = "与该殖民者进行指令对话。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Buttons/OpenCommand"),
                action = () =>
                {
                    try
                    {
                        var pidSvc = Infrastructure.CoreServices.Locator.Get<Modules.World.IParticipantIdService>();
                        string pawnId = pidSvc.FromVerseObject(pawn);
                        string playerId = pidSvc.GetPlayerId();
                        string convKey = string.Join("|", new[] { pawnId, playerId });
                        var chat = new RimAI.Core.UI.Chat.MainTabWindow_Chat(convKey, "命令");
                        Find.WindowStack.Add(chat);
                    }
                    catch
                    {
                        Find.WindowStack.Add(new RimAI.Core.UI.Chat.MainTabWindow_Chat(string.Empty, "命令"));
                    }
                }
            };
        }

        public static Command_Action CreateAppointForPawn(Pawn pawn)
        {
            return new Command_Action
            {
                defaultLabel = "任命",
                defaultDesc = "为该殖民者任命人格。",
                icon = ContentFinder<UnityEngine.Texture2D>.Get("UI/Buttons/OpenCommand"),
                action = () =>
                {
                    try
                    {
                        var pidSvc = Infrastructure.CoreServices.Locator.Get<Modules.World.IParticipantIdService>();
                        var personaSvc = Infrastructure.CoreServices.Locator.Get<IPersonaService>();
                        var bindingSvc = Infrastructure.CoreServices.Locator.Get<IPersonaBindingService>();

                        string pawnId = pidSvc.FromVerseObject(pawn);
                        var names = (personaSvc.GetAll() ?? System.Array.Empty<RimAI.Core.Contracts.Models.Persona>())
                            .Select(p => p.Name)
                            .OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase)
                            .ToList();

                        var options = new System.Collections.Generic.List<FloatMenuOption>();
                        if (names.Count == 0)
                        {
                            options.Add(new FloatMenuOption("无可用人格", null));
                        }
                        else
                        {
                            foreach (var name in names)
                            {
                                options.Add(new FloatMenuOption(name, () =>
                                {
                                    try
                                    {
                                        bindingSvc.Bind(pawnId, name, 0);
                                        Messages.Message($"已为 {pawn?.LabelShort ?? pawnId} 任命人格：{name}", MessageTypeDefOf.TaskCompletion, false);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Messages.Message("任命失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
                                    }
                                }));
                            }
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                    catch (System.Exception ex)
                    {
                        Messages.Message("打开任命菜单失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
                    }
                }
            };
        }
    }
}


