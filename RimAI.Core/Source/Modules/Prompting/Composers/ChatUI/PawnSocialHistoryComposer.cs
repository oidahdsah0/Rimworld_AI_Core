using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
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
				var list = new List<string>();
				int idx = 1;
				foreach (var e in social.RecentEvents.Take(10))
				{
					var ts = string.IsNullOrWhiteSpace(e.GameTime) ? e.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : e.GameTime;
					var who = string.IsNullOrWhiteSpace(e.WithName) ? "?" : e.WithName;
					var kind = string.IsNullOrWhiteSpace(e.InteractionKind) ? "Social" : e.InteractionKind;
					var outcome = string.IsNullOrWhiteSpace(e.Outcome) ? string.Empty : (" " + e.Outcome);
					var colon = ctx?.L?.Invoke("prompt.punct.colon", ": ") ?? ": ";
					list.Add($"{idx++}. {ts} - {who}{colon}{kind}{outcome}");
				}
				var title = ctx?.L?.Invoke("prompt.section.social_history", "[Social History]") ?? "[Social History]";
				blocks.Add(new ContextBlock { Title = title, Text = string.Join("\n", list) });
			}
			return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks });
		}
	}
}


