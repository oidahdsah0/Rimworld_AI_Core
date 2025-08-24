using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    // Appends the important task suffix at the tail of System for inspection scope
    internal sealed class ServerInspectionTaskSuffixComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ServerInspection;
        public int Order => 2000; // after system summary and other lines
        public string Id => "server_inspection_task_suffix";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var locale = ctx?.Locale ?? (loc?.GetDefaultLocale() ?? "en");
                var suffix = loc?.Get(locale, "ui.server.inspection.task_suffix", "[Important: Current Task] You are executing an automated base inspection; summarize and record the following system JSON.")
                             ?? "[Important: Current Task] You are executing an automated base inspection; summarize and record the following system JSON.";
                if (!string.IsNullOrWhiteSpace(suffix)) lines.Add(suffix);
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}
