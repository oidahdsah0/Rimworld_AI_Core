using System;
using System.Linq;
using System.Collections.Generic;
using RimAI.Core.Contracts.Models;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 历史区域渲染与增量缓冲刷入。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        private void FlushStreamDeltas()
        {
            if (_streamQueue == null) return;
            bool any = false;
            while (_streamQueue.TryDequeue(out var delta))
            {
                if (!string.IsNullOrEmpty(delta))
                {
                    _streamAssistantBuffer = (_streamAssistantBuffer ?? string.Empty) + delta;
                    _streamLastDeltaAtUtc = DateTime.UtcNow;
                    any = true;
                }
            }
            if (any)
            {
                _scroll.y = Mathf.Max(0, _scroll.y - 0.0001f);
            }
        }

        private void FlushProgressLines()
        {
            if (_progressQueue == null) return;
            while (_progressQueue.TryDequeue(out var line))
            {
                _progressSb.AppendLine(line);
            }
        }

        private void DrawHistory(Rect rect)
        {
            var entries = _entries ?? new List<ConversationEntry>();

            float estimated = 0f;
            var cWidthEst = rect.width - 16f - LeftMetaColWidth - 8f;
            foreach (var e in entries)
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(e.Content ?? string.Empty, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(_pendingPlayerMessage, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            if (!string.IsNullOrEmpty(_streamAssistantBuffer))
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(_streamAssistantBuffer, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            var viewH = Math.Max(rect.height - 8f, estimated + 16f);
            var viewRect = new Rect(0, 0, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);

            float curY = 0f;
            var oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            int rowIndex = 0;
            for (int i = 0; i < entries.Count; i++, rowIndex++)
            {
                var e = entries[i];
                float rowY = curY;

                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(e.Content ?? string.Empty, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;

                bool isPlayer = e.SpeakerId?.StartsWith("player:") == true;
                if (isPlayer)
                {
                    var backRect = new Rect(0, rowY, viewRect.width, rowHeight);
                    Widgets.DrawBoxSolid(backRect, new Color(0.85f, 0.92f, 1f, 0.25f));
                }

                var alias = _config?.Current?.UI?.PlayerAlias ?? "总督";
                string speaker = (e.SpeakerId?.StartsWith("player:") ?? false) ? alias : (e.SpeakerId ?? "assistant");
                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{e.Timestamp:HH:mm:ss}] {speaker}");
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, e.Content ?? string.Empty);

                curY = rowY + rowHeight;
            }

            if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
            {
                float rowY = curY;
                var alias = _config?.Current?.UI?.PlayerAlias ?? "总督";
                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(_pendingPlayerMessage, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;
                Widgets.DrawBoxSolid(new Rect(0, rowY, viewRect.width, rowHeight), new Color(0.85f, 0.92f, 1f, 0.25f));

                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{_pendingTimestamp:HH:mm:ss}] {alias}");
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, _pendingPlayerMessage);
                curY = rowY + rowHeight;
            }

            if (string.Equals(_modeTitle, "命令", StringComparison.Ordinal))
            {
                // 限制进度文本的最大展示长度，防止 UI 文本过长
                var progressRaw = _progressSb.ToString();
                var progressText = progressRaw.Length > MaxProgressChars
                    ? progressRaw.Substring(progressRaw.Length - MaxProgressChars)
                    : progressRaw;
                if (!string.IsNullOrEmpty(progressText))
                {
                    float rowY = curY;
                    float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                    float contentHeight = Math.Max(22f, Text.CalcHeight(progressText, cWidth));
                    float rowHeight = contentHeight + RowSpacing + 8f;
                    Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), "[Progress] Orchestrator");
                    var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                    Widgets.Label(contentRect, progressText);
                    curY = rowY + rowHeight;
                }
            }

            if (!string.IsNullOrEmpty(_streamAssistantBuffer))
            {
                // 限制流式预览最大长度（展示尾部内容即可）
                var preview = _streamAssistantBuffer.Length > MaxStreamPreviewChars
                    ? _streamAssistantBuffer.Substring(_streamAssistantBuffer.Length - MaxStreamPreviewChars)
                    : _streamAssistantBuffer;
                float rowY = curY;
                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(preview, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;
                var ts = (_streamStartedAtUtc == DateTime.MinValue ? DateTime.UtcNow : _streamStartedAtUtc);
                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{ts:HH:mm:ss}] assistant");
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, preview);
                curY = rowY + rowHeight;
            }
            Text.WordWrap = oldWrap;

            Widgets.EndScrollView();
        }
    }
}


