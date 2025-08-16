using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Services.Prompting.Models;

namespace RimAI.Core.Source.Services.Prompting.Composers.ChatUI
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
				var title = "[个体]"; // 后续接入本地化
				var parts = new List<string>();
				if (!string.IsNullOrWhiteSpace(p.Id.Name)) parts.Add(p.Id.Name);
				if (!string.IsNullOrWhiteSpace(p.Id.Gender)) parts.Add(p.Id.Gender);
				if (p.Id.Age > 0) parts.Add(p.Id.Age + "岁");
				if (!string.IsNullOrWhiteSpace(p.Id.Race)) parts.Add(p.Id.Race);
				if (p.IsIdeologyAvailable && !string.IsNullOrWhiteSpace(p.Id.Belief)) parts.Add(p.Id.Belief);
				if (parts.Count > 0)
				{
					lines.Add(title);
					lines.Add(string.Join(" / ", parts));
				}
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


