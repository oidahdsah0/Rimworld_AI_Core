using System.Collections.Generic;
using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class RecordHistoryButton : IDebugPanelButton
    {
        public string Label => "Record History";

        public void Execute(DebugPanelContext ctx)
        {
            var history = ctx.Get<RimAI.Core.Services.IHistoryWriteService>();
            Task.Run(async () =>
            {
                try
                {
                    var participants = new List<string> { ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId(), "pawn:ColonyGovernor" };
                    var convId = history.CreateConversation(participants);
                    await history.AppendEntryAsync(convId, new RimAI.Core.Contracts.Models.ConversationEntry(ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId(), "测试对话：你好，总督！", System.DateTime.UtcNow));
                    await history.AppendEntryAsync(convId, new RimAI.Core.Contracts.Models.ConversationEntry("pawn:ColonyGovernor", "你好，指挥官！", System.DateTime.UtcNow));
                    ctx.AppendOutput("示例对话已写入；请手动存档→主菜单→读档后验证历史是否持久化。");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Record History failed: {ex.Message}");
                }
            });
        }
    }
}


