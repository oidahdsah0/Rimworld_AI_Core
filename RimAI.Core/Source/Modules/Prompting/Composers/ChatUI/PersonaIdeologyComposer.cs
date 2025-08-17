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
				// 按新规则：ChatUI 下仍保留意识形态四段单行
				var title = ctx?.L?.Invoke("prompt.section.ideology", "[意识形态]") ?? "[意识形态]";
				string OneLine(string s)
				{
					if (string.IsNullOrWhiteSpace(s)) return string.Empty;
					var t = s.Replace("\r", " ").Replace("\n", " ");
					while (t.Contains("  ")) t = t.Replace("  ", " ");
					return t.Trim();
				}
				void Add(string name, string text, string key)
				{
					var single = OneLine(text);
					if (string.IsNullOrWhiteSpace(single)) return;
					var tpl = ctx?.F?.Invoke(key, new Dictionary<string, string> { { "name", name }, { "text", single } }, name + "：" + single) ?? (name + "：" + single);
					lines.Add(title + tpl);
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


