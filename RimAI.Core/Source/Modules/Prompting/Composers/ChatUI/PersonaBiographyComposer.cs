using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PersonaBiographyComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 45;
		public string Id => "persona_biography";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var list = ctx?.Persona?.Biography;
			if (list != null && list.Count > 0)
			{
				var title = ctx?.L?.Invoke("prompt.section.biography", "[Biography]") ?? "[Biography]";
				var parts = list.Take(4).Select(b => (b.Text ?? string.Empty).Trim()).Where(s => !string.IsNullOrEmpty(s));
				var sep = ctx?.L?.Invoke("prompt.punct.list_semicolon", "; ") ?? "; ";
				var end = ctx?.L?.Invoke("prompt.punct.end_semicolon", ";") ?? ";";
				var oneLine = string.Join(sep, parts);
				if (!string.IsNullOrEmpty(oneLine)) lines.Add(title + oneLine + end);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


