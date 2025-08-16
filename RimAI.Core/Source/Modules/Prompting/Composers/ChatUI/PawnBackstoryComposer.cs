using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnBackstoryComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 20;
		public string Id => "pawn_backstory";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var p = ctx?.PawnPrompt;
			if (p?.Story != null)
			{
				var title = ctx?.L?.Invoke("prompt.section.backstory", "[身份]") ?? "[身份]";
				var tpl = ctx?.F?.Invoke("prompt.format.backstory", new Dictionary<string, string> { { "child", p.Story.Childhood ?? string.Empty }, { "adult", p.Story.Adulthood ?? string.Empty } }, $"童年：{p.Story.Childhood}；成年：{p.Story.Adulthood}")
					?? $"童年：{p.Story.Childhood}；成年：{p.Story.Adulthood}";
				lines.Add(title);
				lines.Add(tpl);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


