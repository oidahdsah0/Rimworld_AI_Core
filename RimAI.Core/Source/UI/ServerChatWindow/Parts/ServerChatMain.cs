using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimAI.Core.Source.UI.ChatWindow; // ChatController, ChatConversationState
using RimAI.Core.Source.UI.ChatWindow.Parts; // ChatTranscriptView, IndicatorLights, InputRow, LcdMarquee

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
    // 复刻 CW 聊天主体：滚动对话区 + 指示灯 + LCD 跑马灯 + 输入区（支持 Shift+Enter / Ctrl+Enter）
    internal static class ServerChatMain
    {
        private sealed class LcdRngState
        {
            public System.Random Rng;
            public double NextShuffleAt;
            public LcdRngState(int seed) { Rng = new System.Random(seed); }
        }

        private static readonly Dictionary<string, LcdRngState> _lcdByConv = new Dictionary<string, LcdRngState>(StringComparer.Ordinal);

        public static void Draw(
            Rect transcriptRect,
            Rect indicatorRect,
            Rect inputRect,
            ChatController controller,
            ref string inputText,
            ref Vector2 scrollTranscript,
            ref float lastTranscriptContentHeight,
            ref bool historyWritten,
            Action onCancel,
            Func<string, System.Threading.Tasks.Task> onSmalltalkAsync,
            Func<string, System.Threading.Tasks.Task> onCommandAsync)
        {
            if (controller == null) return;

            // 自动吸底逻辑：依据可视高度变化判断
            var prevViewH = lastTranscriptContentHeight;
            var newViewH = ComputeTranscriptViewHeight(transcriptRect, controller.State);
            var prevMaxScrollY = Mathf.Max(0f, prevViewH - transcriptRect.height);
            bool wasNearBottom = scrollTranscript.y >= (prevMaxScrollY - 20f);

            ChatTranscriptView.Draw(transcriptRect, controller.State, scrollTranscript, out scrollTranscript);

            if (wasNearBottom && newViewH > prevViewH + 1f)
            {
                scrollTranscript.y = newViewH;
            }
            lastTranscriptContentHeight = newViewH;

            // 指示灯 + LCD
            IndicatorLights.Draw(indicatorRect, controller.State.Indicators, controller.State.IsStreaming);
            var lcdLeft = indicatorRect.x + 180f;
            if (lcdLeft < indicatorRect.xMax)
            {
                const float lcdRightMargin = 5f;
                var lcdRect = new Rect(lcdLeft, indicatorRect.y, Mathf.Max(0f, indicatorRect.xMax - lcdLeft - lcdRightMargin), indicatorRect.height);
                var pulse = controller.State.Indicators.DataOn;
                var text = GetOrShuffleLcdText(controller);
                LcdMarquee.Draw(lcdRect, controller.State.Lcd, text, pulse, controller.State.IsStreaming);
            }

            // 输入区与快捷键
            bool wantSmalltalk = false, wantCommand = false;
            InputRow.Draw(
                inputRect,
                ref inputText,
                onSmalltalk: () => { wantSmalltalk = true; },
                onCommand: () => { wantCommand = true; },
                onCancel: () => onCancel?.Invoke(),
                isStreaming: controller.State.IsStreaming
            );

            if (wantSmalltalk)
            {
                var t = inputText?.Trim();
                inputText = string.Empty;
                _ = onSmalltalkAsync?.Invoke(t);
            }
            else if (wantCommand)
            {
                var t = inputText?.Trim();
                inputText = string.Empty;
                _ = onCommandAsync?.Invoke(t);
            }

            // 消费一个增量分片（保持与 CW 行为一致）
            if (controller.TryDequeueChunk(out var chunk))
            {
                AppendToLastAiMessage(controller.State, chunk);
            }

            // 复位 Data 指示灯的闪烁
            if (DateTime.UtcNow > controller.State.Indicators.DataBlinkUntilUtc)
            {
                controller.State.Indicators.DataOn = false;
            }

            // 完成后一并合并残余分片（避免 UI 帧漏合并）
            if (controller.State.Indicators.FinishOn && !historyWritten)
            {
                try { AppendAllChunks(controller.State); historyWritten = true; } catch { }
            }
        }

        private static void AppendToLastAiMessage(ChatConversationState state, string delta)
        {
            if (string.IsNullOrEmpty(delta)) return;
            for (int i = state.Messages.Count - 1; i >= 0; i--)
            {
                var m = state.Messages[i];
                if (m.Sender == MessageSender.Ai)
                {
                    m.Text += delta;
                    break;
                }
            }
        }

        private static void AppendAllChunks(ChatConversationState state)
        {
            while (state.StreamingChunks.TryDequeue(out var c))
            {
                AppendToLastAiMessage(state, c);
            }
        }

        private static float ComputeTranscriptViewHeight(Rect rect, ChatConversationState state)
        {
            var contentW = rect.width - 16f;
            var textW = contentW - 12f;
            float totalHeight = 0f;
            for (int i = 0; i < state.Messages.Count; i++)
            {
                var m = state.Messages[i];
                var label = $"[{m.DisplayName} {m.TimestampUtc.ToLocalTime():HH:mm:ss}] {m.Text}";
                var textH = Mathf.Max(24f, Text.CalcHeight(label, textW));
                totalHeight += textH + 6f;
            }
            return Math.Max(rect.height, totalHeight + 8f);
        }

        private static string GetOrShuffleLcdText(ChatController controller)
        {
            var key = controller?.State?.ConvKey ?? string.Empty;
            if (!_lcdByConv.TryGetValue(key, out var st))
            {
                st = new LcdRngState(key.GetHashCode());
                st.NextShuffleAt = 0.0;
                _lcdByConv[key] = st;
            }
            var now = Time.realtimeSinceStartup;
            if (now >= st.NextShuffleAt || string.IsNullOrEmpty(controller.State.Lcd.CachedText))
            {
                var parts = new[] { "RIMAI", "CORE", "V5", "SAFE", "FAST", "STABLE", "AGENT", "STAGE", "TOOL", "WORLD", "HISTORY", "P3", "P4", "P5", "P6", "P7", "P8", "P9", "P10", "AI" };
                for (int i = parts.Length - 1; i > 0; i--)
                {
                    int j = st.Rng.Next(i + 1);
                    var tmp = parts[i]; parts[i] = parts[j]; parts[j] = tmp;
                }
                var take = Mathf.Clamp(8, 3, parts.Length);
                var sel = string.Join(" ", parts, 0, take);
                controller.State.Lcd.CachedText = sel + " ";
                LcdMarquee.EnsureColumns(controller.State.Lcd, controller.State.Lcd.CachedText);
                int totalCols = Mathf.Max(1, controller.State.Lcd.Columns.Count);
                float secsPerLoop = (totalCols / 3f) * controller.State.Lcd.IntervalSec;
                st.NextShuffleAt = now + Mathf.Max(4f, secsPerLoop);
            }
            return controller.State.Lcd.CachedText ?? string.Empty;
        }
    }
}
