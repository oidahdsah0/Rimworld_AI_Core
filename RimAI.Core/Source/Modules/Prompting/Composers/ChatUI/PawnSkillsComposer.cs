using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnSkillsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 35;
		public string Id => "pawn_skills";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var p = ctx?.PawnPrompt;
			if (p?.Skills?.Items != null && p.Skills.Items.Count > 0)
			{
				var title = ctx?.L?.Invoke("prompt.section.skills", "[技能]") ?? "[技能]";
				var parts = new List<string>();
				foreach (var s in p.Skills.Items.Take(12))
				{
					var passion = string.IsNullOrWhiteSpace(s.Passion) || s.Passion == "None" ? string.Empty : (" " + s.Passion);
					var tpl = ctx?.F?.Invoke("prompt.format.skill", new Dictionary<string, string> { { "name", s.Name ?? string.Empty }, { "level", s.Level.ToString() }, { "passion", passion } }, $"{s.Name}: Lv{s.Level}{passion}")
						?? $"{s.Name}: Lv{s.Level}{passion}";
					parts.Add(tpl);
				}
				if (parts.Count > 0) lines.Add(title + string.Join("; ", parts) + ";");
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


