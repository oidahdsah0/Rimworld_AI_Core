using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

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
            Widgets.Label(titleRect, "服务器加载工具管理");
            Text.Font = GameFont.Small;
            var prevColor = GUI.color; GUI.color = new Color(1f,1f,1f,0.75f);
            Widgets.Label(descRect, "服务器会通过工具槽里的工具，定时自动分析领地的相关信息");
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
            var bodyOuter = new Rect(inRect.x, bodyTop, inRect.width, inRect.height - (bodyTop - inRect.y));
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
        }

        private static void DrawControlsLine(Rect rect, State state, IServerService server, string entityId)
        {
            var left = new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 4f);
            // Interval slider (6-128 hours)
            var labRect = new Rect(left.x, left.y, 260f, left.height);
            Widgets.Label(labRect, "巡检周期：");
            // Move slider 30px to the left (x-30, width+30 to keep right edge)
            // Further shift the whole slider 50px left (keep right edge): x-50, width+50
            var sliderRect = new Rect(labRect.xMax + 8f - 150f, left.y, (left.width - labRect.width - 12f) + 30f - 80f, left.height);
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
            Widgets.CheckboxLabeled(toggleRect, "巡检开关：", ref en);
            if (en != state.Enabled)
            {
                state.Enabled = en;
                try { server?.SetInspectionEnabled(entityId, en); } catch { }
            }
        }

        private static void DrawSlotRow(Rect row, int index, string entityId, IServerService server, IToolRegistryService tooling, State state)
        {
            var labelRect = new Rect(row.x, row.y + 6f, 120f, 22f);
            Widgets.Label(labelRect, $"工具槽{index + 1}：");
            var btnRect = new Rect(labelRect.xMax + 6f, row.y + 2f, Mathf.Min(360f, row.width - (labelRect.width + 12f)), 26f);
            string currentName = null;
            try { currentName = (index >= 0 && index < state.SelectedTools.Count) ? state.SelectedTools[index] : null; } catch { }
            string currentDisplay = string.IsNullOrWhiteSpace(currentName) ? "不加载" : (tooling?.GetToolDisplayNameOrNull(currentName) ?? currentName);
            if (Widgets.ButtonText(btnRect, currentDisplay))
            {
                var menu = new List<FloatMenuOption>();
                menu.Add(new FloatMenuOption("不加载", () =>
                {
                    try { server?.RemoveSlot(entityId, index); } catch { }
                    try { if (index >= 0 && index < state.SelectedTools.Count) state.SelectedTools[index] = null; } catch { }
                }));
                // Build allowed tools by server level
                try
                {
                    var rec = server?.Get(entityId);
                    int level = rec?.Level ?? 1;
                    var classic = tooling?.GetClassicToolCallSchema(new ToolQueryOptions { MaxToolLevel = level });
                    var list = classic?.ToolsJson ?? Array.Empty<string>();

                    // 构建一次 “工具名→等级(Lv1/2/3)” 映射，用于展示标注
                    var levelMap = BuildLevelMap(tooling);
                    foreach (var j in list)
                    {
                        string name = TryExtractName(j);
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        var disp = tooling?.GetToolDisplayNameOrNull(name) ?? name;
                        if (levelMap != null && levelMap.TryGetValue(name, out var lvl) && lvl >= 1 && lvl <= 3)
                        {
                            disp = $"{disp} (Lv{lvl})";
                        }
                        menu.Add(new FloatMenuOption(disp, () =>
                        {
                            try { server?.AssignSlot(entityId, index, name); } catch { }
                            try { if (index >= 0 && index < state.SelectedTools.Count) state.SelectedTools[index] = name; } catch { }
                        }));
                    }
                }
                catch { }
                Find.WindowStack.Add(new FloatMenu(menu));
            }
        }

        private static int GetInspectionCapacity(int level) => level <= 1 ? 3 : (level == 2 ? 5 : 10);

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
        private static Dictionary<string, int> BuildLevelMap(IToolRegistryService tooling)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var l1 = tooling?.GetClassicToolCallSchema(new ToolQueryOptions { MaxToolLevel = 1 })?.ToolsJson ?? Array.Empty<string>();
                var l2 = tooling?.GetClassicToolCallSchema(new ToolQueryOptions { MaxToolLevel = 2 })?.ToolsJson ?? Array.Empty<string>();
                var l3 = tooling?.GetClassicToolCallSchema(new ToolQueryOptions { MaxToolLevel = 3 })?.ToolsJson ?? Array.Empty<string>();
                var s1 = new HashSet<string>(l1.Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
                var s2 = new HashSet<string>(l2.Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
                var s3 = new HashSet<string>(l3.Select(TryExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
                foreach (var n in s3)
                {
                    int lvl = s1.Contains(n) ? 1 : (s2.Contains(n) ? 2 : 3);
                    map[n] = lvl;
                }
            }
            catch { }
            return map;
        }
    }
}
