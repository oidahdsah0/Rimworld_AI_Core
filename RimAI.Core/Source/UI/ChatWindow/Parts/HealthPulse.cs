using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class HealthPulseState
	{
		public float ScrollOffsetPx;
		public double NextStepAtRealtime;
		public float StepPx = 2f;
		public float IntervalSec = 0.05f;
		public float AmplitudePx = 9f;
		public float SpikeIntervalPx = 60f;
	}

	internal static class HealthPulse
	{
		public static void Draw(Rect rect, HealthPulseState state, float? healthPercent, bool isDead)
		{
			Widgets.DrawBoxSolidWithOutline(rect, new Color(0f, 0f, 0f, 0.10f), new Color(0f, 0f, 0f, 0.35f));
			if (rect.width <= 1f || rect.height <= 1f) return;

			Color color;
			if (isDead) color = new Color(0.5f, 0.5f, 0.5f, 1f);
			else if (healthPercent.HasValue && healthPercent.Value > 80f) color = new Color(0.1f, 0.85f, 0.3f, 1f);
			else if (healthPercent.HasValue && healthPercent.Value > 40f) color = new Color(0.95f, 0.8f, 0.1f, 1f);
			else color = new Color(0.9f, 0.2f, 0.2f, 1f);

			var baselineY = rect.y + rect.height * 0.55f;
			var amp = isDead ? 0f : state.AmplitudePx;

			// 推进偏移（按实时节拍，仅用于滚动，不参与随机）
			var now = Time.realtimeSinceStartup;
			if (now >= state.NextStepAtRealtime)
			{
				state.ScrollOffsetPx += state.StepPx;
				if (state.ScrollOffsetPx > state.SpikeIntervalPx) state.ScrollOffsetPx -= state.SpikeIntervalPx;
				state.NextStepAtRealtime = now + state.IntervalSec;
			}

			// 使用 1px 纹理绘制线段
			var tex = BaseContent.WhiteTex;
			var lineW = 2f;
			var x = rect.x;
			var endX = rect.xMax;
			var spikeEvery = state.SpikeIntervalPx;
			var phase = state.ScrollOffsetPx;
			var prevY = baselineY;
			while (x < endX)
			{
				var localTotal = (x - rect.x + phase);
				var spikeIndex = Mathf.FloorToInt(localTotal / spikeEvery);
				var local = localTotal % spikeEvery;
				if (local < 0) { local += spikeEvery; }

				// 每个心跳周期固定的随机参数（与时间无关）
				float baseDrift = isDead ? 0f : (Mathf.PerlinNoise(spikeIndex * 0.111f, 0.222f) - 0.5f) * 4f; // ±2px
				var effAmpGlobal = isDead ? 0f : amp * (0.75f + 0.5f * Mathf.PerlinNoise(spikeIndex * 0.333f, 0.444f));
				var peakMul = 0.7f + 0.6f * Mathf.PerlinNoise(spikeIndex * 0.555f, 0.666f);
				var upperMul = 0.7f + 0.6f * Mathf.PerlinNoise(spikeIndex * 0.777f, 0.888f);
				var lowerMul = 0.7f + 0.6f * Mathf.PerlinNoise(spikeIndex * 0.999f, 0.123f);

				float y;
				if (isDead)
				{
					y = baselineY;
				}
				else if (local < 14f)
				{
					// 心跳尖峰形状：升-尖-回落（本周期固定峰值倍率）
					var effAmp = effAmpGlobal * peakMul;
					if (local < 6f) y = baselineY + baseDrift - effAmp * (local / 6f);
					else if (local < 8f) y = baselineY + baseDrift - effAmp;
					else y = baselineY + baseDrift - effAmp * (1f - (local - 8f) / 6f);
				}
				else
				{
					// 其余段为正弦起伏：上峰/下谷使用不同固定倍率（本周期内不再变化）
					var t = (local - 14f) / (spikeEvery - 14f);
					var sinRaw = Mathf.Sin(t * Mathf.PI * 2f);
					var sideMul = sinRaw >= 0 ? upperMul : lowerMul;
					var sinAmp = effAmpGlobal * 0.35f;
					y = baselineY + baseDrift + sinRaw * sinAmp * sideMul;
				}

				var seg = new Rect(x, Mathf.Min(prevY, y), lineW, Mathf.Max(1f, Mathf.Abs(y - prevY)));
				GUI.color = color;
				GUI.DrawTexture(seg, tex);
				x += lineW;
				prevY = y;
			}
			GUI.color = Color.white;
		}
	}
}


