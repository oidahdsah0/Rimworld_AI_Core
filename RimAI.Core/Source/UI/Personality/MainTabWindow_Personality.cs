using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.World;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.UI.Personality
{
    /// <summary>
    /// 个性窗体：统一管理固定提示词、个人传记、观点与意识形态。
    /// 左侧小人列表，右侧三个 Tab。
    /// </summary>
    public class MainTabWindow_Personality : Window
    {
        private readonly IFixedPromptService _fixedPrompts;
        private readonly IBiographyService _bio;
        private readonly IPersonalBeliefsAndIdeologyService _beliefs;
        private readonly IParticipantIdService _pid;

        private Vector2 _scrollLeft = Vector2.zero;
        private Vector2 _scrollRight = Vector2.zero;
        private int _activeTab = 0; // 0 固定提示词；1 传记；2 观点
        private List<Pawn> _pawns = new List<Pawn>();
        private Pawn _selected;
        private string _search = string.Empty;

        private const float LeftWidth = 280f;
        private const float RowH = 28f;
        private const float TabH = 26f;

        public MainTabWindow_Personality()
        {
            _fixedPrompts = CoreServices.Locator.Get<IFixedPromptService>();
            _bio = CoreServices.Locator.Get<IBiographyService>();
            _beliefs = CoreServices.Locator.Get<IPersonalBeliefsAndIdeologyService>();
            _pid = CoreServices.Locator.Get<IParticipantIdService>();

            forcePause = false;
            draggable = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;
            ReloadPawnList();
        }

        public override Vector2 InitialSize => new Vector2(1080f, 660f);

        public override void DoWindowContents(Rect inRect)
        {
            DrawLeft(inRect);
            DrawRight(inRect);
        }

        private void ReloadPawnList()
        {
            try
            {
                _pawns = PawnsFinder.AllMaps_FreeColonistsAndPrisoners?.ToList() ?? new List<Pawn>();
                _pawns = _pawns.OrderBy(p => p?.LabelShortCap?.ToString() ?? string.Empty, StringComparer.Ordinal).ToList();
                if (_selected == null) _selected = _pawns.FirstOrDefault();
            }
            catch { _pawns = new List<Pawn>(); }
        }

        private void DrawLeft(Rect inRect)
        {
            var left = new Rect(inRect.x, inRect.y, LeftWidth, inRect.height);
            Widgets.DrawMenuSection(left);

            var pad = 6f;
            var searchRect = new Rect(left.x + pad, left.y + pad, left.width - pad * 2, 24f);
            _search = Widgets.TextField(searchRect, _search ?? string.Empty);
            var listRect = new Rect(left.x + pad, searchRect.yMax + pad, left.width - pad * 2, left.height - searchRect.height - pad * 3);

            var items = _pawns.Where(p => string.IsNullOrWhiteSpace(_search) || (p?.LabelShortCap?.ToString() ?? string.Empty).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var viewH = Math.Max(listRect.height, items.Count * RowH + 8f);
            var viewRect = new Rect(0, 0, listRect.width - 16f, viewH);
            Widgets.BeginScrollView(listRect, ref _scrollLeft, viewRect);
            float y = 0f;
            foreach (var p in items)
            {
                var row = new Rect(0, y, viewRect.width, RowH);
                if (p == _selected) Widgets.DrawHighlight(row);
                if (Widgets.ButtonText(row, p?.LabelShortCap?.ToString() ?? "(未知)"))
                {
                    _selected = p;
                }
                y += RowH + 2f;
            }
            Widgets.EndScrollView();
        }

        private void DrawRight(Rect inRect)
        {
            var right = new Rect(inRect.x + LeftWidth + 6f, inRect.y, inRect.width - LeftWidth - 6f, inRect.height);
            Widgets.DrawMenuSection(right);
            if (_selected == null)
            {
                Widgets.Label(new Rect(right.x + 6f, right.y + 6f, right.width - 12f, 24f), "请选择一个小人");
                return;
            }
            var pawnId = _pid.FromVerseObject(_selected);
            var name = _selected?.LabelShortCap?.ToString() ?? pawnId;

            // Tabs
            float y = right.y + 6f;
            var tabs = new[] { "固定提示词", "个人传记", "观点与意识形态" };
            float curX = right.x + 6f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var label = tabs[i];
                var size = Text.CalcSize(label);
                var r = new Rect(curX, y, size.x + 24f, TabH);
                bool on = _activeTab == i;
                if (Widgets.ButtonText(r, label, drawBackground: on))
                    _activeTab = i;
                curX += r.width + 6f;
            }
            y += TabH + 6f;

            var body = new Rect(right.x + 6f, y, right.width - 12f, right.height - (y - right.y) - 6f);
            Widgets.BeginScrollView(body, ref _scrollRight, new Rect(0, 0, body.width - 16f, Math.Max(body.height, 600f)));
            var inner = new Rect(0, 0, body.width - 16f, Math.Max(body.height, 600f));

            switch (_activeTab)
            {
                case 0: DrawFixedPrompts(inner, pawnId, name); break;
                case 1: DrawBiography(inner, pawnId); break;
                case 2: DrawBeliefs(inner, pawnId, name); break;
            }

            Widgets.EndScrollView();
        }

        private void DrawFixedPrompts(Rect rect, string pawnId, string name)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), $"{name} 的固定提示词：");
            var cur = _fixedPrompts.GetByPawn(pawnId) ?? string.Empty;
            var newText = Widgets.TextArea(new Rect(rect.x, rect.y + 28f, rect.width - 160f, 80f), cur);
            if (newText != cur)
                _fixedPrompts.UpsertByPawn(pawnId, newText);
            if (Widgets.ButtonText(new Rect(rect.x + rect.width - 150f, rect.y + 28f, 60f, 24f), "清空"))
                _fixedPrompts.DeleteByPawn(pawnId);
        }

        private void DrawBiography(Rect rect, string pawnId)
        {
            float y = rect.y;
            if (Widgets.ButtonText(new Rect(rect.x, y, 100f, 24f), "新增段落"))
            {
                _bio.Add(pawnId, "");
            }
            y += 28f;
            var items = _bio.ListByPawn(pawnId).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                Widgets.Label(new Rect(rect.x, y, 120f, 24f), it.CreatedAt.ToString("HH:mm:ss"));
                var txt = Widgets.TextArea(new Rect(rect.x + 124f, y, rect.width - 260f, 60f), it.Text);
                if (txt != it.Text) _bio.Update(pawnId, it.Id, txt);
                if (Widgets.ButtonText(new Rect(rect.x + rect.width - 130f, y, 60f, 24f), "上移") && i > 0)
                    _bio.Reorder(pawnId, it.Id, i - 1);
                if (Widgets.ButtonText(new Rect(rect.x + rect.width - 65f, y, 60f, 24f), "删除"))
                    _bio.Remove(pawnId, it.Id);
                y += 70f;
            }
        }

        private void DrawBeliefs(Rect rect, string pawnId, string name)
        {
            var b = _beliefs.GetByPawn(pawnId) ?? new PersonalBeliefs("", "", "", "");
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), $"{name} 的观点与意识形态：");
            float y = rect.y + 28f;
            float labW = 110f;

            // 预设模板
            if (Widgets.ButtonText(new Rect(rect.x, y, 100f, 24f), "预设模板"))
            {
                var menu = new List<FloatMenuOption>
                {
                    new FloatMenuOption("生存主义者", () =>
                    {
                        _beliefs.UpsertByPawn(pawnId, new PersonalBeliefs(
                            "世界残酷，必须以实用为先以保集体生存",
                            "资源节约、效率优先、团队协作",
                            "在危机中优先保障弱者与关键岗位；绝不浪费弹药与粮食",
                            "冷静、务实、偶尔多疑，但绝不抛弃队友"));
                    }),
                    new FloatMenuOption("仁慈的管理者", () =>
                    {
                        _beliefs.UpsertByPawn(pawnId, new PersonalBeliefs(
                            "文明的火种应该被守护",
                            "公平、忠诚、同情与责任",
                            "先沟通与劝解，必要时果断出手，但避免无谓伤害",
                            "温和、坚定、富有同理心的协调者"));
                    }),
                    new FloatMenuOption("冷酷的现实主义者", () =>
                    {
                        _beliefs.UpsertByPawn(pawnId, new PersonalBeliefs(
                            "弱肉强食的边缘世界需要强硬手段",
                            "结果导向、秩序优先、代价可控",
                            "不做无意义牺牲；为殖民地长远利益作出艰难决策",
                            "理性、克制、略显冷酷，但极具执行力"));
                    })
                };
                Find.WindowStack.Add(new FloatMenu(menu));
            }
            y += 30f;

            Widgets.Label(new Rect(rect.x, y, labW, 24f), "世界观");
            var worldview = Widgets.TextArea(new Rect(rect.x + labW + 4f, y, rect.width - labW - 20f, 60f), b.Worldview);
            y += 66f;

            Widgets.Label(new Rect(rect.x, y, labW, 24f), "价值观");
            var values = Widgets.TextArea(new Rect(rect.x + labW + 4f, y, rect.width - labW - 20f, 60f), b.Values);
            y += 66f;

            Widgets.Label(new Rect(rect.x, y, labW, 24f), "行为准则");
            var code = Widgets.TextArea(new Rect(rect.x + labW + 4f, y, rect.width - labW - 20f, 60f), b.CodeOfConduct);
            y += 66f;

            Widgets.Label(new Rect(rect.x, y, labW, 24f), "人格特质");
            var traits = Widgets.TextArea(new Rect(rect.x + labW + 4f, y, rect.width - labW - 20f, 60f), b.TraitsText);
            y += 66f;

            if (Widgets.ButtonText(new Rect(rect.x, y, 100f, 24f), "保存/更新"))
            {
                _beliefs.UpsertByPawn(pawnId, new PersonalBeliefs(worldview, values, code, traits));
            }
            if (Widgets.ButtonText(new Rect(rect.x + 110f, y, 60f, 24f), "清空"))
            {
                _beliefs.DeleteByPawn(pawnId);
            }
        }
    }
}


