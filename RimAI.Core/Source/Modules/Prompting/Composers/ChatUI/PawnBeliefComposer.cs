using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnBeliefComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 15;
		public string Id => "pawn_belief";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var p = ctx?.PawnPrompt;
			if (p != null && p.IsIdeologyAvailable && p.Id != null && !string.IsNullOrWhiteSpace(p.Id.Belief))
			{
				var title = ctx?.L?.Invoke("prompt.section.belief", "[Belief]") ?? "[Belief]";
				lines.Add(title + p.Id.Belief);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


