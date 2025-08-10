using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.World;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI.PersonaManager
{
    public class MainTabWindow_PersonaBindingPanel : Window
    {
        private readonly IPersonaService _personaService;
        private readonly IPersonaBindingService _bindingService;
        private readonly IParticipantIdService _pidService;

        private Vector2 _scroll = Vector2.zero;
        private readonly Dictionary<string, int> _selectedPersonaIndexByPawn = new Dictionary<string, int>(StringComparer.Ordinal);

        public MainTabWindow_PersonaBindingPanel()
        {
            _personaService = CoreServices.Locator.Get<IPersonaService>();
            _bindingService = CoreServices.Locator.Get<IPersonaBindingService>();
            _pidService = CoreServices.Locator.Get<IParticipantIdService>();
            forcePause = false; doCloseX = true; draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(720f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            // 数据准备
            var personas = _personaService.GetAll().OrderBy(p => p.Name).ToList();
            var personaNames = personas.Select(p => p.Name).ToList();
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned?.ToList() ?? new List<Pawn>();

            // 列表头
            float headerH = 26f;
            Widgets.Label(new Rect(inRect.x, inRect.y, 220f, headerH), "殖民地成员");
            Widgets.Label(new Rect(inRect.x + 230f, inRect.y, 200f, headerH), "人格");
            Widgets.Label(new Rect(inRect.x + 440f, inRect.y, 80f, headerH), "操作");

            // 列表区域
            var listRect = new Rect(inRect.x, inRect.y + headerH + 6f, inRect.width, inRect.height - (headerH + 20f));
            float rowH = 32f;
            var viewHeight = pawns.Count * rowH + 10f;
            var viewRect = new Rect(0, 0, listRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(listRect, ref _scroll, viewRect);
            float y = 0f;
            for (int i = 0; i < pawns.Count; i++)
            {
                var pawn = pawns[i];
                var rowRect = new Rect(0, y, viewRect.width, rowH);
                if (i % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                string pawnId = string.Empty;
                try { pawnId = _pidService.FromVerseObject(pawn); } catch { }

                // 左：头像 + 名字
                var iconRect = new Rect(0, y + 2f, 28f, 28f);
                var nameRect = new Rect(32f, y + 4f, 190f, 24f);
                try { Widgets.ThingIcon(iconRect, pawn); } catch { }
                Widgets.Label(nameRect, pawn?.LabelShort ?? pawn?.Name?.ToStringShort ?? "<未知>");

                // 中：人格下拉
                var dropdownRect = new Rect(230f, y + 4f, 190f, 24f);
                string currentBound = null;
                try { currentBound = _bindingService.GetBinding(pawnId)?.PersonaName; } catch { }
                if (!_selectedPersonaIndexByPawn.ContainsKey(pawnId))
                {
                    int idx = -1;
                    if (!string.IsNullOrWhiteSpace(currentBound))
                    {
                        idx = personaNames.FindIndex(n => string.Equals(n, currentBound, StringComparison.OrdinalIgnoreCase));
                    }
                    _selectedPersonaIndexByPawn[pawnId] = idx;
                }
                int selIdx = _selectedPersonaIndexByPawn[pawnId];
                string selName = (selIdx >= 0 && selIdx < personaNames.Count) ? personaNames[selIdx] : (currentBound ?? "<选择>");
                if (Widgets.ButtonText(dropdownRect, selName))
                {
                    var floatMenu = new List<FloatMenuOption>();
                    for (int j = 0; j < personaNames.Count; j++)
                    {
                        int captured = j;
                        string label = personaNames[j];
                        floatMenu.Add(new FloatMenuOption(label, () => _selectedPersonaIndexByPawn[pawnId] = captured));
                    }
                    if (floatMenu.Count == 0)
                    {
                        floatMenu.Add(new FloatMenuOption("无可用人格", null));
                    }
                    Find.WindowStack.Add(new FloatMenu(floatMenu));
                }

                // 右：绑定/解绑
                var bindRect = new Rect(440f, y + 4f, 70f, 24f);
                var unbindRect = new Rect(515f, y + 4f, 70f, 24f);
                bool canBind = selIdx >= 0 && selIdx < personaNames.Count && !string.IsNullOrWhiteSpace(pawnId);
                bool oldEnabled = GUI.enabled;
                GUI.enabled = canBind;
                if (Widgets.ButtonText(bindRect, "绑定"))
                {
                    try
                    {
                        _bindingService.Bind(pawnId, personaNames[selIdx], 0);
                        Messages.Message($"已为 {pawn?.LabelShort ?? pawnId} 绑定人格：{personaNames[selIdx]}", RimWorld.MessageTypeDefOf.TaskCompletion, false);
                    }
                    catch (Exception ex)
                    {
                        Messages.Message("绑定失败: " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, false);
                    }
                }
                GUI.enabled = oldEnabled;
                if (Widgets.ButtonText(unbindRect, "解绑"))
                {
                    try
                    {
                        _bindingService.Unbind(pawnId);
                        _selectedPersonaIndexByPawn[pawnId] = -1;
                        Messages.Message($"已为 {pawn?.LabelShort ?? pawnId} 解除人格绑定", RimWorld.MessageTypeDefOf.TaskCompletion, false);
                    }
                    catch (Exception ex)
                    {
                        Messages.Message("解绑失败: " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, false);
                    }
                }

                y += rowH;
            }
            Widgets.EndScrollView();
        }
    }
}


