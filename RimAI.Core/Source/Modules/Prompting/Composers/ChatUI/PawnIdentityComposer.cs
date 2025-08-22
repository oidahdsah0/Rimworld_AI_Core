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
				var isZh = (ctx?.Locale ?? "en").StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);
				var title = ctx?.L?.Invoke("prompt.section.identity", isZh ? "[个体]" : "[Identity]") ?? (isZh ? "[个体]" : "[Identity]");
				var parts = new System.Collections.Generic.List<string>();
				if (!string.IsNullOrWhiteSpace(p.Id.Name)) parts.Add(p.Id.Name);
				if (!string.IsNullOrWhiteSpace(p.Id.Gender)) parts.Add(p.Id.Gender);
				if (p.Id.Age > 0) parts.Add(isZh ? (p.Id.Age + "岁") : (p.Id.Age + "y"));
				if (!string.IsNullOrWhiteSpace(p.Id.Race)) parts.Add(p.Id.Race);
				if (parts.Count > 0)
				{
					var sep = isZh ? " / " : " / ";
					lines.Add(title + string.Join(sep, parts));
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


