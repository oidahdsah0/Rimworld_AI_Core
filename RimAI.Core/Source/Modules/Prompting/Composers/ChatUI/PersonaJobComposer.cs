using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PersonaJobComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 40;
		public string Id => "persona_job";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var rec = ctx?.Persona;
			if (rec?.Job != null)
			{
				var name = rec.Job.Name ?? string.Empty;
				var desc = rec.Job.Description ?? string.Empty;
				if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc))
				{
					var title = ctx?.L?.Invoke("prompt.section.persona_job", "[Job]") ?? "[Job]";
					var unassigned = ctx?.L?.Invoke("persona.job.unassigned", "Unassigned") ?? "Unassigned";
					lines.Add(title + unassigned);
				}
				else
				{
					var title = ctx?.L?.Invoke("prompt.section.persona_job", "[Job]") ?? "[Job]";
					var colon = ctx?.L?.Invoke("prompt.punct.colon", ": ") ?? ": ";
					var content = (name ?? string.Empty) + (string.IsNullOrWhiteSpace(desc) ? string.Empty : (colon + desc));
					lines.Add(title + content);
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


