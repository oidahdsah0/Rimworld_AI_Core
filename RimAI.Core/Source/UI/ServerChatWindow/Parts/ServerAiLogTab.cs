using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.Stage;
using Newtonsoft.Json.Linq;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
    internal static class ServerAiLogTab
    {
    // 本页UI日志总开关（默认关闭，相当于“注释掉”所有本页日志输出）
    private static readonly bool UiLogEnabled = false;
        private static void UiLog(string msg)
        {
            if (UiLogEnabled)
                Verse.Log.Message(msg);
        }
        private static void UiLogError(string msg)
        {
            if (UiLogEnabled)
                Verse.Log.Error(msg);
        }

        internal sealed class State
        {
            public Vector2 Scroll = Vector2.zero;
            public readonly List<(Color color, string text)> Lines = new List<(Color color, string text)>();
            public readonly Dictionary<int, Color> ColorMap = new Dictionary<int, Color>();
            public double NextRefreshRealtime;
        }

        private static readonly Color[] Palette = new Color[]
        {
            new Color(0.95f, 0.35f, 0.35f), // red
            new Color(0.35f, 0.65f, 0.95f), // blue
            new Color(0.35f, 0.85f, 0.45f), // green
            new Color(0.95f, 0.75f, 0.35f), // orange
            new Color(0.75f, 0.45f, 0.95f), // purple
            new Color(0.35f, 0.85f, 0.85f), // teal
            new Color(0.85f, 0.35f, 0.65f), // pink
            new Color(0.60f, 0.60f, 0.95f), // indigo
            new Color(0.60f, 0.80f, 0.60f), // olive
            new Color(0.90f, 0.60f, 0.60f), // salmon
        };

        public static void Draw(Rect rect, State state, IHistoryService history, IStageService stage)
        {
            if (state == null) return;

            // periodic refresh
            var now = Time.realtimeSinceStartup;
            if (now >= state.NextRefreshRealtime)
            {
                state.NextRefreshRealtime = now + 3f;
                _ = RefreshAsync(state, history);
            }

            // content
            var actionsH = 36f;
            var outer = new Rect(rect.x, rect.y, rect.width, rect.height - actionsH);
            // 背景框，便于“富文本区域”可见
            Widgets.DrawMenuSection(outer);
            var totalHeight = CalcTotalHeight(state, outer.width - 16f);
            var viewRect = new Rect(0f, 0f, outer.width - 16f, totalHeight);
            Widgets.BeginScrollView(outer, ref state.Scroll, viewRect);
            float y = 0f;
            // 占位提示（无日志时）
            if (state.Lines.Count == 0)
            {
                var old = GUI.color;
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 0.9f);
                Widgets.Label(new Rect(8f, 8f, viewRect.width - 16f, 24f), "（暂无日志）");
                GUI.color = old;
            }
            for (int i = 0; i < state.Lines.Count; i++)
            {
                var ln = state.Lines[i];
                var h = Text.CalcHeight(ln.text, viewRect.width);
                var r = new Rect(0f, y, viewRect.width, h);
                var old = GUI.color;
                GUI.color = ln.color;
                Widgets.Label(r, ln.text);
                GUI.color = old;
                y += h + 4f;
            }
            Widgets.EndScrollView();

            // actions bar
            var bar = new Rect(rect.x, rect.yMax - actionsH + 4f, rect.width, actionsH - 4f);
            float bw = 120f;
            var rAct = new Rect(bar.xMax - bw, bar.y, bw, 28f);
            if (Widgets.ButtonText(rAct, "触发ACT"))
            {
                _ = TriggerAsync(stage, state);
            }
        }

        private static float CalcTotalHeight(State state, float width)
        {
            float total = 0f;
            for (int i = 0; i < state.Lines.Count; i++)
            {
                total += Text.CalcHeight(state.Lines[i].text, width) + 4f;
            }
            if (total < 10f) total = 10f;
            return total;
        }

        private static async System.Threading.Tasks.Task RefreshAsync(State state, IHistoryService history)
        {
            if (history == null) return;
            try
            {
                var convKey = BuildServerHubConvKey();
                var items = await history.GetAllEntriesRawAsync(convKey).ConfigureAwait(false);
                var lines = new List<(Color color, string text)>();
                foreach (var e in items)
                {
                    if (e == null || string.IsNullOrWhiteSpace(e.Content)) continue;
                    string speaker = null; string content = null;
                    try
                    {
                        var jo = JObject.Parse(e.Content);
                        speaker = jo.Value<string>("speaker");
                        content = jo.Value<string>("content");
                    }
                    catch { content = e.Content; }
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    var (clr, disp) = ResolveColorAndDisplay(state, speaker);
                    var line = string.IsNullOrWhiteSpace(disp) ? content : ($"【{disp}】{content}");
                    lines.Add((clr, line));
                }
                state.Lines.Clear();
                state.Lines.AddRange(lines);
            }
            catch { }
        }

        private static (Color color, string display) ResolveColorAndDisplay(State state, string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker)) return (Color.white, string.Empty);
            int? id = null;
            try
            {
                if (speaker.StartsWith("thing:"))
                {
                    var s = speaker.Substring(6);
                    if (int.TryParse(s, out var n)) id = n;
                }
            }
            catch { }
            if (!id.HasValue) return (new Color(0.85f, 0.85f, 0.85f), speaker);
            if (!state.ColorMap.TryGetValue(id.Value, out var color))
            {
                var idx = state.ColorMap.Count % Palette.Length;
                color = Palette[idx];
                state.ColorMap[id.Value] = color;
            }
            return (color, id.Value.ToString());
        }

        private static string BuildServerHubConvKey()
        {
            var list = new List<string> { "agent:server_hub", "player:servers" };
            list.Sort(StringComparer.Ordinal);
            return string.Join("|", list);
        }

        private static async System.Threading.Tasks.Task TriggerAsync(IStageService stage, State state)
        {
            try
            {
                if (stage == null) return;
                UiLog("[RimAI.Core][UI][AiLog] Trigger click: begin");
                // 优先：走“服务器群聊”专用手动触发器
                try { var armed = stage.ArmTrigger("ManualInterServerTrigger"); UiLog($"[RimAI.Core][UI][AiLog] Arm ManualInterServerTrigger => {armed}"); } catch { }
                try { await stage.RunActiveTriggersOnceAsync(System.Threading.CancellationToken.None).ConfigureAwait(false); UiLog("[RimAI.Core][UI][AiLog] RunActiveTriggersOnceAsync after Manual trigger"); } catch { }

                // 次优：全局随机触发器（被武装后会必定尝试一次）
                try { var armed2 = stage.ArmTrigger("GlobalTimedRandomActTrigger"); UiLog($"[RimAI.Core][UI][AiLog] Arm GlobalTimedRandomActTrigger => {armed2}"); } catch { }
                try { await stage.RunActiveTriggersOnceAsync(System.Threading.CancellationToken.None).ConfigureAwait(false); UiLog("[RimAI.Core][UI][AiLog] RunActiveTriggersOnceAsync after Global trigger"); } catch { }

                // 兜底：若未注册该 Trigger 或无可用 Act，则直接尝试提交 InterServerGroupChat 的自动意图
                if (stage is RimAI.Core.Source.Modules.Stage.StageService impl)
                {
                    var provider = impl.TryGetAutoProvider("InterServerGroupChat");
                    if (provider == null) { UiLog("[RimAI.Core][UI][AiLog] AutoProvider InterServerGroupChat is null"); }
                    var intent = await (provider?.TryBuildAutoIntentAsync(System.Threading.CancellationToken.None) ?? System.Threading.Tasks.Task.FromResult<RimAI.Core.Source.Modules.Stage.Models.StageIntent>(null));
                    if (intent != null)
                    {
                            intent.Origin = "Manual"; // mark manual so it bypasses coalesce/cooldown
                        try
                        {
                            var decision = await impl.SubmitIntentAsync(intent, System.Threading.CancellationToken.None).ConfigureAwait(false);
                            UiLog($"[RimAI.Core][UI][AiLog] Direct submit decision: outcome={decision?.Outcome} reason={decision?.Reason} ticket={(decision?.Ticket?.Id ?? "")}" );
                        }
                        catch (System.Exception ex) { UiLogError($"[RimAI.Core][UI][AiLog] Direct submit error: {ex}"); }
                    }
                    else { UiLog("[RimAI.Core][UI][AiLog] AutoIntent build returned null"); }
                }
                state.NextRefreshRealtime = 0; // 强制更快刷新以显示新日志
                UiLog("[RimAI.Core][UI][AiLog] Trigger click: end");
            }
            catch { }
        }
    }
}
