using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
    // Adds the highest-priority requirement about output language to system lines
    internal sealed class OutputLanguageRequirementComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ChatUI;
        public int Order => 1; // Immediately after system base
        public string Id => "output_language_requirement";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            var text = ctx?.L?.Invoke("prompt.requirement.output_language", string.Empty) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}
