using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ReloadPromptsButton : IDebugPanelButton
    {
        public string Label => "Reload Prompts";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.AppendOutput("Reloading prompt templates...");
            Task.Run(() =>
            {
                try
                {
                    var tmpl = ctx.Get<RimAI.Core.Modules.Prompting.IPromptTemplateService>();
                    tmpl?.Get();
                    ctx.AppendOutput("Prompt templates reloaded.");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput("Reload prompts failed: " + ex.Message);
                }
            });
        }
    }
}


