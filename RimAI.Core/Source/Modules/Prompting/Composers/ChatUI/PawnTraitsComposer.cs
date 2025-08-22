using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnTraitsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 30;
		public string Id => "pawn_traits";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var p = ctx?.PawnPrompt;
			if (p?.Traits != null)
			{
				if (p.Traits.Traits != null && p.Traits.Traits.Count > 0)
				{
					var title = ctx?.L?.Invoke("prompt.section.traits", "[Traits]") ?? "[Traits]";
					var comma = ctx?.L?.Invoke("prompt.punct.list_comma", ", ") ?? ", ";
					var list = string.Join(comma, p.Traits.Traits);
					var line = ctx?.F?.Invoke("prompt.format.traits_line", new Dictionary<string, string> { { "traits", list } }, $"Traits: {list}") ?? $"Traits: {list}";
					lines.Add(title + line);
				}
				if (p.Traits.WorkDisables != null && p.Traits.WorkDisables.Count > 0)
				{
					var title2 = ctx?.L?.Invoke("prompt.section.work_disables", "[Work Disables]") ?? "[Work Disables]";
					var comma2 = ctx?.L?.Invoke("prompt.punct.list_comma", ", ") ?? ", ";
					var list2 = string.Join(comma2, p.Traits.WorkDisables);
					var line2 = ctx?.F?.Invoke("prompt.format.work_disables_line", new Dictionary<string, string> { { "list", list2 } }, $"Cannot: {list2}") ?? $"Cannot: {list2}";
					lines.Add(title2 + line2);
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


