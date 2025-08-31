using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    internal sealed class ServerCommandSummaryTaskComposer : IPromptComposer
    {
        public PromptScope Scope => PromptScope.ServerCommand;
    public int Order => 9998; // 确保位于系统行的最末尾
        public string Id => "server_command_summary_task";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var req = ctx?.Request;
            if (req == null || !req.IsCommand) return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() });
            var l = ctx?.L ?? ((k, fb) => fb);
            var line = l("ui.command.summary.important_task", "[注意，当下你的任务] 你正在进行领地 Function Calling，你已获取当前工具调用结果，你需要对工具调用结果进行总结。");
            return Task.FromResult(new ComposerOutput
            {
                SystemLines = new List<string> { line },
                ContextBlocks = System.Array.Empty<ContextBlock>()
            });
        }
    }
}
