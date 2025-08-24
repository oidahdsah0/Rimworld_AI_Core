using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    internal sealed class ServerInspectionSystemComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ServerInspection;
        public int Order => 1000; // 放末尾
        public string Id => "server_inspection_system";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var locale = ctx?.Locale ?? (loc?.GetDefaultLocale() ?? "en");
                var fb = "You'll receive one tool result (JSON or text). Summarize in 2–3 lines how it relates to the colony's current state. Output only the summary.";
                var text = loc?.Get(locale, "server.inspection.summary.system", fb) ?? fb;
                if (!string.IsNullOrWhiteSpace(text)) lines.Add(text);
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}
