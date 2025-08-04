using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Data;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 总工程师：集中处理所有需要持久化的服务状态。
    /// 目前仅负责 HistoryService 的状态保存与恢复。
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// 在 RimWorld 存档加载 / 保存流程中调用，负责序列化或反序列化 HistoryService 状态。
        /// 该方法应当只在 <see cref="Verse.Scribe"/> 环境下调用。
        /// </summary>
        /// <param name="historyService">核心的 IHistoryService 实例。</param>
        void PersistHistoryState(IHistoryService historyService);
    }
}
