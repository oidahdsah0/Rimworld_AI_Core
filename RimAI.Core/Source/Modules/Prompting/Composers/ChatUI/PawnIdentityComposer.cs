using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PawnIdentityComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 10;
		public string Id => "pawn_identity";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var p = ctx?.PawnPrompt;
			if (p != null && p.Id != null)
			{
				var title = ctx?.L?.Invoke("prompt.section.identity", "[个体]") ?? "[个体]";
				var parts = new List<string>();
				if (!string.IsNullOrWhiteSpace(p.Id.Name)) parts.Add(p.Id.Name);
				if (!string.IsNullOrWhiteSpace(p.Id.Gender)) parts.Add(p.Id.Gender);
				if (p.Id.Age > 0) parts.Add(p.Id.Age + "岁");
				if (!string.IsNullOrWhiteSpace(p.Id.Race)) parts.Add(p.Id.Race);
				// 信仰改为单独项目输出
				if (parts.Count > 0)
				{
					lines.Add(title + string.Join(" / ", parts));
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


