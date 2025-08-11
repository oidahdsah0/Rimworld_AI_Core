using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 输入栏与快捷键处理。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        private void DrawInputBar(Rect rect)
        {
            float btnW = 120f;
            float inputH = rect.height;
            var inputRect = new Rect(rect.x, rect.y, rect.width - (btnW * 3 + 6f * 3), inputH);
            var chatBtnRect = new Rect(inputRect.xMax + 6f, rect.y, btnW, inputH);
            var cmdBtnRect = new Rect(chatBtnRect.xMax + 6f, rect.y, btnW, inputH);
            var cancelBtnRect = new Rect(cmdBtnRect.xMax + 6f, rect.y, btnW, inputH);

            GUI.SetNextControlName("RimAI.ChatInput");
            HandleHotkeysForSend();
            _inputText = Widgets.TextArea(inputRect, _inputText ?? string.Empty);
            if (string.IsNullOrEmpty(_inputText))
            {
                string placeholder = "Enter换行；Shift+Enter闲聊；Ctrl+Enter命令";
                var oldCol = GUI.color; var oldFont = Text.Font; var oldAnchor = Text.Anchor;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.UpperLeft;
                var hintRect = new Rect(inputRect.x + 6f, inputRect.y + 4f, inputRect.width - 12f, inputRect.height - 8f);
                Widgets.Label(hintRect, placeholder);
                GUI.color = oldCol; Text.Font = oldFont; Text.Anchor = oldAnchor;
            }

            bool canSend = !_isSending && !string.IsNullOrWhiteSpace(_convKeyInput) && !string.IsNullOrWhiteSpace(_inputText);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = canSend;
            if (Widgets.ButtonText(chatBtnRect, "闲聊"))
            {
                _ = SendAsync(SendMode.Chat);
            }
            GUI.enabled = oldEnabled;

            oldEnabled = GUI.enabled;
            GUI.enabled = canSend;
            if (Widgets.ButtonText(cmdBtnRect, "命令"))
            {
                _ = SendAsync(SendMode.Command);
            }
            GUI.enabled = oldEnabled;

            bool canCancel = _isSending && _cts != null;
            oldEnabled = GUI.enabled;
            GUI.enabled = canCancel;
            if (Widgets.ButtonText(cancelBtnRect, "取消"))
            {
                try { _cts?.Cancel(); } catch { }
                if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
                {
                    _inputText = _pendingPlayerMessage;
                    _pendingPlayerMessage = null;
                }
                _streamAssistantBuffer = null;
                _isSending = false;
            }
            GUI.enabled = oldEnabled;
        }

        private void HandleHotkeysForSend()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (!(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) return;
            string focused = GUI.GetNameOfFocusedControl();
            bool focusOnInput = string.Equals(focused, "RimAI.ChatInput", StringComparison.Ordinal);
            if (!focusOnInput) return;

            bool canSend = !_isSending && !string.IsNullOrWhiteSpace(_convKeyInput) && !string.IsNullOrWhiteSpace(_inputText);
            if (!canSend) return;

            if (e.shift)
            {
                _ = SendAsync(SendMode.Chat);
                e.Use();
                return;
            }
            if (e.control || e.command)
            {
                _ = SendAsync(SendMode.Command);
                e.Use();
                return;
            }
        }
    }
}


