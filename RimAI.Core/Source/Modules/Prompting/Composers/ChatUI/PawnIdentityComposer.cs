using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnIdentityComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 10;
		public string Id => "pawn_identity";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new System.Collections.Generic.List<string>();
			var p = ctx?.PawnPrompt;
			if (p != null && p.Id != null)
			{
				var title = ctx?.L?.Invoke("prompt.section.identity", "[Identity]") ?? "[Identity]";
				var parts = new System.Collections.Generic.List<string>();
				if (!string.IsNullOrWhiteSpace(p.Id.Name)) parts.Add(p.Id.Name);
				if (!string.IsNullOrWhiteSpace(p.Id.Gender)) parts.Add(p.Id.Gender);
				if (p.Id.Age > 0) parts.Add(p.Id.Age + (ctx?.L?.Invoke("prompt.unit.age_y", "y") ?? "y"));
				if (!string.IsNullOrWhiteSpace(p.Id.Race)) parts.Add(p.Id.Race);
				if (parts.Count > 0)
				{
					var sep = " / ";
					lines.Add(title + string.Join(sep, parts));
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


