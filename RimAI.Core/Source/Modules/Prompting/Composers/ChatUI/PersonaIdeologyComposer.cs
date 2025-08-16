using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PersonaIdeologyComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 50;
		public string Id => "persona_ideology";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var rec = ctx?.Persona?.Ideology;
			if (rec != null)
			{
				var title = ctx?.L?.Invoke("prompt.section.ideology", "[意识形态]") ?? "[意识形态]";
				lines.Add(title);
				void Add(string name, string text, string key)
				{
					if (string.IsNullOrWhiteSpace(text)) return;
					var tpl = ctx?.F?.Invoke(key, new Dictionary<string, string> { { "name", name }, { "text", text } }, name + "：" + text) ?? (name + "：" + text);
					lines.Add(tpl);
				}
				Add("世界观", rec.Worldview, "prompt.format.ideology_worldview");
				Add("价值观", rec.Values, "prompt.format.ideology_values");
				Add("行为准则", rec.CodeOfConduct, "prompt.format.ideology_code");
				Add("性格特质", rec.TraitsText, "prompt.format.ideology_traits");
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


