using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class PromptAuditTestButton : IDebugPanelButton
    {
        public string Label => "Prompt Audit Test";

        public void Execute(DebugPanelContext ctx)
        {
            Task.Run(async () =>
            {
                try
                {
                    // 改为调用统一提示词服务做审计预览
                    var prompt = ctx.Get<RimAI.Core.Modules.Prompt.IPromptService>();
                    var input = new RimAI.Core.Modules.Orchestration.PromptAssemblyInput
                    {
                        Mode = RimAI.Core.Modules.Orchestration.PromptMode.Chat,
                        Locale = prompt.ResolveLocale(),
                        PersonaSystemPrompt = ctx.Get<RimAI.Core.Contracts.Services.IPersonaService>().Get("Default")?.SystemPrompt,
                        RecapSegments = new System.Collections.Generic.List<string> { "- 前情提要样例1", "- 前情提要样例2" },
                        HistorySnippets = new System.Collections.Generic.List<string> { "- player: 问候", "- pawn: 回答" },
                        MaxPromptChars = 2000
                    };
                    var sys = await prompt.ComposeSystemPromptAsync(input);
                    ctx.EnqueueRaw(sys);
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput("Prompt Audit Test failed: " + ex.Message);
                }
            });
        }
    }
}


