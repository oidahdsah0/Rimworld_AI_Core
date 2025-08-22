using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerTemperatureChart
	{
		public static void Draw(Rect rect, float[] samples)
		{
			if (samples == null || samples.Length == 0)
			{
				Widgets.DrawMenuSection(rect);
				return;
			}
			Widgets.DrawMenuSection(rect);
			var pad = 4f; var w = rect.width - pad * 2f; var h = rect.height - pad * 2f;
			var inner = new Rect(rect.x + pad, rect.y + pad, w, h);
			int n = samples.Length;
			float barW = Mathf.Max(2f, inner.width / Mathf.Max(1, n));
			for (int i = 0; i < n; i++)
			{
				var v = Mathf.Clamp01((samples[i] - 0.5f) / 1.5f); // 映射 0.5..2.0 -> 0..1
				float bh = inner.height * v;
				var bar = new Rect(inner.x + i * barW, inner.yMax - bh, barW - 1f, bh);
				var color = Color.Lerp(new Color(0.2f,0.6f,1f), new Color(1f,0.3f,0.2f), v);
				Widgets.DrawBoxSolid(bar, color);
			}
		}
	}
}


