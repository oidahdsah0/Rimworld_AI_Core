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
			var lines = new System.Collections.Generic.List<string>();
			var locSvc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
			var locale = ctx?.Locale ?? "en";
			var titleLoc = ctx?.PlayerTitle ?? (locSvc?.Get(locale, "ui.chat.player_title.value", locSvc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? (locSvc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor"));
			var nameLoc = locSvc?.Get(locale, "ui.chat.player_title.name", string.Empty) ?? string.Empty;
			var nameEn = locSvc?.Get("en", "ui.chat.player_title.name", "[You should address the player as]") ?? "[You should address the player as]";
			var titleEn = ctx?.PlayerTitle ?? (locSvc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor");
			bool useEn = string.IsNullOrWhiteSpace(nameLoc) || string.IsNullOrWhiteSpace(titleLoc);
			var name = useEn ? nameEn : nameLoc;
			var title = useEn ? titleEn : titleLoc;
			if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(title))
			{
				lines.Add(name + title);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}




