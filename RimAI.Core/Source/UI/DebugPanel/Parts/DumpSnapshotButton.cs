using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class DumpSnapshotButton : IDebugPanelButton
    {
        public string Label => "Dump Snapshot";

        public void Execute(DebugPanelContext ctx)
        {
            Task.Run(() =>
            {
                try
                {
                    var recap = ctx.Get<RimAI.Core.Modules.History.IRecapService>();
                    var fixedSvc = ctx.Get<RimAI.Core.Modules.Persona.IFixedPromptService>();
                    var bioSvc = ctx.Get<RimAI.Core.Modules.Persona.IBiographyService>();
                    var history = ctx.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>();

                    var participants = new List<string> { ctx.Get<RimAI.Core.Modules.World.IParticipantIdService>().GetPlayerId(), "pawn:DEMO" };
                    var convKey = string.Join("|", participants.OrderBy(x => x, System.StringComparer.Ordinal));
                    var ctxTask = history.GetHistoryAsync(participants);
                    ctxTask.Wait();

                    ctx.AppendOutput($"[Snapshot] convKey={convKey}");
                    ctx.AppendOutput("- Fixed Prompts (by pawn):");
                    foreach (var kv in fixedSvc.GetAllByPawn()) ctx.AppendOutput($"  {kv.Key}: {kv.Value}");
                    var overrideText = fixedSvc.GetConvKeyOverride(convKey);
                    if (!string.IsNullOrWhiteSpace(overrideText)) ctx.AppendOutput($"  [override] {overrideText}");
                    ctx.AppendOutput("- Biographies:");
                    var pawnId = participants.First(x => x.StartsWith("pawn:"));
                    foreach (var it in bioSvc.ListByPawn(pawnId)) ctx.AppendOutput($"  [{it.CreatedAt:HH:mm:ss}] {it.Text}");
                    ctx.AppendOutput("- Recap:");
                    var historyWrite = ctx.Get<RimAI.Core.Services.IHistoryWriteService>();
                    string latestConvId = null;
                    try { var list = historyWrite.FindByConvKeyAsync(convKey).GetAwaiter().GetResult(); latestConvId = list?.LastOrDefault(); } catch { }
                    if (!string.IsNullOrWhiteSpace(latestConvId))
                    {
                        foreach (var it in recap.GetRecapItems(latestConvId)) ctx.AppendOutput($"  [{it.CreatedAt:HH:mm:ss}] {it.Text}");
                    }
                    ctx.AppendOutput("- History (last entries):");
                    foreach (var c in ctxTask.Result.MainHistory)
                        foreach (var e in c.Entries) ctx.AppendOutput($"  [{e.Timestamp:HH:mm:ss}] {e.SpeakerId}: {e.Content}");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput("Dump Snapshot failed: " + ex.Message);
                }
            });
        }
    }
}


