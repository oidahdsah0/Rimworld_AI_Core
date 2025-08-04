using RimAI.Core.Contracts.Data;
using RimAI.Core.Contracts.Services;
using Verse;

#nullable enable

namespace RimAI.Core.Services
{
    /// <summary>
    /// IPersistenceService 实现：利用 RimWorld 的 Scribe 系统序列化 / 反序列化 HistoryService 状态。
    /// </summary>
    public class PersistenceService : IPersistenceService
    {
        public void PersistHistoryState(IHistoryService historyService)
        {
            HistoryStateSnapshot? snapshot = null;

            // 保存阶段：从 HistoryService 获取快照
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                snapshot = (HistoryStateSnapshot)historyService.GetStateForPersistence();
            }

            // Scribe 深度序列化 / 反序列化
            Scribe_Deep.Look(ref snapshot, "historySnapshot");

            // 加载阶段：将反序列化后的快照写回 HistoryService
            if (Scribe.mode == LoadSaveMode.LoadingVars && snapshot != null)
            {
                historyService.LoadStateFromPersistence(snapshot);
            }
        }
    }
}
