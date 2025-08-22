using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PersonaFixedPromptComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 1; // 紧随系统基底之后
		public string Id => "persona_fixed";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var fp = ctx?.Persona?.FixedPrompts;
			if (fp != null && !string.IsNullOrWhiteSpace(fp.Text))
			{
				var title = ctx?.L?.Invoke("prompt.section.fixed_prompts", "[Fixed Prompts]") ?? "[Fixed Prompts]";
				var sp = ctx?.L?.Invoke("prompt.punct.space", " ") ?? " ";
				lines.Add(title + sp + fp.Text);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


