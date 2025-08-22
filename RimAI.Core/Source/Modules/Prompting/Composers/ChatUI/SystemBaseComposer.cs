using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
    internal sealed class SystemBaseComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ChatUI;
        public int Order => 0; // 放到最前，作为 SystemPrompt 基底
        public string Id => "system_base";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            var locSvc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
            var locale = ctx?.Locale ?? "en";
            var playerTitle = ctx?.PlayerTitle ?? (locSvc?.Get(locale, "ui.chat.player_title.value", "总督") ?? "总督");

            // 目标语言取值
            var nameLoc = locSvc?.Get(locale, "ui.chat.system.base.name", string.Empty) ?? string.Empty;
            var valueLoc = locSvc?.Format(locale, "ui.chat.system.base.value", new Dictionary<string, string> { { "player_title", playerTitle } }, string.Empty) ?? string.Empty;
            // 英文回退
            var nameEn = locSvc?.Get("en", "ui.chat.system.base.name", string.Empty) ?? string.Empty;
            var valueEn = locSvc?.Format("en", "ui.chat.system.base.value", new Dictionary<string, string> { { "player_title", playerTitle } }, string.Empty) ?? string.Empty;

            // 若任一缺失，则整对回退到英文，避免中英混搭
            bool useEn = string.IsNullOrWhiteSpace(nameLoc) || string.IsNullOrWhiteSpace(valueLoc);
            var name = useEn ? nameEn : nameLoc;
            var value = useEn ? valueEn : valueLoc;

            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                lines.Add(name + value);
            }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}



