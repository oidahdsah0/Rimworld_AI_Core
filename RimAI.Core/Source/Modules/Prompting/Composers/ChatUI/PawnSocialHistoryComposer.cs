using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Services.Prompting.Models;

namespace RimAI.Core.Source.Services.Prompting.Composers.ChatUI
{
	internal sealed class PawnSocialHistoryComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 96;
		public string Id => "pawn_social_history";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var blocks = new List<ContextBlock>();
			var social = ctx?.PawnSocial;
			if (social?.RecentEvents != null && social.RecentEvents.Count > 0)
			{
				var lines = social.RecentEvents.Take(5).Select(e => $"[{e.TimestampUtc:HH:mm}] {e.WithName}: {e.InteractionKind}{(string.IsNullOrWhiteSpace(e.Outcome) ? string.Empty : (" " + e.Outcome))}");
				blocks.Add(new ContextBlock { Title = "[社交历史]", Text = string.Join("\n", lines) });
			}
			return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks });
		}
	}
}


