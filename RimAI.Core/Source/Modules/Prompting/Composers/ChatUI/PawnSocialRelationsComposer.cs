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
				lines.Add("[社交关系]");
				foreach (var r in social.Relations.Take(5))
				{
					lines.Add($"{r.RelationKind}: {r.OtherName} (好感{r.Opinion})");
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


