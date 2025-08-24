using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.UI.ChatWindow; // ChatConversationState

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
    internal sealed class ServerHistoryManagerTabView
    {
    private enum HistorySubTab { Thread, Recaps, RawJson }
        private HistorySubTab _subTab = HistorySubTab.Thread;
        private Vector2 _scrollThread = Vector2.zero;
        private Vector2 _scrollRecaps = Vector2.zero;
        private Vector2 _scrollRaw = Vector2.zero;
        private string _rawAllText;
        private bool _rawLoaded;
        private bool _recapGenerating = false;

        private sealed class HistoryEntryVM { public string Id; public string ConvKey; public EntryRole Role; public string Content; public DateTime TimestampUtc; public bool IsEditing; public string EditText; }
        private List<HistoryEntryVM> _entries;
        private sealed class RecapVM { public string Id; public string Text; public bool IsEditing; public string EditText; public string Range; public DateTime UpdatedAtUtc; }
        private List<RecapVM> _recaps;
        // Recap 实时刷新订阅
        private IRecapService _recapHooked;
        private string _recapHookedConvKey;
        private bool _recapDirty;
        private Action<string, string> _recapHandler;
        // 显示名缓存：convKey → (playerTitle, serverName)
        private readonly Dictionary<string, string> _convUserName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _convServerName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _nameResolving = new HashSet<string>(StringComparer.Ordinal);

        public void Draw(Rect inRect, ChatConversationState state, IHistoryService history, IRecapService recap, Action<string> switchToConvKey)
        {
            var prevFont = Text.Font; Text.Font = GameFont.Small;
            float tabsH = 28f; float sp = 6f; float btnW = 110f;
            var rTabs = new Rect(inRect.x, inRect.y, inRect.width, tabsH);
            if (Widgets.ButtonText(new Rect(rTabs.x, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Thread".Translate())) _subTab = HistorySubTab.Thread;
            if (Widgets.ButtonText(new Rect(rTabs.x + btnW + sp, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Recaps".Translate())) _subTab = HistorySubTab.Recaps;
            if (Widgets.ButtonText(new Rect(rTabs.x + (btnW + sp) * 2, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.RawJson".Translate())) { _subTab = HistorySubTab.RawJson; _rawLoaded = false; _rawAllText = string.Empty; }

            var contentRect = new Rect(inRect.x, rTabs.yMax + 8f, inRect.width, inRect.height - tabsH - 12f);

            switch (_subTab)
            {
                case HistorySubTab.Thread:
                    EnsureHistoryLoaded(history, state.ConvKey);
                    DrawThread(contentRect, history, state.ConvKey);
                    break;
                case HistorySubTab.Recaps:
                    EnsureRecapsLoaded(recap, state.ConvKey);
                    DrawRecaps(contentRect, recap, state.ConvKey);
                    break;
                case HistorySubTab.RawJson:
                    DrawRawJson(contentRect, history, state.ConvKey);
                    break;
            }
            Text.Font = prevFont;
        }

        private void EnsureHistoryLoaded(IHistoryService history, string convKey)
        {
            if (_entries != null) return;
            try
            {
                var thread = history.GetThreadAsync(convKey, 1, 200).GetAwaiter().GetResult();
                _entries = new List<HistoryEntryVM>();
                if (thread?.Entries != null)
                {
                    foreach (var e in thread.Entries)
                    {
                        if (e == null || e.Deleted) continue;
                        _entries.Add(new HistoryEntryVM { Id = e.Id, ConvKey = convKey, Role = e.Role, Content = e.Content, TimestampUtc = e.Timestamp, IsEditing = false, EditText = e.Content });
                    }
                }
            }
            catch { _entries = new List<HistoryEntryVM>(); }
        }

        private void ReloadHistory(IHistoryService history, string convKey)
        {
            _entries = null; EnsureHistoryLoaded(history, convKey);
            _rawLoaded = false; _rawAllText = string.Empty;
        }

        public void ForceReloadHistory(IHistoryService history, string convKey)
        {
            ReloadHistory(history, convKey);
        }

        private void DrawThread(Rect rect, IHistoryService history, string convKey)
        {
            var prev = Text.Font; Text.Font = GameFont.Small;
            float totalH = 4f;
            float actionsWForMeasure = 200f;
            float contentWForMeasure = (rect.width - 16f) - actionsWForMeasure - 16f;
            var names = GetOrBeginResolveNames(history, convKey);
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var it = _entries[i];
                    var measureRect = new Rect(0f, 0f, contentWForMeasure, 99999f);
                    string senderNameMeasure = it.Role == EntryRole.User ? names.userName : names.serverName;
                    string labelForMeasure = (senderNameMeasure ?? string.Empty) + ": " + (it.Content ?? string.Empty);
                    float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(labelForMeasure, measureRect.width));
                    float rowH = contentH + 12f;
                    totalH += rowH + 6f;
                }
            }
            var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
            Widgets.BeginScrollView(rect, ref _scrollThread, viewRect);
            float y = 4f;
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Count; i++)
                {
                    var it = _entries[i];
                    float actionsW = 200f;
                    float contentW = viewRect.width - actionsW - 16f;
                    var contentMeasureRect = new Rect(0f, 0f, contentW, 99999f);
                    string senderName = it.Role == EntryRole.User ? names.userName : names.serverName;
                    string label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
                    float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, contentMeasureRect.width)) : Mathf.Max(24f, Text.CalcHeight(label, contentMeasureRect.width));
                    float rowH = contentH + 12f;
                    var row = new Rect(0f, y, viewRect.width, rowH);
                    Widgets.DrawHighlightIfMouseover(row);
                    var contentRect = new Rect(row.x + 6f, row.y + 6f, contentW, contentH);
                    var actionsRect = new Rect(row.xMax - (actionsW + 10f), row.y + 8f, actionsW, 28f);
                    label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
                    if (!it.IsEditing)
                    {
                        Widgets.Label(contentRect, label);
                        if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Content; }
                        if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { _ = DeleteEntryAsync(history, it.ConvKey ?? convKey, it.Id); }
                    }
                    else
                    {
                        it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
                        if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { _ = SaveEntryAsync(history, it.ConvKey ?? convKey, it.Id, it.EditText); }
                        if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Content; }
                    }
                    y += rowH + 6f;
                }
            }
            Widgets.EndScrollView();
            Text.Font = prev;
        }

        private async Task SaveEntryAsync(IHistoryService history, string convKey, string entryId, string text)
        {
            try { var ok = await history.EditEntryAsync(convKey, entryId, text); if (!ok) Verse.Log.Warning("[RimAI.Core][SCW] EditEntryAsync failed"); }
            catch (Exception ex) { Verse.Log.Warning($"[RimAI.Core][SCW] EditEntryAsync error: {ex.Message}"); }
            finally { ReloadHistory(history, convKey); }
        }

        private async Task DeleteEntryAsync(IHistoryService history, string convKey, string entryId)
        {
            try { var ok = await history.DeleteEntryAsync(convKey, entryId); if (!ok) Verse.Log.Warning("[RimAI.Core][SCW] DeleteEntryAsync failed"); }
            catch (Exception ex) { Verse.Log.Warning($"[RimAI.Core][SCW] DeleteEntryAsync error: {ex.Message}"); }
            finally { ReloadHistory(history, convKey); }
        }

        private void EnsureRecapsLoaded(IRecapService recap, string convKey)
        {
            if (_recaps != null) return;
            try
            {
                EnsureRecapEventHooked(recap, convKey);
                var items = recap.GetRecaps(convKey) ?? Array.Empty<RecapItem>();
                _recaps = new List<RecapVM>();
                foreach (var r in items)
                {
                    string range = $"{r.FromTurnExclusive + 1}..{r.ToTurnInclusive}";
                    _recaps.Add(new RecapVM { Id = r.Id, Text = r.Text, IsEditing = false, EditText = r.Text, Range = range, UpdatedAtUtc = r.UpdatedAt });
                }
            }
            catch { _recaps = new List<RecapVM>(); }
        }

        private void ReloadRecaps(IRecapService recap, string convKey)
        {
            _recaps = null; EnsureRecapsLoaded(recap, convKey);
        }

        public void ForceReloadRecaps(IRecapService recap, string convKey)
        {
            ReloadRecaps(recap, convKey);
        }

        private void DrawRecaps(Rect rect, IRecapService recap, string convKey)
        {
            var prev = Text.Font; Text.Font = GameFont.Small;
            EnsureRecapEventHooked(recap, convKey);
            if (_recapDirty)
            {
                ReloadRecaps(recap, convKey);
                _recapDirty = false;
            }
            float totalH = 4f;
            float actionsW = 200f;
            float contentW = (rect.width - 16f) - actionsW - 16f;
            if (_recaps != null)
            {
                totalH += 34f;
                for (int i = 0; i < _recaps.Count; i++)
                {
                    var it = _recaps[i];
                    var measureRect = new Rect(0f, 0f, contentW, 99999f);
                    float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
                    float headerH = 28f;
                    float headerPad = 2f;
                    float rowH = headerH + headerPad + contentH + 12f;
                    totalH += rowH + 6f;
                }
            }
            var viewRect = new Rect(0f, 0f, rect.width - 16f, Math.Max(rect.height, totalH));
            Widgets.BeginScrollView(rect, ref _scrollRecaps, viewRect);
            float y = 4f;
            if (_recaps != null)
            {
                if (!_recapGenerating && Widgets.ButtonText(new Rect(viewRect.x, y, 160f, 28f), "RimAI.ChatUI.Recap.Generate".Translate()))
                {
                    _recapGenerating = true;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try { await recap.GenerateManualAsync(convKey); }
                        catch (Exception ex) { try { Verse.Log.Warning($"[RimAI.Core][SCW] Manual recap failed conv={convKey}: {ex.Message}"); } catch { } }
                        finally { _recapGenerating = false; ReloadRecaps(recap, convKey); }
                    });
                }
                if (_recapGenerating)
                {
                    Widgets.Label(new Rect(viewRect.x + 170f, y + 4f, 200f, 22f), "RimAI.ChatUI.Recap.Generating".Translate());
                }
                y += 34f;
                for (int i = 0; i < _recaps.Count; i++)
                {
                    var it = _recaps[i];
                    var measureRect = new Rect(0f, 0f, contentW, 99999f);
                    float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
                    float headerH = 28f;
                    float headerPad = 2f;
                    float rowH = headerH + headerPad + contentH + 12f;
                    var row = new Rect(0f, y, viewRect.width, rowH);
                    Widgets.DrawHighlightIfMouseover(row);
                    var headerRect = new Rect(row.x + 6f, row.y + 6f, contentW, headerH);
                    var contentRect = new Rect(row.x + 6f, headerRect.yMax + headerPad, contentW, contentH);
                    var actionsRect = new Rect(row.xMax - 210f, row.y + 8f, 200f, row.height - 16f);
                    Widgets.Label(headerRect, $"[{it.Range}] {it.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
                    if (!it.IsEditing)
                    {
                        var body = string.IsNullOrWhiteSpace(it.Text) ? "RimAI.Common.Empty".Translate().ToString() : it.Text;
                        Widgets.Label(contentRect, body);
                        if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Text; }
                        if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { if (!recap.DeleteRecap(convKey, it.Id)) Verse.Log.Warning("[RimAI.Core][SCW] DeleteRecap failed"); else ReloadRecaps(recap, convKey); }
                    }
                    else
                    {
                        it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
                        if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { if (!recap.UpdateRecap(convKey, it.Id, it.EditText ?? string.Empty)) Verse.Log.Warning("[RimAI.Core][SCW] UpdateRecap failed"); else { it.Text = it.EditText; it.IsEditing = false; } }
                        if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Text; }
                    }
                    y += rowH + 6f;
                }
            }
            Widgets.EndScrollView();
            Text.Font = prev;
        }

    private void EnsureRecapEventHooked(IRecapService recap, string convKey)
        {
            try
            {
                if (recap == null || string.IsNullOrWhiteSpace(convKey)) return;
                if (ReferenceEquals(_recapHooked, recap) && string.Equals(_recapHookedConvKey, convKey, StringComparison.Ordinal)) return;
                TryUnhookRecapEvent();
                _recapHooked = recap;
                _recapHookedConvKey = convKey;
                _recapHandler = (ck, id) => { if (string.Equals(ck, _recapHookedConvKey, StringComparison.Ordinal)) _recapDirty = true; };
                _recapHooked.OnRecapUpdated += _recapHandler;
            }
            catch { }
        }

    private void TryUnhookRecapEvent()
        {
            try
            {
                if (_recapHooked != null && _recapHandler != null)
                {
                    _recapHooked.OnRecapUpdated -= _recapHandler;
                }
            }
            catch { }
            finally
            {
                _recapHooked = null;
                _recapHandler = null;
                _recapHookedConvKey = null;
                _recapDirty = false;
            }
        }

        private void DrawRawJson(Rect rect, IHistoryService history, string convKey)
        {
            var prev = Text.Font; Text.Font = GameFont.Small;
            if (!_rawLoaded)
            {
                _rawLoaded = true;
                _rawAllText = string.Empty;
                try
                {
                    var list = history.GetAllEntriesRawAsync(convKey).GetAwaiter().GetResult();
                    var sb = new System.Text.StringBuilder();
                    if (list != null)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var r = list[i];
                            if (r == null || r.Deleted) continue;
                            sb.AppendLine(r.Content ?? string.Empty);
                        }
                    }
                    _rawAllText = sb.ToString();
                }
                catch { _rawAllText = string.Empty; }
            }

            float totalH = Mathf.Max(24f, Text.CalcHeight(_rawAllText ?? string.Empty, rect.width - 16f));
            var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH + 8f));
            Widgets.BeginScrollView(rect, ref _scrollRaw, viewRect);
            var textRect = new Rect(4f, 4f, viewRect.width - 8f, totalH);
            _rawAllText = Widgets.TextArea(textRect, _rawAllText ?? string.Empty);
            Widgets.EndScrollView();
            Text.Font = prev;
        }

        private (string userName, string serverName) GetOrBeginResolveNames(IHistoryService history, string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return ("RimAI.Common.Player".Translate(), "AI Server");
            if (!_convUserName.TryGetValue(convKey, out var user)) user = null;
            if (!_convServerName.TryGetValue(convKey, out var server)) server = null;
            if ((user == null || server == null) && !_nameResolving.Contains(convKey))
            {
                _nameResolving.Add(convKey);
        _ = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
            var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
            var loc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<ILocalizationService>();
                        var locale = cfg?.GetInternal()?.General?.Locale ?? "en";
                        string playerTitle = cfg?.GetPlayerTitleOrDefault();
                        if (string.IsNullOrWhiteSpace(playerTitle))
                        {
                            playerTitle = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
                        }
                        string serverName = "AI Server";
                        try
                        {
                            var parts = history.GetParticipantsOrEmpty(convKey) ?? new List<string>();
                            foreach (var p in parts)
                            {
                                if (p != null && p.StartsWith("server:"))
                                {
                                    serverName = ResolveServerNameFromParticipant(p) ?? serverName;
                                    break;
                                }
                            }
                        }
                        catch { }
                        lock (_convUserName)
                        {
                            _convUserName[convKey] = playerTitle;
                            _convServerName[convKey] = serverName;
                        }
                    }
                    finally { _nameResolving.Remove(convKey); }
                });
            }
            return (user ?? "RimAI.Common.Player".Translate(), server ?? "AI Server");
        }

        private static string ResolveServerNameFromParticipant(string serverParticipant)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(serverParticipant)) return null;
                if (!serverParticipant.StartsWith("server:")) return null;
                var s = serverParticipant.Substring("server:".Length);
                if (!int.TryParse(s, out var id)) return null;
                foreach (var map in Verse.Find.Maps)
                {
                    var things = map?.listerThings?.AllThings; if (things == null) continue;
                    for (int i = 0; i < things.Count; i++)
                    {
                        var t = things[i]; if (t == null || t.thingIDNumber != id) continue;
                        var label = t.LabelCap?.ToString();
                        if (!string.IsNullOrWhiteSpace(label)) return label;
                        return t.def?.label ?? t.def?.defName ?? ($"server:{id}");
                    }
                }
            }
            catch { }
            return null;
        }

        public void ClearCache()
        {
            _entries = null; _recaps = null; TryUnhookRecapEvent();
        }
    }
}
