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
            var l = ctx?.L;
            var name = l?.Invoke("ui.chat.system.base.name", string.Empty) ?? string.Empty;
            var value = l?.Invoke("ui.chat.system.base.value", string.Empty) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(value))
            {
                lines.Add(name + value);
            }
            // 若 name 或 value 为空，则跳过（验证“空值时作曲器跳过”）
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() });
        }
    }
}



