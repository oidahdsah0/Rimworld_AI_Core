using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Services.Prompting.Models;

namespace RimAI.Core.Source.Services.Prompting.Composers.ChatUI
{
	internal sealed class HistoryRecapComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 90;
		public string Id => "history_recap";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var blocks = new List<ContextBlock>();
			var recaps = ctx?.Recaps;
			if (recaps != null && recaps.Count > 0)
			{
				var latest = recaps.OrderByDescending(r => r.ToTurnInclusive).Take(2).ToList();
				var text = string.Join("\n\n", latest.Select(r => r.Text ?? string.Empty));
				if (!string.IsNullOrWhiteSpace(text))
				{
					blocks.Add(new ContextBlock { Title = "[前情提要]", Text = text }); // 后续接入本地化
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks });
		}
	}
}


