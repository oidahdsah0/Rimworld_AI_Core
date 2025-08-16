using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class InputRow
	{
		private static GUIStyle _richInputStyle;
		private const string InputControlName = "RimAI.Core.ChatWindow.Input";

		private static void EnsureStyle()
		{
			if (_richInputStyle != null) return;
			var baseStyle = new GUIStyle(GUI.skin.textArea)
			{
				wordWrap = true,
				richText = true,
				alignment = TextAnchor.UpperLeft,
				padding = new RectOffset(8, 8, 6, 6)
			};
			// 透明背景以显示我们自绘的黑色底
			baseStyle.normal.background = BaseContent.ClearTex;
			baseStyle.focused.background = BaseContent.ClearTex;
			baseStyle.hover.background = BaseContent.ClearTex;
			baseStyle.active.background = BaseContent.ClearTex;
			baseStyle.normal.textColor = Color.white;
			baseStyle.focused.textColor = Color.white;
			baseStyle.hover.textColor = Color.white;
			baseStyle.active.textColor = Color.white;
			_richInputStyle = baseStyle;
		}

		public static void Draw(Rect rect, ref string text, System.Action onSmalltalk, System.Action onCommand, System.Action onCancel, bool isStreaming = false)
		{
			// 无边框，同一行：富文本输入框与三个按钮等高
			var buttonW = 110f;
			var spacing = 4f;
			var rowH = rect.height;
			var totalButtonsW = buttonW * 3f + spacing * 2f;
			var inputRect = new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - totalButtonsW - spacing), rowH);
			var r1 = new Rect(inputRect.xMax + spacing, rect.y, buttonW, rowH);
			var r2 = new Rect(r1.xMax + spacing, rect.y, buttonW, rowH);
			var r3 = new Rect(r2.xMax + spacing, rect.y, buttonW, rowH);

			// 先处理快捷键：在 TextArea 消费键盘事件之前拦截 Shift+Enter / Ctrl+Enter
			var evt = Event.current;
			if (evt != null && evt.type == EventType.KeyDown)
			{
				bool focusedNow = GUI.GetNameOfFocusedControl() == InputControlName;
				if (focusedNow)
				{
					if (evt.shift && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !isStreaming)
					{
						evt.Use();
						onSmalltalk?.Invoke();
					}
					else if (evt.control && (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && !isStreaming)
					{
						evt.Use();
						onCommand?.Invoke();
					}
				}
			}

			// 文本区域：移除外框，仅保留原生多行输入，Enter 默认换行
			var innerRect = inputRect;
			Text.Font = GameFont.Small;
			GUI.SetNextControlName(InputControlName);
			text = Widgets.TextArea(innerRect, text);
			if (Event.current.type == EventType.MouseDown && innerRect.Contains(Event.current.mousePosition))
			{
				GUI.FocusControl(InputControlName);
			}

			// 三按钮（流式传输中禁用两个发送，只保留“中断传输”）
			bool enableSends = !isStreaming;
			GUI.enabled = enableSends;
			if (Widgets.ButtonText(r1, "闲聊发送 (Shift+Enter)") && enableSends) onSmalltalk?.Invoke();
			if (Widgets.ButtonText(r2, "命令发送 (Ctrl+Enter)") && enableSends) onCommand?.Invoke();
			GUI.enabled = true;
			if (Widgets.ButtonText(r3, "中断传输")) onCancel?.Invoke();

			// 常规 Enter：由 Widgets.TextArea 自行处理为换行，不在此处拦截
		}
	}
}


