using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;
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
        private readonly IHistoryQueryService _historyQuery;
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
        private List<ConversationEntry> _entries = new();
        private int _editingIndex = -1;
        private string _editBuffer = string.Empty;
        private bool _demoPrepared = false;

        private const float Padding = 10f;
        private const float RowSpacing = 6f;
        private const float LinkSpacing = 12f;

        public MainTabWindow_HistoryManager()
        {
            _historyQuery = CoreServices.Locator.Get<IHistoryQueryService>();
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
                _convKeyInput = CanonicalizeConvKey(presetConvKey);
                HistoryUIState.CurrentConvKey = _convKeyInput;
                _ = ReloadKeysAsync();
                _ = ReloadEntriesAsync();
            }
        }

        public override Vector2 InitialSize => new Vector2(1000f, 640f);

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
            // 加载键输入与选择
            Widgets.Label(new Rect(inRect.x, y, 80f, 24f), "会话键");
            _convKeyInput = Widgets.TextField(new Rect(inRect.x + 80f, y, inRect.width - 300f, 24f), _convKeyInput);
            if (Widgets.ButtonText(new Rect(inRect.x + inRect.width - 210f, y, 80f, 24f), "刷新"))
            {
                _ = ReloadKeysAsync();
            }
            if (Widgets.ButtonText(new Rect(inRect.x + inRect.width - 120f, y, 100f, 24f), "加载"))
            {
                _ = LoadByConvKeyAsync(_convKeyInput);
            }
            y += 28f;

            // 下拉快速选择现有 convKey
            string selected = _allConvKeys.FirstOrDefault(k => k == _convKeyInput) ?? (_allConvKeys.Count > 0 ? _allConvKeys[0] : string.Empty);
            if (!string.IsNullOrEmpty(selected))
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y, inRect.width - 300f, 24f), selected))
                {
                    var menu = new List<FloatMenuOption>();
                    foreach (var k in _allConvKeys)
                    {
                        var show = k;
                        menu.Add(new FloatMenuOption(show, () => { _convKeyInput = k; _ = LoadByConvKeyAsync(k); }));
                    }
                    Find.WindowStack.Add(new FloatMenu(menu));
                }
            }

            // conversationId 选择器（V2）
            if (_convCandidates != null && _convCandidates.Count > 0)
            {
                if (Widgets.ButtonText(new Rect(inRect.x, y + 28f, inRect.width - 300f, 24f), _selectedConversationId ?? string.Empty))
                {
                    var menu2 = new List<FloatMenuOption>();
                    foreach (var cid in _convCandidates)
                    {
                        var show = cid;
                        menu2.Add(new FloatMenuOption(show, () => { _selectedConversationId = cid; _ = ReloadEntriesAsync(); }));
                    }
                    Find.WindowStack.Add(new FloatMenu(menu2));
                }
                y += 28f;
            }

            if (!string.IsNullOrWhiteSpace(_convKeyInput))
            {
                var parts = _convKeyInput.Split('|');
                var names = parts.Select(p => _pid.GetDisplayName(p)).ToList();
                Widgets.Label(new Rect(inRect.x, y + 28f, inRect.width, 24f), $"参与者：{string.Join(", ", names)}");
            }
            y += 56f;
        }

        private void DrawTabs(Rect inRect, ref float y)
        {
            var tabs = new[] { "历史记录", "前情提要", "固定提示词", "关联对话", "人物传记" };
            float curX = inRect.x;
            for (int i = 0; i < tabs.Length; i++)
            {
                var label = tabs[i];
                var size = Text.CalcSize(label);
                var r = new Rect(curX, y, size.x + 20f, 24f);
                bool on = _activeTab == i;
                if (Widgets.ButtonText(r, label, drawBackground: on))
                {
                    // 切换 Tab 时，确保 convKey 与 cid 与全局状态一致
                    var canon = CanonicalizeConvKey(_convKeyInput);
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
                        // 同步当前选择的 cid（若不同步则尝试根据convKey刷新）
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
                curX += r.width + 8f;
            }
        }

        private void DrawBody(Rect body)
        {
            switch (_activeTab)
            {
                case 0: DrawTabHistory(body); break;
                case 1: DrawTabRecap(body); break;
                case 2: DrawTabFixedPrompts(body); break;
                case 3: DrawTabRelated(body); break;
                case 4: DrawTabBiography(body); break;
            }
        }

        #region Tab1 历史记录（行内修改/删除）
        private void DrawTabHistory(Rect rect)
        {
            var pageSize = Math.Max(1, _config?.Current?.History?.HistoryPageSize ?? 100);
            var entries = _entries ?? new List<ConversationEntry>();

            var viewH = Math.Max(rect.height - 8f, entries.Count * 46f + 40f);
            var viewRect = new Rect(0, 0, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scrollMain, viewRect);

            float y = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var msgRect = new Rect(0, y, viewRect.width, 22f);
                Widgets.Label(msgRect, $"[{e.Timestamp:HH:mm:ss}] {e.SpeakerId}: {e.Content}");

                // 第二行：轻量链接按钮
                y += 22f;
                float linkX = msgRect.x + 8f;
                if (_editingIndex == i)
                {
                    // 行内编辑器
                    var editRect = new Rect(linkX, y, viewRect.width - 16f, 22f);
                    _editBuffer = Widgets.TextField(editRect, _editBuffer ?? string.Empty);

                    // 保存 / 取消（链接样式）
                    var saveRect = new Rect(linkX, y + 24f, 60f, 18f);
                    var cancelRect = new Rect(linkX + 60f + LinkSpacing, y + 24f, 60f, 18f);
                    if (LinkButton(saveRect, "保存"))
                    {
                        _ = SaveEditAsync(i, _editBuffer);
                    }
                    if (LinkButton(cancelRect, "取消"))
                    {
                        _editingIndex = -1;
                        _editBuffer = string.Empty;
                    }
                    y += 24f + 22f + RowSpacing;
                }
                else
                {
                    var editRect = new Rect(linkX, y, 40f, 18f);
                    var delRect = new Rect(linkX + 40f + LinkSpacing, y, 40f, 18f);
                    var undoRect = new Rect(linkX + 80f + LinkSpacing * 2, y, 60f, 18f);
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
                    y += 18f + RowSpacing;
                }
            }

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
                    var list = _historyWrite.FindByConvKeyAsync(_convKeyInput).GetAwaiter().GetResult();
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
                        var list = _historyWrite.FindByConvKeyAsync(_convKeyInput).GetAwaiter().GetResult();
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

        #region Tab3 固定提示词（MVP: 内存演示）
        private void DrawTabFixedPrompts(Rect rect)
        {
            if (string.IsNullOrWhiteSpace(_convKeyInput))
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "请先加载会话键。");
                return;
            }
            var ids = _convKeyInput.Split('|');
            float y = rect.y;
            var convKey = _convKeyInput;
            foreach (var pid in ids)
            {
                Widgets.Label(new Rect(rect.x, y, 300f, 24f), _pid.GetDisplayName(pid));
                y += 24f;
                // 优先 convKey 覆盖；否则按 pawnId 主存
                var cur = _fixedPrompts.GetConvKeyOverride(convKey);
                if (string.IsNullOrWhiteSpace(cur) && pid.StartsWith("pawn:", StringComparison.Ordinal))
                    cur = _fixedPrompts.GetByPawn(pid) ?? string.Empty;
                var newText = Widgets.TextArea(new Rect(rect.x, y, rect.width - 160f, 60f), cur);
                if (newText != cur)
                {
                    if (!string.IsNullOrWhiteSpace(_fixedPrompts.GetConvKeyOverride(convKey)))
                        _fixedPrompts.UpsertConvKeyOverride(convKey, newText);
                    else if (pid.StartsWith("pawn:", StringComparison.Ordinal))
                        _fixedPrompts.UpsertByPawn(pid, newText);
                }
                if (Widgets.ButtonText(new Rect(rect.x + rect.width - 150f, y, 60f, 24f), "清空"))
                {
                    if (!string.IsNullOrWhiteSpace(_fixedPrompts.GetConvKeyOverride(convKey)))
                        _fixedPrompts.DeleteConvKeyOverride(convKey);
                    else if (pid.StartsWith("pawn:", StringComparison.Ordinal))
                        _fixedPrompts.DeleteByPawn(pid);
                }
                y += 70f;
            }
        }
        #endregion

        #region Tab4 关联对话
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

        #region Tab5 人物传记（MVP 文本）
        private void DrawTabBiography(Rect rect)
        {
            if (string.IsNullOrWhiteSpace(_convKeyInput))
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "请先加载会话键。");
                return;
            }
            var ids = _convKeyInput.Split('|');
            if (ids.Length != 2 || !(ids[0].StartsWith("player:") || ids[1].StartsWith("player:")) || !(ids[0].StartsWith("pawn:") || ids[1].StartsWith("pawn:")))
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "仅在 1v1（player↔pawn）场景可用。");
                return;
            }
            string playerId = ids.First(x => x.StartsWith("player:"));
            string pawnId = ids.First(x => x.StartsWith("pawn:"));
            string convKey = _convKeyInput;

            float y = rect.y;
            if (Widgets.ButtonText(new Rect(rect.x, y, 100f, 24f), "新增段落"))
            {
                _bio.Add(pawnId, "");
            }
            y += 28f;

            // 仅在 1v1 player↔pawn 场景展示指定 pawn 的传记
            var items = string.IsNullOrWhiteSpace(pawnId) ? new List<BiographyItem>() : _bio.ListByPawn(pawnId).ToList();
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                Widgets.Label(new Rect(rect.x, y, 120f, 24f), it.CreatedAt.ToString("HH:mm:ss"));
                var txt = Widgets.TextArea(new Rect(rect.x + 124f, y, rect.width - 260f, 60f), it.Text);
                if (txt != it.Text)
                {
                    _bio.Update(pawnId, it.Id, txt);
                }
                if (Widgets.ButtonText(new Rect(rect.x + rect.width - 130f, y, 60f, 24f), "上移") && i > 0)
                {
                    _bio.Reorder(pawnId, it.Id, i - 1);
                }
                if (Widgets.ButtonText(new Rect(rect.x + rect.width - 65f, y, 60f, 24f), "删除"))
                {
                    _bio.Remove(pawnId, it.Id);
                }
                y += 70f;
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

        private async Task LoadByConvKeyAsync(string convKey)
        {
            _convKeyInput = CanonicalizeConvKey(convKey ?? string.Empty);
            HistoryUIState.CurrentConvKey = _convKeyInput;
            _selectedConversationId = string.Empty;
            _convCandidates.Clear();
            try
            {
                var list = await _historyWrite.FindByConvKeyAsync(_convKeyInput);
                _convCandidates = list?.ToList() ?? new List<string>();

                // 回退：若按 convKey 未找到，则尝试按参与者列举（兼容 player id 变更或顺序问题）
                if (_convCandidates.Count == 0)
                {
                    var ids = _convKeyInput.Split('|');
                    var union = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var pid in ids)
                    {
                        if (string.IsNullOrWhiteSpace(pid)) continue;
                        if (pid.StartsWith("player:", StringComparison.Ordinal))
                        {
                            // 兼容旧档：尝试 __SAVE__ 作为备选
                            try { foreach (var c in await _historyWrite.ListByParticipantAsync("player:__SAVE__")) union.Add(c); } catch { }
                        }
                        try { foreach (var c in await _historyWrite.ListByParticipantAsync(pid)) union.Add(c); } catch { }
                    }
                    _convCandidates = union.ToList();
                }

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
                    _selectedConversationId = _convCandidates.LastOrDefault() ?? string.Empty; // 默认选择最新会话
                    HistoryUIState.CurrentConversationId = _selectedConversationId;
                }
            }
            catch { /* ignore */ }
            await ReloadEntriesAsync();
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
                    var list = await _historyWrite.FindByConvKeyAsync(_convKeyInput);
                    _convCandidates = list?.ToList() ?? new List<string>();
                    if (_convCandidates.Count == 0)
                    {
                        // 同 LoadByConvKey 回退逻辑
                        var ids = _convKeyInput.Split('|');
                        var union = new HashSet<string>(StringComparer.Ordinal);
                        foreach (var pid in ids)
                        {
                            if (string.IsNullOrWhiteSpace(pid)) continue;
                            if (pid.StartsWith("player:", StringComparison.Ordinal))
                            {
                                try { foreach (var c in await _historyWrite.ListByParticipantAsync("player:__SAVE__")) union.Add(c); } catch { }
                            }
                            try { foreach (var c in await _historyWrite.ListByParticipantAsync(pid)) union.Add(c); } catch { }
                        }
                        _convCandidates = union.ToList();
                    }
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

        private static string CanonicalizeConvKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var ids = key.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            return string.Join("|", ids.OrderBy(x => x, StringComparer.Ordinal));
        }
        #endregion
    }
}


