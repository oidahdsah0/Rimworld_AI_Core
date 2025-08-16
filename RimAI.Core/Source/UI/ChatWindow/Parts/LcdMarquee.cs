using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class LcdMarquee
	{
		// 将文本展开为列位图缓存，并按红绿交替给每列上色标记
		public static void EnsureColumns(RimAI.Core.Source.UI.ChatWindow.LcdMarqueeState state, string text)
		{
			if (text == null) text = string.Empty;
			if (state.CachedText == text && state.Columns.Count > 0) return;
			state.CachedText = text;
			state.Columns.Clear();
			state.ColumnIsGreen.Clear();
			foreach (var ch in text)
			{
				var glyph = LcdFont5x7.GetGlyph(ch);
				for (int i = 0; i < glyph.Length; i++)
				{
					state.Columns.Add(glyph[i]);
					state.ColumnIsGreen.Add(true); // 仅绿色（Idle）或黄（Streaming）由渲染期决定
				}
				// 列间距 1 列空白
				state.Columns.Add(0);
				state.ColumnIsGreen.Add(true);
			}
			if (state.Columns.Count == 0)
			{
				state.Columns.Add(0);
				state.ColumnIsGreen.Add(true);
			}
		}

		public static void Draw(Rect rect, RimAI.Core.Source.UI.ChatWindow.LcdMarqueeState state, string text, bool pulse, bool isStreaming)
		{
			// 背板：黑底
			var bgRect = new Rect(rect.x, rect.y, rect.width, rect.height + 2f);
			Widgets.DrawBoxSolid(bgRect, Color.black);
			EnsureColumns(state, text);

			float innerPad = 1f;
			int rowCount = 6;
			float cellH = Mathf.Floor((rect.height - innerPad * 2f) / rowCount);
			if (cellH >= 1f) cellH += 1f;
			if (cellH < 1f) return;
			float cellW = cellH; // 方点

			// 以秒为节拍推进“脉冲式”偏移：一次跳 3 个 LED 列宽
			var now = Time.realtimeSinceStartup;
			if (now >= state.NextStepAtRealtime)
			{
				float stepPx = cellW * 3f;
				state.OffsetPx += stepPx;
				if (state.OffsetPx >= 100000f) state.OffsetPx = 0f;
				state.NextStepAtRealtime = now + state.IntervalSec;
			}

			int totalCols = state.Columns.Count;
			float totalWidthPx = Mathf.Max(1f, totalCols * cellW);
			float offsetMod = state.OffsetPx % totalWidthPx;
			if (offsetMod < 0f) offsetMod += totalWidthPx;
			float baseX = rect.x + innerPad - offsetMod;
			float baseY = rect.y + innerPad;

			// 颜色方案：Streaming=黄，Idle=绿
			Color onIdle = new Color(0.6f, 1f, 0.6f, 1f);
			Color offIdle = new Color(0.12f, 0.26f, 0.12f, 1f);
			Color onStream = new Color(1f, 0.95f, 0.5f, 1f);
			Color offStream = new Color(0.35f, 0.33f, 0.12f, 1f);
			var onColor = isStreaming ? onStream : onIdle;
			var offColor = isStreaming ? offStream : offIdle;

			// 绘制三遍（第 2、3 遍向右平移一个/两个总宽度），实现无缝循环
			for (int pass = 0; pass < 3; pass++)
			{
				float passBaseX = baseX + pass * totalWidthPx;
				for (int col = 0; col < totalCols; col++)
				{
					float x = passBaseX + col * cellW;
					if (x > rect.xMax) break;
					if (x + cellW < rect.x) continue;
					byte columnBits = state.Columns[col];
					int bitBase = rowCount == 6 ? 1 : 0;
					for (int row = 0; row < rowCount; row++)
					{
						int bitIndex = row + bitBase;
						bool bitOn = ((columnBits >> bitIndex) & 1) != 0;
						var c = bitOn ? onColor : offColor;
						Widgets.DrawBoxSolid(new Rect(x, baseY + ((rowCount - 1) - row) * cellH, cellW - 1f, cellH - 1f), c);
					}
				}
			}

			// 脉冲：数据脉冲时在最右边缘叠加 2 列高亮（使用当前 onColor 的更亮版本）
			if (pulse)
			{
				var pulseColor = isStreaming ? new Color(1f, 1f, 0.7f, 1f) : new Color(0.9f, 1f, 0.9f, 1f);
				float px = rect.xMax - innerPad - cellW * 2f;
				for (int i = 0; i < 2; i++)
				{
					for (int row = 0; row < rowCount; row++)
					{
						Widgets.DrawBoxSolid(new Rect(px + i * cellW, baseY + row * cellH, cellW - 1f, cellH - 1f), pulseColor);
					}
				}
			}
		}
	}
}
