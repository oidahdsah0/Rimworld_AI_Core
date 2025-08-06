using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 认知历史服务（海马体）。
    /// 记录与检索玩家与 AI 的最终对话条目。
    /// </summary>
    public interface IHistoryService
    {
        /// <summary>
        /// 记录一条对话条目。
        /// </summary>
        /// <param name="participantIds">参与者稳定 ID 列表（排序后生成会话键）。</param>
        /// <param name="entry">条目内容。</param>
        Task RecordEntryAsync(IReadOnlyList<string> participantIds, ConversationEntry entry);

        /// <summary>
        /// 按参与者组合检索主线 + 背景历史。
        /// </summary>
        /// <param name="participantIds">参与者稳定 ID 列表。</param>
        Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds);

        /// <summary>
        /// 导出内部状态，供持久化层序列化。
        /// </summary>
        HistoryState GetStateForPersistence();

        /// <summary>
        /// 从持久化状态恢复内部数据结构。
        /// </summary>
        void LoadStateFromPersistence(HistoryState state);
    }
}
