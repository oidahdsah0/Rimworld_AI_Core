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
				var title = ctx?.L?.Invoke("prompt.section.backstory", "[Backstory]") ?? "[Backstory]";
				var unknown = ctx?.L?.Invoke("prompt.token.unknown", "Unknown") ?? "Unknown";
				var child = string.IsNullOrWhiteSpace(p.Story.Childhood) ? unknown : p.Story.Childhood;
				var adult = string.IsNullOrWhiteSpace(p.Story.Adulthood) ? unknown : p.Story.Adulthood;
				var tpl = ctx?.F?.Invoke("prompt.format.backstory", new Dictionary<string, string> { { "child", child }, { "adult", adult } }, $"Childhood: {child}; Adulthood: {adult}")
					?? $"Childhood: {child}; Adulthood: {adult}";
				lines.Add(title + tpl);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


