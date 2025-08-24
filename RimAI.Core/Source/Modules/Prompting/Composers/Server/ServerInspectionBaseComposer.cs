using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    // Prepends the inspection system base at the very top for ServerInspection scope
    internal sealed class ServerInspectionBaseComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ServerInspection;
        public int Order => 1; // very early, before identity/persona/temperature
        public string Id => "server_inspection_base";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var locale = ctx?.Locale ?? (loc?.GetDefaultLocale() ?? "en");
                var title = loc?.Get(locale, "ui.server.inspection.base.name", "[System Base]") ?? "[System Base]";
                var value = loc?.Get(locale, "ui.server.inspection.base.value", "You are the colony's resident AI server. Be steady and factual; prefer structured, actionable outputs; avoid meta explanations.") ?? "You are the colony's resident AI server. Be steady and factual; prefer structured, actionable outputs; avoid meta explanations.";
                lines.Add(title);
                lines.Add(value);
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}
