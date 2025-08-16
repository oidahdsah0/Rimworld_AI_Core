using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
    internal sealed class UserPrefixComposer : IPromptComposer, IProvidesUserPrefix
    {
        public PromptScope Scope => PromptScope.ChatUI;
        public int Order => 9999; // 不输出 SystemLines，仅提供前缀
        public string Id => "user_prefix";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            // 不产生 SystemLines 或 ContextBlocks
            return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() });
        }

        public string GetUserPrefix(PromptBuildContext ctx)
        {
            var l = ctx?.L;
            var pfx = l?.Invoke("ui.chat.user_prefix", string.Empty) ?? string.Empty;
            return pfx;
        }
    }
}



