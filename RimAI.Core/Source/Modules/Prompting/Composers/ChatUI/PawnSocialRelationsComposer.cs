using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnSocialRelationsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 60;
		public string Id => "pawn_social_relations";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var social = ctx?.PawnSocial;
			if (social?.Relations != null && social.Relations.Count > 0)
			{
				var title = ctx?.L?.Invoke("prompt.section.social_relations", "[Social Relations]") ?? "[Social Relations]";
				var sep = ctx?.L?.Invoke("prompt.punct.list_semicolon", "; ") ?? "; ";
				var items = new List<string>();
				foreach (var r in social.Relations.Take(5))
				{
					var rel = r?.RelationKind ?? string.Empty;
					var name = r?.OtherName ?? (ctx?.L?.Invoke("prompt.token.unknown", "Unknown") ?? "Unknown");
					var opinion = r?.Opinion.ToString() ?? "0";
					var formatted = ctx?.F?.Invoke("prompt.format.relation",
						new Dictionary<string, string> { { "rel", rel }, { "name", name }, { "opinion", opinion } },
						$"{rel}: {name} (opinion {opinion})")
						?? $"{rel}: {name} (opinion {opinion})";
					items.Add(formatted);
				}
				if (items.Count > 0) lines.Add(title + string.Join(sep, items));
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


