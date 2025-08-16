using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class HealthAverageComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 85; // 位于标题相关区段之后、社交之前
		public string Id => "pawn_health_avg";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var sysLines = new List<string>();
			var h = ctx?.PawnHealth;
			if (h != null)
			{
				// 计算均值（0..100），与 UI 生命体征一致
				var avg = (h.Consciousness + h.Moving + h.Manipulation + h.Sight + h.Hearing + h.Talking + h.Breathing + h.BloodPumping + h.BloodFiltration + h.Metabolism) / 10f * 100f;
				var title = ctx?.L?.Invoke("prompt.section.health_avg", "[生命体征]") ?? "[生命体征]";
				sysLines.Add(title + $"Average Health: {avg:F0}%{(h.IsDead ? " (DEAD)" : string.Empty)}");
			}
			return Task.FromResult(new ComposerOutput { SystemLines = sysLines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


