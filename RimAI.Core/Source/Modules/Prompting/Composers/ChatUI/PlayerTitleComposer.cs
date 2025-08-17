using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class PlayerTitleComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 1; // 紧随系统基底之后
		public string Id => "player_title";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var l = ctx?.L;
			var name = l?.Invoke("ui.chat.player_title.name", "[你应该称呼玩家为]") ?? "[你应该称呼玩家为]";
			var title = ctx?.PlayerTitle ?? (l?.Invoke("ui.chat.player_title.value", "总督") ?? "总督");
			if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(title))
			{
				lines.Add(name + title);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}




