using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.History;
using RimAI.Core.Modules.World;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Core.Modules.Persona;

namespace RimAI.Core.UI.HistoryManager
{
    /// <summary>
    /// 历史管理窗口（P10-M3）。
    /// - 顶部选择/输入 convKey
    /// - 5 个 Tab：历史、前情提要、固定提示词、关联对话、人物传记
    /// - 历史 Tab 支持每条消息“修改/删除”轻量超链接式操作
    /// </summary>
    public class MainTabWindow_HistoryManager : Window
    {
        private readonly RimAI.Core.Services.IHistoryWriteService _historyWrite;
        private readonly IParticipantIdService _pid;
        private readonly IRecapService _recap;
        private readonly RimAI.Core.Infrastructure.Configuration.IConfigurationService _config;
        private readonly IFixedPromptService _fixedPrompts;
        private readonly IBiographyService _bio;

        private Vector2 _scrollMain = Vector2.zero;
        private int _activeTab = 0;
        private string _convKeyInput = string.Empty;
        private List<string> _allConvKeys = new();
        private string _selectedConversationId = string.Empty;
        private List<string> _convCandidates = new List<string>();
        private string _selectedConvKey = string.Empty; // 供下拉选择用
        private List<string> _menuCandidates = new List<string>(); // 供下拉选择用
        private string _selectedConversationIdMenu = string.Empty; // 供下拉选择用
        private List<ConversationEntry> _entries = new();
        private int _editingIndex = -1;
        private string _editBuffer = string.Empty;
        private bool _demoPrepared = false;

        private const float RowSpacing = 6f;
        private const float LinkSpacing = 12f;
        private const float HeaderRowHeight = 26f;
        private const float ControlGap = 8f;
        private const float RightButtonWidth = 100f;
        private const float HeaderLabelWidth = 80f;
        private const float TabHeight = 26f;
        private const float LeftMetaColWidth = 180f; // 时间+说话人列宽
        // 固定化历史内容行高，避免复杂计算导致的命中区域错位
        private const float HistoryRowHeight = 100f; // 包含：时间行(22) + 内容区(约60) + 链接区(18)

        public MainTabWindow_HistoryManager()
        {
            _historyWrite = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
            _pid = CoreServices.Locator.Get<IParticipantIdService>();
            _recap = CoreServices.Locator.Get<IRecapService>();
            _config = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
            _fixedPrompts = CoreServices.Locator.Get<IFixedPromptService>();
            _bio = CoreServices.Locator.Get<IBiographyService>();

            forcePause = false;
            draggable = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;

            // 初始加载
            _ = EnsureDemoAndReloadAsync();
        }

        public MainTabWindow_HistoryManager(string presetConvKey) : this()
        {
            if (!string.IsNullOrWhiteSpace(presetConvKey))
            {
                _demoPrepared = true; // 禁止自动生成 Demo
                _convKeyInput = HistoryUIState.CanonicalizeConvKey(presetConvKey);
                HistoryUIState.CurrentConvKey = _convKeyInput;
                _ = ReloadKeysAsync();
                _ = ReloadEntriesAsync();
            }
        }

        public override Vector2 InitialSize => new Vector2(1080f, 660f);

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            DrawHeader(inRect, ref y);
            y += 6f;
            DrawTabs(inRect, ref y);
            y += 6f;
            var bodyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y));
            DrawBody(bodyRect);
        }

        private void DrawHeader(Rect inRect, ref float y)
        {
            // Row 1: 会话键输入 + 右侧操作
            var row1 = new Rect(inRect.x, y, inRect.width, HeaderRowHeight);
            Widgets.Label(new Rect(row1.x, row1.y, HeaderLabelWidth, row1.height), "会话键");
            float textWidth = row1.width - HeaderLabelWidth - RightButtonWidth * 2 - ControlGap * 3;
            _convKeyInput = Widgets.TextField(new Rect(row1.x + HeaderLabelWidth, row1.y, textWidth, row1.height), _convKeyInput);
            if (Widgets.ButtonText(new Rect(row1.x + HeaderLabelWidth + textWidth + ControlGap, row1.y, RightButtonWidth, row1.height), "刷新"))
            {
                _ = ReloadKeysAsync();
            }
            if (Widgets.ButtonText(new Rect(row1.x + HeaderLabelWidth + textWidth + ControlGap + RightButtonWidth + ControlGap, row1.y, RightButtonWidth, row1.height), "加载"))
            {
                _ = LoadByConvKeyAsync(_convKeyInput);
            }
            y += HeaderRowHeight + RowSpacing;

            // Row 2: 参与者友好名（隐藏 mode:* 前缀）
            if (!string.IsNullOrWhiteSpace(_convKeyInput))
            {
                var parts = _convKeyInput.Split('|');
                var names = parts.Where(p => !p.StartsWith("mode:", StringComparison.Ordinal)).Select(p => _pid.GetDisplayName(p)).ToList();
                Widgets.Label(new Rect(inRect.x, y, inRect.width, HeaderRowHeight), $"参与者：{string.Join(", ", names)}");
                y += HeaderRowHeight + RowSpacing;
            }

            // 初始化下拉默认值（避免空状态下的 UI 抖动）
            if (string.IsNullOrWhiteSpace(_selectedConvKey))
            {
                _selectedConvKey = _allConvKeys.FirstOrDefault() ?? _convKeyInput;
                try { _menuCandidates = GetCandidatesByKeyAsync(_selectedConvKey).GetAwaiter().GetResult() ?? new List<string>(); }
                catch { _menuCandidates = new List<string>(); }
                _selectedConversationIdMenu = _menuCandidates.LastOrDefault() ?? string.Empty;
            }

            // Row 3: 下拉选择 convKey + 会话ID + 加载所选
            var row3 = new Rect(inRect.x, y, inRect.width, HeaderRowHeight);
            float btnWidthKey = Math.Min(420f, row3.width * 0.5f - ControlGap * 1.5f);
            float btnWidthCid = Math.Min(360f, row3.width * 0.35f - ControlGap * 1.5f);
            var keyBtn = new Rect(row3.x, row3.y, btnWidthKey, row3.height);
            var cidBtn = new Rect(keyBtn.xMax + ControlGap, row3.y, btnWidthCid, row3.height);
            var loadSelBtn = new Rect(cidBtn.xMax + ControlGap, row3.y, RightButtonWidth, row3.height);

            if (Widgets.ButtonText(keyBtn, string.IsNullOrWhiteSpace(_selectedConvKey) ? "选择会话键" : _selectedConvKey))
            {
                var menu = new List<FloatMenuOption>();
                foreach (var k in _allConvKeys)
                {
                    var show = k;
                    menu.Add(new FloatMenuOption(show, () =>
                    {
                        _selectedConvKey = k;
                        try
                        {
                            _menuCandidates = GetCandidatesByKeyAsync(k).GetAwaiter().GetResult() ?? new List<string>();
                        }
                        catch { _menuCandidates = new List<string>(); }
                        _selectedConversationIdMenu = _menuCandidates.LastOrDefault() ?? string.Empty;
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(menu));
            }

            if (Widgets.ButtonText(cidBtn, string.IsNullOrWhiteSpace(_selectedConversationIdMenu) ? "选择会话ID" : _selectedConversationIdMenu))
            {
                var menu2 = new List<FloatMenuOption>();
                foreach (var cid in _menuCandidates)
                {
                    var show = cid;
                    menu2.Add(new FloatMenuOption(show, () => { _selectedConversationIdMenu = cid; }));
                }
                Find.WindowStack.Add(new FloatMenu(menu2));
            }

            if (Widgets.ButtonText(loadSelBtn, "加载所选"))
            {
                if (!string.IsNullOrWhiteSpace(_selectedConvKey))
                {
                    _ = LoadByConvKeyAsync(_selectedConvKey, _selectedConversationIdMenu);
                }
            }
            y += HeaderRowHeight; // 末行不额外加 RowSpacing，交给调用方统一留白
        }

        private void DrawTabs(Rect inRect, ref float y)
        {
            var tabs = new[] { "历史记录", "前情提要", "关联对话" };
            float startY = y;
            float curX = inRect.x;
            for (int i = 0; i < tabs.Length; i++)
            {
                var label = tabs[i];
                var size = Text.CalcSize(label);
                var r = new Rect(curX, startY, size.x + 24f, TabHeight);
                bool on = _activeTab == i;
                if (Widgets.ButtonText(r, label, drawBackground: on))
                {
                    // 切换 Tab 时，确保 convKey 与 cid 与全局状态一致
                    var canon = HistoryUIState.CanonicalizeConvKey(_convKeyInput);
                    if (!string.Equals(canon, _convKeyInput, System.StringComparison.Ordinal))
                    {
                        _ = LoadByConvKeyAsync(canon);
                    }
                    else if (!string.IsNullOrWhiteSpace(HistoryUIState.CurrentConvKey) && !string.Equals(HistoryUIState.CurrentConvKey, _convKeyInput, System.StringComparison.Ordinal))
                    {
                        _ = LoadByConvKeyAsync(HistoryUIState.CurrentConvKey);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(HistoryUIState.CurrentConversationId) && HistoryUIState.CurrentConvKey == _convKeyInput)
                        {
                            _selectedConversationId = HistoryUIState.CurrentConversationId;
                        }
                        else
                        {
                            _ = ReloadEntriesAsync();
                        }
                    }
                    _activeTab = i;
                }
                curX += r.width + ControlGap;
            }
            // Tabs 下方分隔线，并下推 y，避免与正文重叠
            float bottomY = startY + TabHeight;
            Widgets.DrawLineHorizontal(inRect.x, bottomY + 2f, inRect.width);
            y = bottomY + 2f; // 让调用方在此基础上再加额外间距
        }

        private void DrawBody(Rect body)
        {
            switch (_activeTab)
            {
                case 0: DrawTabHistory(body); break;
                case 1: DrawTabRecap(body); break;
                case 2: DrawTabRelated(body); break;
            }
        }

        #region Tab1 历史记录（行内修改/删除）
        private void DrawTabHistory(Rect rect)
        {
            var entries = _entries ?? new List<ConversationEntry>();

            // 空状态提示
            if (entries.Count == 0)
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, HeaderRowHeight),
                    string.IsNullOrWhiteSpace(_convKeyInput)
                        ? "无历史对话。请输入或选择会话键后点击加载。"
                        : $"无历史对话：{_convKeyInput}");
                return;
            }

            // 固定化：视图高度 = 行数 * 固定行高 + 余量
            var viewH = Math.Max(rect.height - 8f, entries.Count * HistoryRowHeight + 16f);
            var viewRect = new Rect(0, 0, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scrollMain, viewRect);

            float curY = 0f;
            var oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                float rowY = curY;

                // 背景交替高亮（固定行高）
                if (i % 2 == 1)
                {
                    var backRect = new Rect(0, rowY, viewRect.width, HistoryRowHeight);
                    Widgets.DrawLightHighlight(backRect);
                }

                // 左列（时间+说话人）
                var leftRect = new Rect(0, rowY, LeftMetaColWidth, 22f);
                Widgets.Label(leftRect, $"[{e.Timestamp:HH:mm:ss}] {e.SpeakerId}");

                // 固定内容区（占据行内剩余空间，底部预留链接区 18f）
                float contentWidth = viewRect.width - LeftMetaColWidth - 12f;
                float linksHeight = 18f;
                var contentRect = new Rect(LeftMetaColWidth + 8f, rowY, contentWidth, HistoryRowHeight - linksHeight - 6f);

                if (_editingIndex == i)
                {
                    // 编辑状态：在内容区顶部放置输入框，避免高度膨胀
                    float editH = 24f;
                    var editRect = new Rect(contentRect.x, contentRect.y, contentWidth, editH);
                    _editBuffer = Widgets.TextField(editRect, _editBuffer ?? string.Empty);
                    // 下方显示原内容的预览（裁剪显示）
                    var previewRect = new Rect(contentRect.x, contentRect.y + editH + 2f, contentWidth, contentRect.height - editH - 2f);
                    Widgets.Label(previewRect, e.Content ?? string.Empty);
                }
                else
                {
                    // 正常状态：显示内容（裁剪在固定高度内，自动换行）
                    Widgets.Label(contentRect, e.Content ?? string.Empty);
                }

                // 固定链接区在行底部
                float linksY = rowY + HistoryRowHeight - linksHeight - 2f;
                float linkX = contentRect.x;
                if (_editingIndex == i)
                {
                    var saveRect = new Rect(linkX, linksY, 60f, linksHeight);
                    var cancelRect = new Rect(linkX + 60f + LinkSpacing, linksY, 60f, linksHeight);
                    if (LinkButton(saveRect, "保存"))
                    {
                        _ = SaveEditAsync(i, _editBuffer);
                    }
                    if (LinkButton(cancelRect, "取消"))
                    {
                        _editingIndex = -1;
                        _editBuffer = string.Empty;
                    }
                }
                else
                {
                    var editRect = new Rect(linkX, linksY, 40f, linksHeight);
                    var delRect = new Rect(linkX + 40f + LinkSpacing, linksY, 40f, linksHeight);
                    var undoRect = new Rect(linkX + 80f + LinkSpacing * 2, linksY, 60f, linksHeight);
                    if (LinkButton(editRect, "修改"))
                    {
                        _editingIndex = i;
                        _editBuffer = e.Content;
                    }
                    if (LinkButton(delRect, "删除"))
                    {
                        _ = DeleteAsync(i);
                    }
                    if (_lastDeletedEntry != null && _lastDeletedIndex >= 0 && LinkButton(undoRect, "撤销"))
                    {
                        _ = UndoDeleteAsync();
                    }
                }

                curY = rowY + HistoryRowHeight;
            }
            Text.WordWrap = oldWrap;

            Widgets.EndScrollView();
        }

        private async Task SaveEditAsync(int index, string newContent)
        {
            if (string.IsNullOrWhiteSpace(_selectedConversationId) || index < 0 || index >= _entries.Count) return;
            try
            {
                await _historyWrite.EditEntryAsync(_selectedConversationId, index, newContent ?? string.Empty);
                _editingIndex = -1;
                _editBuffer = string.Empty;
                await ReloadEntriesAsync();
            }
            catch (Exception ex)
            {
                Messages.Message("修改失败: " + ex.Message, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private ConversationEntry _lastDeletedEntry;
        private int _lastDeletedIndex = -1;

        private async Task DeleteAsync(int index)
        {
            if (string.IsNullOrWhiteSpace(_selectedConversationId) || index < 0 || index >= _entries.Count) return;
            try
            {
                // 记录撤销信息
                _lastDeletedEntry = _entries[index];
                _lastDeletedIndex = index;
                await _historyWrite.DeleteEntryAsync(_selectedConversationId, index);
                if (_editingIndex == index) { _editingIndex = -1; _editBuffer = string.Empty; }
                await ReloadEntriesAsync();
                // 提供可配置的撤销窗口
                var seconds = Math.Max(0, _config?.Current?.History?.UndoWindowSeconds ?? 0);
                if (seconds > 0)
                {
                    Messages.Message($"已删除，可在{seconds}秒内撤销", MessageTypeDefOf.TaskCompletion, false);
                }
                _ = Task.Run(async () =>
                {
                    await Task.Delay(Math.Max(0, (_config?.Current?.History?.UndoWindowSeconds ?? 0)) * 1000);
                    // 超时后清空撤销缓存
                    _lastDeletedEntry = null;
                    _lastDeletedIndex = -1;
                });
            }
            catch (Exception ex)
            {
                Messages.Message("删除失败: " + ex.Message, MessageTypeDefOf.RejectInput, historical: false);
            }
        }

        private async Task UndoDeleteAsync()
        {
            try
            {
                if (_lastDeletedEntry != null && _lastDeletedIndex >= 0 && !string.IsNullOrWhiteSpace(_selectedConversationId))
                {
                    await _historyWrite.RestoreEntryAsync(_selectedConversationId, _lastDeletedIndex, _lastDeletedEntry);
                    _lastDeletedEntry = null;
                    _lastDeletedIndex = -1;
                    await ReloadEntriesAsync();
                    Messages.Message("已撤销删除", MessageTypeDefOf.TaskCompletion, false);
                }
            }
            catch (Exception ex)
            {
                Messages.Message("撤销失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }
        #endregion

        #region Tab2 前情提要
        private void DrawTabRecap(Rect rect)
        {
            // 若未选择具体会话，尝试默认选中当前 convKey 下最新的 conversationId
            if (string.IsNullOrWhiteSpace(_selectedConversationId))
            {
                try
                {
                    var list = GetCandidatesByKeyAsync(_convKeyInput).GetAwaiter().GetResult();
                    _convCandidates = list?.ToList() ?? new List<string>();
                    _selectedConversationId = _convCandidates.LastOrDefault() ?? string.Empty;
                }
                catch { /* ignore */ }
            }
            var recapItems = _recap.GetRecapItems(_selectedConversationId).ToList();
            // 如果当前选中的会话没有任何前情提要，自动在该 convKey 下寻找最近一个“有前情”的会话并切换
            if ((recapItems == null || recapItems.Count == 0) && !string.IsNullOrWhiteSpace(_convKeyInput))
            {
                try
                {
                    if (_convCandidates == null || _convCandidates.Count == 0)
                    {
                        var list = GetCandidatesByKeyAsync(_convKeyInput).GetAwaiter().GetResult();
                        _convCandidates = list?.ToList() ?? new List<string>();
                    }
                    for (int i = _convCandidates.Count - 1; i >= 0; i--)
                    {
                        var cid = _convCandidates[i];
                        var items = _recap.GetRecapItems(cid);
                        if (items != null && items.Count > 0)
                        {
                            _selectedConversationId = cid;
                            recapItems = items.ToList();
                            break;
                        }
                    }
                }
                catch { /* ignore */ }
            }
            var viewH = Math.Max(rect.height - 8f, recapItems.Count * 48f + 60f);
            var viewRect = new Rect(0, 0, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scrollMain, viewRect);

            float y = 0f;
            foreach (var item in recapItems)
            {
                Widgets.Label(new Rect(0, y, viewRect.width, 20f), $"[{item.CreatedAt:HH:mm:ss}] 前情提要");
                y += 22f;
                var txt = Widgets.TextArea(new Rect(0, y, viewRect.width - 140f, 46f), item.Text);
                if (txt != item.Text)
                {
                        _recap.UpdateRecapItem(_selectedConversationId, item.Id, txt);
                }
                if (Widgets.ButtonText(new Rect(viewRect.width - 135f, y, 60f, 24f), "上移"))
                {
                    _recap.ReorderRecapItem(_selectedConversationId, item.Id, Math.Max(0, recapItems.FindIndex(i => i.Id == item.Id) - 1));
                }
                if (Widgets.ButtonText(new Rect(viewRect.width - 70f, y, 60f, 24f), "删除"))
                {
                    _recap.RemoveRecapItem(_selectedConversationId, item.Id);
                }
                y += 50f;
            }

            Widgets.EndScrollView();

            // 底部操作
            var btn = new Rect(rect.x, rect.yMax - 28f, 160f, 24f);
            if (Widgets.ButtonText(btn, "一键重述"))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _recap.RebuildRecapAsync(_selectedConversationId);
                        Messages.Message("已启动重述", MessageTypeDefOf.TaskCompletion, false);
                    }
                    catch (Exception ex)
                    {
                        Messages.Message("重述失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
                    }
                });
            }

            // 手动刷新按钮：在冷却/退化后，用户可主动刷新一次
            // 取消“刷新前情”按钮以避免用户连点造成重复条目；使用自动重述/后台叠加
        }

        
        #endregion

        #region Tab3 关联对话
        private void DrawTabRelated(Rect rect)
        {
            if (string.IsNullOrWhiteSpace(_convKeyInput))
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "请先加载会话键。");
                return;
            }

            var ids = _convKeyInput.Split('|');
            var relSvc = CoreServices.Locator.Get<RimAI.Core.Services.IRelationsIndexService>();
            var writer = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();

            var relatedSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pid in ids)
            {
                try
                {
                    var list = relSvc.ListConversationsByParticipantAsync(pid).GetAwaiter().GetResult();
                    foreach (var cid in list) relatedSet.Add(cid);
                }
                catch { /* ignore */ }
            }

            var relatedConvKeys = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var cid in relatedSet)
            {
                try
                {
                    var rec = writer.GetConversationAsync(cid).GetAwaiter().GetResult();
                    var key = string.Join("|", (rec.ParticipantIds ?? ids).OrderBy(x => x, StringComparer.Ordinal));
                    if (!string.Equals(key, _convKeyInput, StringComparison.Ordinal)) relatedConvKeys.Add(key);
                }
                catch { /* ignore */ }
            }

            float y = rect.y;
            foreach (var k in relatedConvKeys)
            {
                if (Widgets.ButtonText(new Rect(rect.x, y, 400f, 24f), k))
                {
                    _ = LoadByConvKeyAsync(k);
                }
                y += 28f;
            }
        }
        #endregion

        #region 工具与加载
        private bool LinkButton(Rect rect, string label)
        {
            var hovered = Mouse.IsOver(rect);
            var old = GUI.color;
            GUI.color = hovered ? new Color(0.2f, 0.6f, 1f) : new Color(0.25f, 0.5f, 0.9f);
            Widgets.Label(rect, label);
            var size = Text.CalcSize(label);
            Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, size.x);
            bool clicked = Widgets.ButtonInvisible(rect, true);
            GUI.color = old;
            return clicked;
        }

        private async Task EnsureDemoAndReloadAsync()
        {
            try
            {
                await ReloadKeysAsync();
                if (_allConvKeys.Count == 0 && !_demoPrepared)
                {
                    _demoPrepared = true;
                    // 生成 Demo 数据（1v1）
                    var player = _pid.GetPlayerId();
                    var pawn = "pawn:DEMO";
                    var ids = new List<string> { player, pawn };
                    var now = DateTime.UtcNow;
                    var convId = _historyWrite.CreateConversation(ids);
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(player, "你好，我是玩家。", now.AddSeconds(1)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(pawn, "你好，我是殖民地总督助手。", now.AddSeconds(2)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(player, "帮我汇总一下殖民地现状。", now.AddSeconds(3)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(pawn, "目前有 5 名殖民者，粮食储备 12 天。", now.AddSeconds(4)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(player, "好的，安排明日播种。", now.AddSeconds(5)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(pawn, "已记录：明日优先播种。", now.AddSeconds(6)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(player, "谢谢。", now.AddSeconds(7)));
                    await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(pawn, "随时效劳。", now.AddSeconds(8)));
                    // 触发 recap 回放
                    // 使用刚创建的第一个会话作为重建目标
                    await _recap.RebuildRecapAsync(convId);

                    // 生成一个交集会话
                    var ids2 = new List<string> { player, "pawn:ALLY" };
                    var convId2 = _historyWrite.CreateConversation(ids2);
                    await _historyWrite.AppendEntryAsync(convId2, new ConversationEntry(player, "欢迎加入我们。", now.AddSeconds(9)));
                    await _historyWrite.AppendEntryAsync(convId2, new ConversationEntry("pawn:ALLY", "很高兴来到殖民地。", now.AddSeconds(10)));

                    await ReloadKeysAsync();
                }
                // 默认加载第一个
                if (_allConvKeys.Count > 0)
                {
                    _convKeyInput = _allConvKeys[0];
                    await ReloadEntriesAsync();
                }
            }
            catch (Exception ex)
            {
                Messages.Message("初始化失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }

        private async Task ReloadKeysAsync()
        {
            try
            {
                var writer = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                var state = writer.GetV2StateForPersistence();
                var keys = state?.ConvKeyIndex?.Keys?.ToList() ?? new List<string>();
                keys.Sort(StringComparer.Ordinal);
                _allConvKeys = keys;
            }
            catch
            {
                _allConvKeys = _allConvKeys ?? new List<string>();
            }
            await Task.CompletedTask;
        }

        private async Task LoadByConvKeyAsync(string convKey, string preferredConversationId = null)
        {
            var typed = convKey ?? string.Empty;
            var canon = HistoryUIState.CanonicalizeConvKey(typed);
            _convKeyInput = typed; // 保留用户输入原样
            HistoryUIState.CurrentConvKey = _convKeyInput;
            // 立即清空当前选择与记录，防止在异步加载期间残留旧数据（例如仍显示上一次的 ALLY 会话）
            _entries = new List<ConversationEntry>();
            HistoryUIState.CurrentConversationId = string.Empty;
            _selectedConversationId = string.Empty;
            _convCandidates.Clear();
            try
            {
                _convCandidates = await FindCandidatesWithFallbackAsync(_convKeyInput);

                if (_convCandidates.Count == 0)
                {
                    // 未找到任何会话：清空当前视图并提示
                    _selectedConversationId = string.Empty;
                    HistoryUIState.CurrentConversationId = string.Empty;
                    _entries = new List<ConversationEntry>();
                    Messages.Message($"未找到该会话的历史记录：{_convKeyInput}", MessageTypeDefOf.RejectInput, false);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(preferredConversationId) && _convCandidates.Contains(preferredConversationId))
                        _selectedConversationId = preferredConversationId;
                    else
                        _selectedConversationId = _convCandidates.LastOrDefault() ?? string.Empty; // 默认选择最新会话
                    HistoryUIState.CurrentConversationId = _selectedConversationId;
                }
            }
            catch { /* ignore */ }
            await ReloadEntriesAsync();
        }

        private async Task<List<string>> GetCandidatesByKeyAsync(string convKey)
        {
            return await FindCandidatesWithFallbackAsync(convKey);
        }

        private async Task ReloadEntriesAsync()
        {
            _editingIndex = -1;
            _editBuffer = string.Empty;

            if (string.IsNullOrWhiteSpace(_convKeyInput))
            {
                _entries = new List<ConversationEntry>();
                return;
            }
            if (string.IsNullOrWhiteSpace(_selectedConversationId))
            {
                try
                {
                    _convCandidates = await FindCandidatesWithFallbackAsync(_convKeyInput);
                    _selectedConversationId = _convCandidates.LastOrDefault() ?? string.Empty; // 默认选择最新会话
                    HistoryUIState.CurrentConversationId = _selectedConversationId;
                }
                catch { /* ignore */ }
            }
            if (!string.IsNullOrWhiteSpace(_selectedConversationId))
            {
                try
                {
                    var rec = await _historyWrite.GetConversationAsync(_selectedConversationId);
                    _entries = rec?.Entries?.ToList() ?? new List<ConversationEntry>();
                    HistoryUIState.CurrentConversationId = _selectedConversationId;
                }
                catch { _entries = new List<ConversationEntry>(); }
            }
            else
            {
                // 当前无选中会话：清空并提示（仅当 convKey 非空时提示一次）
                _entries = new List<ConversationEntry>();
                if (!string.IsNullOrWhiteSpace(_convKeyInput))
                {
                    Messages.Message($"未找到该会话的历史记录：{_convKeyInput}", MessageTypeDefOf.RejectInput, false);
                }
            }
        }

        private async Task<List<string>> FindCandidatesWithFallbackAsync(string convKey)
        {
            var typed = convKey ?? string.Empty;
            var canon = HistoryUIState.CanonicalizeConvKey(typed);
            // 1) 先用原样键，再用规范化键
            var list = await _historyWrite.FindByConvKeyAsync(typed);
            var cands = list?.ToList() ?? new List<string>();
            if (cands.Count == 0 && !string.Equals(typed, canon, StringComparison.Ordinal))
            {
                var list2 = await _historyWrite.FindByConvKeyAsync(canon);
                cands = list2?.ToList() ?? new List<string>();
            }
            // 2) 仍为空则回退到“参与者交集”
            if (cands.Count == 0)
            {
                var ids = canon.Split('|');
                HashSet<string> inter = null;
                foreach (var pid in ids)
                {
                    if (string.IsNullOrWhiteSpace(pid)) { inter = null; break; }
                    var setForPid = new HashSet<string>(StringComparer.Ordinal);
                    try { foreach (var c in await _historyWrite.ListByParticipantAsync(pid)) setForPid.Add(c); } catch { }
                    if (pid.StartsWith("player:", StringComparison.Ordinal))
                    {
                        // 兼容旧档 player:__SAVE__
                        try { foreach (var c in await _historyWrite.ListByParticipantAsync("player:__SAVE__")) setForPid.Add(c); } catch { }
                    }
                    if (inter == null) inter = setForPid;
                    else inter.IntersectWith(setForPid);
                    if (inter.Count == 0) break;
                }
                cands = inter?.ToList() ?? new List<string>();
            }
            return cands;
        }
        #endregion
    }
}


