using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using RimWorld;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.Boot;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
    internal static class ServerToolsTab
    {
        internal sealed class State
        {
            public string LoadedEntityId;
            public int LoadedLevel;
            public List<string> SelectedTools = new List<string>(); // per slot internal name or null
            public Vector2 Scroll = Vector2.zero;
            public double NextRefreshRealtime;
            public int IntervalHours; // 6-128
            public bool Enabled;

            // 新增：缓存可用工具 JSON 与等级映射，避免 UI 线程阻塞
            public List<string> AvailableToolsJson; // BuildToolsAsync(Classic) 结果缓存
            public Dictionary<string, int> LevelMap; // 工具名 -> 等级（1..3）
            public bool IsLoadingTools;
        }

        public static void Draw(Rect inRect, State state, string entityId, int serverLevel, IServerService server, IToolRegistryService tooling)
        {
            if (state == null) state = new State();
            // Header
            var titleRect = new Rect(inRect.x + 8f, inRect.y, inRect.width - 16f, 30f);
            var descRect = new Rect(inRect.x + 8f, titleRect.yMax + 2f, inRect.width - 16f, 36f);
            var ctlRect = new Rect(inRect.x + 8f, descRect.yMax + 2f, inRect.width - 16f, 30f);
            var ctlRect2 = new Rect(inRect.x + 8f, ctlRect.yMax + 4f, inRect.width - 16f, 26f);
            var bodyTop = ctlRect2.yMax + 6f;
            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, "RimAI.SCW.Tools.Header".Translate());
            Text.Font = GameFont.Small;
            var prevColor = GUI.color; GUI.color = new Color(1f,1f,1f,0.75f);
            Widgets.Label(descRect, "RimAI.SCW.Tools.Desc".Translate());
            GUI.color = prevColor;

            // Init or refresh current selections from service
            try
            {
                if (!string.Equals(state.LoadedEntityId, entityId, StringComparison.OrdinalIgnoreCase) || state.LoadedLevel != serverLevel)
                {
                    state.LoadedEntityId = entityId;
                    state.LoadedLevel = serverLevel;
                    state.SelectedTools.Clear();
                    int cap = GetInspectionCapacity(serverLevel);
                    for (int i = 0; i < cap; i++) state.SelectedTools.Add(null);
                    // load basic config
                    var rec0 = server?.Get(entityId);
                    state.IntervalHours = Math.Max(6, rec0?.InspectionIntervalHours ?? 24);
                    state.Enabled = rec0?.InspectionEnabled ?? false;
                    var slots = server?.GetSlots(entityId);
                    foreach (var s in (slots ?? Array.Empty<InspectionSlot>()))
                    {
                        if (s == null) continue;
                        if (s.Index >= 0 && s.Index < state.SelectedTools.Count)
                        {
                            state.SelectedTools[s.Index] = (s.Enabled && !string.IsNullOrWhiteSpace(s.ToolName)) ? s.ToolName : null;
                        }
                    }

                    // 首次载入：异步刷新工具缓存
                    TryStartToolsRefreshAsync(tooling, serverLevel, state);
                }
                else
                {
                    // periodic soft refresh (every 3s) to reflect external changes
                    var now = Time.realtimeSinceStartup;
                    if (now >= state.NextRefreshRealtime)
                    {
                        state.NextRefreshRealtime = now + 3f;
                        var rec = server?.Get(entityId);
                        if (rec != null)
                        {
                            state.IntervalHours = Math.Max(6, rec.InspectionIntervalHours);
                            state.Enabled = rec.InspectionEnabled;
                        }
                        var slots = server?.GetSlots(entityId);
                        foreach (var s in (slots ?? Array.Empty<InspectionSlot>()))
                        {
                            if (s == null) continue;
                            if (s.Index >= 0 && s.Index < state.SelectedTools.Count)
                            {
                                state.SelectedTools[s.Index] = (s.Enabled && !string.IsNullOrWhiteSpace(s.ToolName)) ? s.ToolName : null;
                            }
                        }

                        // 周期性轻量刷新：如缓存为空且未在加载，则触发一次异步刷新
                        if ((state.AvailableToolsJson == null || state.LevelMap == null) && !state.IsLoadingTools)
                        {
                            TryStartToolsRefreshAsync(tooling, serverLevel, state);
                        }
                    }
                }
            }
            catch { }

            // Controls: interval on first line; enable toggle on next line
            DrawControlsLine(ctlRect, state, server, entityId);
            DrawToggleLine(ctlRect2, state, server, entityId);

            // Body list: one row per slot
            int capacity = GetInspectionCapacity(serverLevel);
            float rowH = 30f; float pad = 6f;
            // Reserve footer space for the dev-only manual trigger button
            float footerH = 28f;
            bool showFooter = Prefs.DevMode; // only in Dev Mode
            float reserved = showFooter ? (footerH + 6f) : 0f;
            var bodyOuter = new Rect(inRect.x, bodyTop, inRect.width, inRect.height - (bodyTop - inRect.y) - reserved);
            var viewH = capacity * (rowH + pad) + 8f;
            var viewRect = new Rect(0f, 0f, bodyOuter.width - 16f, Mathf.Max(bodyOuter.height, viewH));
            Widgets.BeginScrollView(bodyOuter, ref state.Scroll, viewRect);
            float y = 4f;
            for (int i = 0; i < capacity; i++)
            {
                var row = new Rect(8f, y, viewRect.width - 16f, rowH);
                DrawSlotRow(row, i, entityId, server, tooling, state);
                y += rowH + pad;
            }
            Widgets.EndScrollView();

            // Footer: Dev-only manual trigger inspection button
            if (showFooter)
            {
                var footerRect = new Rect(inRect.x + 8f, bodyOuter.yMax + 4f, inRect.width - 16f, footerH);
                // Right-aligned button
                float btnW = 140f; float btnH = 26f;
                var btnRect = new Rect(footerRect.xMax - btnW, footerRect.y + (footerRect.height - btnH) / 2f, btnW, btnH);
                if (Widgets.ButtonText(btnRect, "RimAI.SCW.Tools.TriggerInspection".Translate()))
                {
                    // Fire-and-forget manual inspection for this server
                    _ = TriggerInspectionAsync(server, entityId);
                }
            }
        }

        private static void DrawControlsLine(Rect rect, State state, IServerService server, string entityId)
        {
            var left = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
            // Interval slider (6-128 hours)
            var labRect = new Rect(left.x, left.y, 260f, left.height);
            Widgets.Label(labRect, "RimAI.SCW.Tools.IntervalLabel".Translate());
            // Move slider 30px to the left (x-30, width+30 to keep right edge)
            // Further shift the whole slider 50px left (keep right edge): x-50, width+50
            var sliderRect = new Rect(labRect.xMax + 8f - 150f, left.y, (left.width - labRect.width - 12f) + 30f - 70f, left.height);
            int prev = state.IntervalHours;
            float f = state.IntervalHours;
            f = Widgets.HorizontalSlider(sliderRect, f, 6f, 128f, false, state.IntervalHours + "h", null, null, 1f);
            int newVal = (int)Mathf.Round(f);
            newVal = Mathf.Clamp(newVal, 6, 128);
            if (newVal != prev)
            {
                state.IntervalHours = newVal;
                try { server?.SetInspectionIntervalHours(entityId, newVal); } catch { }
            }
        }

        private static void DrawToggleLine(Rect rect, State state, IServerService server, string entityId)
        {
            // Toggle enable/disable on its own line
            var toggleRect = new Rect(rect.x, rect.y, rect.width - 200f, rect.height);
            bool en = state.Enabled;
            Widgets.CheckboxLabeled(toggleRect, "RimAI.SCW.Tools.EnabledLabel".Translate(), ref en);
            if (en != state.Enabled)
            {
                state.Enabled = en;
                try { server?.SetInspectionEnabled(entityId, en); } catch { }
            }
        }

        private static void DrawSlotRow(Rect row, int index, string entityId, IServerService server, IToolRegistryService tooling, State state)
        {
            var labelRect = new Rect(row.x, row.y + 6f, 120f, 22f);
            Widgets.Label(labelRect, string.Format("RimAI.SCW.Tools.SlotLabel".Translate(), index + 1));
            var btnRect = new Rect(labelRect.xMax + 6f, row.y + 2f, Mathf.Min(360f, row.width - (labelRect.width + 12f)), 26f);
            string currentName = null;
            try { currentName = (index >= 0 && index < state.SelectedTools.Count) ? state.SelectedTools[index] : null; } catch { }
            string currentDisplay = string.IsNullOrWhiteSpace(currentName) ? "RimAI.SCW.Tools.NotLoaded".Translate() : GetLocalizedToolDisplay(tooling, currentName);
            if (Widgets.ButtonText(btnRect, currentDisplay))
            {
                var menu = new List<FloatMenuOption>();
                menu.Add(new FloatMenuOption("RimAI.SCW.Tools.NotLoaded".Translate(), () =>
                {
                    try { server?.RemoveSlot(entityId, index); } catch { }
                    try { if (index >= 0 && index < state.SelectedTools.Count) state.SelectedTools[index] = null; } catch { }
                }));
                // 使用缓存的工具列表/等级映射，避免 UI 线程阻塞
                var cachedList = state.AvailableToolsJson;
                var cachedLevels = state.LevelMap;
                if (cachedList == null || cachedList.Count == 0)
                {
                    // 若尚未准备好，提示加载中，并触发一次异步刷新
                    menu.Add(new FloatMenuOption("(" + "RimAI.Common.Loading".Translate() + ")", () => { }));
                    TryStartToolsRefreshAsync(tooling, server?.Get(entityId)?.Level ?? 1, state);
                }
                else
                {
                    // 排序：优先级按等级降序，再按显示名升序
                    var items = new List<(string json, string name, string disp, int level)>();
                    foreach (var j in cachedList)
                    {
                        string name = TryExtractName(j);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var disp = GetLocalizedToolDisplay(tooling, name);
                        int lvl = 1; if (cachedLevels != null && cachedLevels.TryGetValue(name, out var lv)) lvl = lv;
                        items.Add((j, name, disp, lvl));
                    }
                    items = items
                        .OrderByDescending(t => t.level)
                        .ThenBy(t => t.disp, System.StringComparer.CurrentCultureIgnoreCase)
                        .ToList();

                    // 构建“已被任意服务器占用”的集合，用于排除重复加载
                    var taken = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        var listAll = server?.List() ?? new System.Collections.Generic.List<RimAI.Core.Source.Modules.Persistence.Snapshots.ServerRecord>();
                        foreach (var rec in listAll)
                        {
                            foreach (var sl in (rec?.InspectionSlots ?? new System.Collections.Generic.List<RimAI.Core.Source.Modules.Persistence.Snapshots.InspectionSlot>()))
                            {
                                if (sl == null || !sl.Enabled) continue;
                                if (!string.IsNullOrWhiteSpace(sl.ToolName)) taken.Add(sl.ToolName);
                            }
                        }
                    }
                    catch { }

                    foreach (var it in items)
                    {
                        // 排除已经被任意服务器占用的工具（允许本服务器本槽位幂等重选）
                        bool occupied = taken.Contains(it.name);
                        if (occupied)
                        {
                            // 若当前槽位已是该工具，则允许显示（便于取消/切换）
                            if (!string.Equals(state.SelectedTools[index], it.name, System.StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        var label = it.disp + $" (Lv{it.level})" + (occupied ? (" [" + "RimAI.SCW.Tools.Occupied".Translate() + "]") : string.Empty);
                        menu.Add(new FloatMenuOption(label, () =>
                        {
                            if (string.Equals(it.name, "get_unknown_civ_contact", System.StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    var wds = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
                                    if (wds == null || !wds.HasPoweredAntennaNow())
                                    {
                                        var act = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldActionService>();
                                        act?.ShowTopLeftMessageAsync("RimAI.Tool.RequireAntennaPowered".Translate(), RimWorld.MessageTypeDefOf.RejectInput);
                                        return;
                                    }
                                }
                                catch { return; }
                            }
                            try { server?.AssignSlot(entityId, index, it.name); } catch { }
                            try { if (index >= 0 && index < state.SelectedTools.Count) state.SelectedTools[index] = it.name; } catch { }
                        }));
                    }
                }
                Find.WindowStack.Add(new FloatMenu(menu));
            }
        }

    private static int GetInspectionCapacity(int level) => level <= 1 ? 1 : (level == 2 ? 3 : 5);

        private static async Task TriggerInspectionAsync(IServerService server, string entityId)
        {
            try
            {
                if (server == null || string.IsNullOrWhiteSpace(entityId)) return;
                await server.RunInspectionOnceAsync(entityId, CancellationToken.None).ConfigureAwait(false);
                // Optional: user feedback in dev console
                Log.Message($"[RimAI.Core][ServerTools] Manual inspection triggered for {entityId}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimAI.Core][ServerTools] Manual inspection failed for {entityId}: {ex.Message}");
            }
        }

        private static string TryExtractName(string toolJson)
        {
            if (string.IsNullOrWhiteSpace(toolJson)) return null;
            try
            {
                var jo = Newtonsoft.Json.Linq.JObject.Parse(toolJson);
                // Preferred: OpenAI-style tool schema { type:"function", function:{ name, ... } }
                var nm = jo.SelectToken("$.function.name")?.ToString();
                if (!string.IsNullOrWhiteSpace(nm)) return nm;
                // Fallbacks: root.name or case variants
                nm = jo.SelectToken("$.name")?.ToString();
                if (!string.IsNullOrWhiteSpace(nm)) return nm;
                nm = jo.SelectToken("$.Name")?.ToString();
                return string.IsNullOrWhiteSpace(nm) ? null : nm;
            }
            catch { return null; }
        }

        // 通过三次查询（MaxToolLevel = 1/2/3）推断每个工具的等级，用于 UI 展示。
        // 异步刷新工具缓存（Classic 全量 + LevelMap 预估），避免在主线程阻塞
        private static void TryStartToolsRefreshAsync(IToolRegistryService tooling, int serverLevel, State state)
        {
            if (tooling == null || state == null) return;
            if (state.IsLoadingTools) return;
            state.IsLoadingTools = true;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var res = await tooling.BuildToolsAsync(RimAI.Core.Contracts.Config.ToolCallMode.Classic, null, null, null, new ToolQueryOptions { MaxToolLevel = serverLevel }, System.Threading.CancellationToken.None).ConfigureAwait(false);
                    var list = res.toolsJson?.ToList() ?? new List<string>();

                    // 估算 Level 映射（通过三次 MaxToolLevel 调用）
                    var t1 = await tooling.BuildToolsAsync(RimAI.Core.Contracts.Config.ToolCallMode.Classic, null, null, null, new ToolQueryOptions { MaxToolLevel = 1 }, System.Threading.CancellationToken.None).ConfigureAwait(false);
                    var t2 = await tooling.BuildToolsAsync(RimAI.Core.Contracts.Config.ToolCallMode.Classic, null, null, null, new ToolQueryOptions { MaxToolLevel = 2 }, System.Threading.CancellationToken.None).ConfigureAwait(false);
                    var t3 = await tooling.BuildToolsAsync(RimAI.Core.Contracts.Config.ToolCallMode.Classic, null, null, null, new ToolQueryOptions { MaxToolLevel = 3 }, System.Threading.CancellationToken.None).ConfigureAwait(false);
                    var s1 = new System.Collections.Generic.HashSet<string>((t1.toolsJson ?? System.Array.Empty<string>()).Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), System.StringComparer.OrdinalIgnoreCase);
                    var s2 = new System.Collections.Generic.HashSet<string>((t2.toolsJson ?? System.Array.Empty<string>()).Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), System.StringComparer.OrdinalIgnoreCase);
                    var s3 = new System.Collections.Generic.HashSet<string>((t3.toolsJson ?? System.Array.Empty<string>()).Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), System.StringComparer.OrdinalIgnoreCase);
                    var map = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                    foreach (var n in s3)
                    {
                        int lvl = s1.Contains(n) ? 1 : (s2.Contains(n) ? 2 : 3);
                        map[n] = lvl;
                    }

                    // 回写缓存（无需主线程）
                    state.AvailableToolsJson = list;
                    state.LevelMap = map;
                }
                catch { }
                finally { state.IsLoadingTools = false; }
            });
        }

        // 设备/研究 gate 已迁移至 WorldDataService 门面
        //
        private static string GetLocalizedToolDisplay(IToolRegistryService tooling, string toolName)
        {
            var baseName = tooling?.GetToolDisplayNameOrNull(toolName) ?? toolName ?? string.Empty;
            try
            {
                var loc = RimAICoreMod.Container.Resolve<ILocalizationService>();
                var locale = loc?.GetDefaultLocale() ?? "en";
                var key = "tool.display." + (toolName ?? string.Empty);
                var localized = loc?.Get(locale, key, baseName) ?? baseName;
                return string.IsNullOrWhiteSpace(localized) ? baseName : localized;
            }
            catch { return baseName; }
        }
    }
}
