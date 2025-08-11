using System;
using System.Linq;
using System.Collections.Generic;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.World;
using RimAI.Core.Infrastructure;
using RimAI.Core.Contracts.Services;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 头像、标题、子标题和头部按钮组。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        private Pawn TryResolvePawnFromConvKey()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_convKeyInput)) return null;
                // 命中缓存则直接返回
                if (!string.IsNullOrWhiteSpace(_cachedPawnConvKey) && string.Equals(_cachedPawnConvKey, _convKeyInput, StringComparison.Ordinal) && _cachedPawn != null)
                    return _cachedPawn;
                var ids = _convKeyInput.Split('|');
                string pawnId = ids.FirstOrDefault(id => id.StartsWith("pawn:", StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(pawnId)) return null;
                var tail = pawnId.Substring("pawn:".Length);
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    try
                    {
                        var uid = p?.GetUniqueLoadID();
                        var tid = p?.ThingID;
                        if (string.Equals(uid, tail, StringComparison.Ordinal) || string.Equals(tid, tail, StringComparison.Ordinal))
                        {
                            _cachedPawn = p;
                            _cachedPawnConvKey = _convKeyInput;
                            return p;
                        }
                    }
                    catch { }
                }
                var map = Find.CurrentMap;
                var allSpawned = map?.mapPawns?.AllPawnsSpawned;
                if (allSpawned != null)
                {
                    foreach (var p in allSpawned)
                    {
                        try
                        {
                            var uid = p?.GetUniqueLoadID();
                            var tid = p?.ThingID;
                            if (string.Equals(uid, tail, StringComparison.Ordinal) || string.Equals(tid, tail, StringComparison.Ordinal))
                            {
                                _cachedPawn = p as Pawn;
                                _cachedPawnConvKey = _convKeyInput;
                                return _cachedPawn;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        private void DrawHeader(Rect inRect, ref float y)
        {
            float avatarSize = 48f;
            var headerRect = new Rect(inRect.x, y, inRect.width, HeaderRowHeight);
            var avatarRect = new Rect(headerRect.x, headerRect.y + (HeaderRowHeight - avatarSize) / 2f, avatarSize, avatarSize);
            var titleRect = new Rect(avatarRect.xMax + 8f, headerRect.y, Mathf.Max(0f, headerRect.width - avatarRect.width - 8f), headerRect.height);

            var pawn = TryResolvePawnFromConvKey();
            if (pawn != null)
            {
                Widgets.ThingIcon(avatarRect, pawn);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(avatarRect, new Color(0f, 0f, 0f, 0.15f), new Color(0f, 0f, 0f, 0.35f));
            }

            var oldFont = Text.Font; var oldAnchor = Text.Anchor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, GetHeaderText());
            Text.Font = oldFont; Text.Anchor = oldAnchor;

            y += HeaderRowHeight;
        }

        private void DrawSubHeader(Rect inRect, ref float y)
        {
            var subRect = new Rect(inRect.x, y, inRect.width, SubHeaderRowHeight);
            string personaName = GetBoundPersonaName();
            string personaLabel = string.IsNullOrWhiteSpace(personaName) ? "未任命" : $"人格：{personaName}";
            var leftRect = new Rect(subRect.x, subRect.y, Mathf.Max(0f, subRect.width - 216f), subRect.height);
            var rightRect = new Rect(leftRect.xMax, subRect.y, 216f, subRect.height);

            var oldFont = Text.Font; var oldAnchor = Text.Anchor; var oldColor = GUI.color;
            Text.Font = GameFont.Small; Text.Anchor = TextAnchor.MiddleLeft;
            if (string.Equals(personaLabel, "未任命", StringComparison.Ordinal))
            {
                GUI.color = new Color(0.9f, 0.3f, 0.3f, 1f);
            }
            Widgets.Label(leftRect, personaLabel);
            GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor;

            DrawHeaderControls(rightRect);
            y += SubHeaderRowHeight;
        }

        private void DrawHeaderControls(Rect controlsRect)
        {
            float buttonHeight = 28f;
            float buttonWidth = 96f;
            float vCenterY = controlsRect.y + (controlsRect.height - buttonHeight) / 2f;
            var appointBtnRect = new Rect(controlsRect.x, vCenterY, buttonWidth, buttonHeight);
            var historyBtnRect = new Rect(appointBtnRect.xMax + 8f, vCenterY, buttonWidth, buttonHeight);

            if (Widgets.ButtonText(appointBtnRect, "任命"))
            {
                try { ShowAppointMenu(); }
                catch (Exception ex)
                { Messages.Message("打开任命菜单失败: " + ex.Message, MessageTypeDefOf.RejectInput, false); }
            }

            if (Widgets.ButtonText(historyBtnRect, "历史记录"))
            {
                try { OpenHistoryManager(); }
                catch (Exception ex)
                { Messages.Message("打开历史记录失败: " + ex.Message, MessageTypeDefOf.RejectInput, false); }
            }
        }

        private void ShowAppointMenu()
        {
            var pawn = TryResolvePawnFromConvKey();
            if (pawn == null)
            {
                var opts = new List<FloatMenuOption> { new FloatMenuOption("不可用：未解析到 NPC", null) };
                Find.WindowStack.Add(new FloatMenu(opts));
                return;
            }

            try
            {
                var pidSvc = CoreServices.Locator.Get<IParticipantIdService>();
                var personaSvc = CoreServices.Locator.Get<IPersonaService>();
                var bindingSvc = CoreServices.Locator.Get<IPersonaBindingService>();

                string pawnId = pidSvc.FromVerseObject(pawn);
                var names = (personaSvc.GetAll() ?? Array.Empty<RimAI.Core.Contracts.Models.Persona>())
                    .Select(p => p.Name)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var options = new List<FloatMenuOption>();
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
                            catch (Exception ex)
                            {
                                Messages.Message("任命失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
                            }
                        }));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            catch (Exception ex)
            {
                Messages.Message("打开任命菜单失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }

        private void OpenHistoryManager()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_convKeyInput))
                {
                    RimAI.Core.UI.HistoryManager.HistoryUIState.CurrentConvKey = _convKeyInput;
                    Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager(_convKeyInput));
                }
                else
                {
                    Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager());
                }
            }
            catch
            {
                Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager());
            }
        }

        private string GetHeaderText()
        {
            if (string.IsNullOrWhiteSpace(_convKeyInput)) return _modeTitle;
            var names = _convKeyInput.Split('|').Select(id => _pidService.GetDisplayName(id));
            return $"{_modeTitle}：{string.Join(" ↔ ", names)}";
        }

        private string GetBoundPersonaName()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_convKeyInput)) return null;
                var pawnId = _convKeyInput.Split('|').FirstOrDefault(id => id.StartsWith("pawn:", StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(pawnId)) return null;
                var binder = CoreServices.Locator.Get<IPersonaBindingService>();
                var binding = binder?.GetBinding(pawnId);
                var name = binding?.PersonaName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch { return null; }
        }
    }
}


