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
                    var conv = ctx.Get<RimAI.Core.Modules.Persona.IPersonaConversationService>();
                    var participants = new System.Collections.Generic.List<string> { ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId(), "pawn:DEMO" };
                    var stream = conv.ChatAsync(participants, "Default", "今天天气怎么样？", new RimAI.Core.Modules.Persona.PersonaChatOptions { Stream = true, WriteHistory = false });
                    await foreach (var chunk in stream)
                    {
                        if (chunk.IsSuccess)
                            ctx.EnqueueRaw(chunk.Value?.ContentDelta ?? string.Empty);
                        else
                            ctx.EnqueueRaw("[Error] " + chunk.Error);
                    }
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


