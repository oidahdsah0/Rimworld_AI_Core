using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class ConversationHeader
	{
		public static void Draw(Rect titleRect, Texture portrait, string pawnName, string jobTitle, HealthPulseState pulseState, float? healthPercent, bool pawnDead)
		{
			var pulseW = 200f;
			var pulseLabelW = 72f;
			var pulseSpacing = 6f;
			var rightReserveW = pulseLabelW + pulseSpacing + pulseW;
			var titleLabelRect = new Rect(titleRect.x, titleRect.y, Mathf.Max(0f, titleRect.width - rightReserveW + 80f), titleRect.height);
			TitleBar.Draw(titleLabelRect, portrait, pawnName ?? "RimAI.Common.Pawn".Translate(), string.IsNullOrWhiteSpace(jobTitle) ? "RimAI.ChatUI.Left.Unassigned".Translate() : jobTitle);
			var pulseRect = new Rect(titleRect.xMax - pulseW, titleRect.y + 12f, pulseW - 6f, titleRect.height - 18f);
			var pulseTitleRect = new Rect(pulseRect.x - pulseSpacing - pulseLabelW - 10f, pulseRect.y, pulseLabelW, pulseRect.height);
			var prevAnchor = Text.Anchor; var prevFont = Text.Font;
			Text.Anchor = TextAnchor.MiddleRight; Text.Font = GameFont.Small;
			Widgets.Label(pulseTitleRect, "RimAI.ChatUI.Header.Vitals".Translate());
			Text.Anchor = TextAnchor.UpperLeft; Text.Font = prevFont;
			HealthPulse.Draw(pulseRect, pulseState, healthPercent, pawnDead);
		}
	}
}


