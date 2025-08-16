using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class IndicatorLights
	{
		public static void Draw(Rect rect, RimAI.Core.Source.UI.ChatWindow.IndicatorLightsState state, bool isBusyOn = false)
		{
			// 收紧留白，适配较小高度（例如 20f）
			var padding = 2f;
			var inner = rect.ContractedBy(padding);

			// 小长方体尺寸：在当前行高内再下调 3px（不改变整行/跑马灯高度）
			float lampH = 7f;
			float lampW = 15f;
			var lampYOffset = 3f; // 向下偏移 3px，仅调整 Position，不改变大小
			float y = inner.y + (inner.height - lampH) * 0.5f + lampYOffset;
			float labelSpacing = 6f;   // 灯与文字之间
			float groupSpacing = 12f;  // 指示灯组之间

			// 计算标签尺寸（右侧文字）
			Text.Font = GameFont.Tiny;
			Vector2 dataSize = Text.CalcSize("Data");
			Vector2 busySize = Text.CalcSize("Busy");
			Vector2 finishSize = Text.CalcSize("Fin");

			// 三个指示灯均靠左排列：Data → Busy → Fin
			var dataLamp = new Rect(inner.x, y, lampW, lampH);
			var dataLabel = new Rect(dataLamp.xMax + labelSpacing, inner.y + lampYOffset, dataSize.x, inner.height);
			float nextX = dataLabel.xMax + groupSpacing;

			var busyLamp = new Rect(nextX, y, lampW, lampH);
			var busyLabel = new Rect(busyLamp.xMax + labelSpacing, inner.y + lampYOffset, busySize.x, inner.height);
			nextX = busyLabel.xMax + groupSpacing;

			var finishLamp = new Rect(nextX, y, lampW, lampH);
			var finishLabel = new Rect(finishLamp.xMax + labelSpacing, inner.y + lampYOffset, finishSize.x, inner.height);

			var now = DateTime.UtcNow;
			bool dataOn = state.DataOn && now <= state.DataBlinkUntilUtc;
			Color dataColor = dataOn ? new Color(0.9f, 0.2f, 0.2f, 1f) : new Color(0.25f, 0.07f, 0.07f, 1f);
			Color busyColor = isBusyOn ? new Color(1f, 0.95f, 0.5f, 1f) : new Color(0.35f, 0.33f, 0.12f, 1f);
			Color finishColor = state.FinishOn ? new Color(0.2f, 0.75f, 0.2f, 1f) : new Color(0.07f, 0.25f, 0.07f, 1f);

			// 绘制灯体
			Widgets.DrawBoxSolid(dataLamp, dataColor);
			Widgets.DrawBoxSolid(busyLamp, busyColor);
			Widgets.DrawBoxSolid(finishLamp, finishColor);

			// 绘制标签（在灯体右侧）
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(dataLabel, "Data");
			Widgets.Label(busyLabel, "Busy");
			Widgets.Label(finishLabel, "Fin");
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}


