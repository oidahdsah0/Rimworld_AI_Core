using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class HediffComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 86; // right after health average
		public string Id => "pawn_hediffs";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var h = ctx?.PawnHealth;
			if (h?.Hediffs != null && h.Hediffs.Count > 0)
			{
				var top = h.Hediffs
					.Where(x => x != null && (!string.IsNullOrWhiteSpace(x.Label)))
					.OrderByDescending(x => x.Permanent)
					.ThenByDescending(x => x.Category == "MissingPart" || x.Category == "Implant")
					.ThenByDescending(x => x.Severity)
					.Take(8)
					.ToList();
				if (top.Count > 0)
				{
					var title = ctx?.L?.Invoke("prompt.section.hediffs", "[健康状况]") ?? "[健康状况]";
					var items = top.Select(x =>
					{
						var baseLabel = string.IsNullOrWhiteSpace(x.Part) ? x.Label : ($"{x.Label}({x.Part})");
						return string.IsNullOrWhiteSpace(x.Category) ? baseLabel : ($"{baseLabel}[{x.Category}]");
					});
					lines.Add(title + string.Join("；", items));
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}
