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
			if (rec?.Job != null && (!string.IsNullOrWhiteSpace(rec.Job.Name) || !string.IsNullOrWhiteSpace(rec.Job.Description)))
			{
				lines.Add("[职务]" + ($"{rec.Job.Name}：{rec.Job.Description}".Trim('：')));
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


