using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Persona
{
	internal sealed class PersonaBiographySystemComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.PersonaBiography;
		public int Order => 0;
		public string Id => "persona_biography_system";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var s = ctx?.L?.Invoke("ui.persona.biography.system", string.Empty) ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(s)) lines.Add(s);
			// 移除个人传记主体内容出 System：仅在 PersonaIdeology 的 user 段使用
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}

	internal sealed class PersonaIdeologySystemComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.PersonaIdeology;
		public int Order => 0;
		public string Id => "persona_ideology_system";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var s = ctx?.L?.Invoke("ui.persona.ideology.system", string.Empty) ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(s)) lines.Add(s);
			// 移除世界观详细四段出 System：仅在 PersonaBiography 的 user 段使用
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


