using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ShowHistoryButton : IDebugPanelButton
    {
        public string Label => "Show History";

        public void Execute(DebugPanelContext ctx)
        {
            var history = ctx.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>();
            var historyWrite = ctx.Get<RimAI.Core.Services.IHistoryWriteService>();
            var recap = ctx.Get<RimAI.Core.Modules.History.IRecapService>();
            Task.Run(async () =>
            {
                try
                {
                    var participants = new List<string> { ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId(), "pawn:ColonyGovernor" };
                    var context = await history.GetHistoryAsync(participants);
                    ctx.AppendOutput($"=== 历史记录 ===");
                    ctx.AppendOutput($"主线对话数: {context.MainHistory.Count}");

                    foreach (var conv in context.MainHistory)
                    {
                        ctx.AppendOutput($"对话条目数: {conv.Entries.Count}");
                        foreach (var entry in conv.Entries)
                        {
                            ctx.AppendOutput($"[{entry.Timestamp:HH:mm:ss}] {entry.SpeakerId}: {entry.Content}");
                        }
                    }

                    if (context.MainHistory.Count == 0)
                    {
                        ctx.AppendOutput("没有找到历史记录。");
                    }

                    try
                    {
                        var convKey = string.Join("|", participants.OrderBy(x => x, System.StringComparer.Ordinal));
                        string latestConvId = null;
                        try { var list = historyWrite.FindByConvKeyAsync(convKey).GetAwaiter().GetResult(); latestConvId = list?.LastOrDefault(); } catch { }
                        var n = (latestConvId != null) ? (recap?.GetCounter(latestConvId) ?? 0) : 0;
                        ctx.AppendOutput($"[调试] Recap 轮次计数（{convKey}）= {n}");
                    }
                    catch { /* ignore */ }
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Show History failed: {ex.Message}");
                }
            });
        }
    }
}


