using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Modules.History
{
    /// <summary>
    /// “总结/前情提要”服务（内部接口）。
    /// 监听历史记录新增事件，按 N 轮生成总结，并每 10 轮叠加到“前情提要”字典。
    /// </summary>
    internal interface IRecapService
    {
        /// <summary>
        /// 历史条目新增时的回调（V2）。
        /// </summary>
        void OnEntryRecorded(string conversationId, ConversationEntry entry);

        /// <summary>
        /// 主动触发“每十轮”的叠加（用于测试或补偿）。
        /// </summary>
        void OnEveryTenRounds(string conversationId);

        /// <summary>
        /// UI 触发的一键重述；后台执行，不阻塞主流程。
        /// </summary>
        Task RebuildRecapAsync(string conversationId, CancellationToken ct = default);

        /// <summary>
        /// （调试用）获取指定会话（conversationId）的累计轮次计数。
        /// </summary>
        int GetCounter(string conversationId);

        /// <summary>
        /// 读取指定会话（conversationId）的前情提要字典（倒序）。
        /// </summary>
        IReadOnlyList<RecapSnapshotItem> GetRecapItems(string conversationId);

        /// <summary>
        /// 更新字典项文本。
        /// </summary>
        bool UpdateRecapItem(string conversationId, string itemId, string newText);

        /// <summary>
        /// 删除字典项。
        /// </summary>
        bool RemoveRecapItem(string conversationId, string itemId);

        /// <summary>
        /// 重排字典项到指定索引。
        /// </summary>
        bool ReorderRecapItem(string conversationId, string itemId, int newIndex);

        // 快照（持久化）
        IReadOnlyDictionary<string, IReadOnlyList<RecapSnapshotItem>> ExportSnapshot();
        void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyList<RecapSnapshotItem>> snapshot);
    }

    internal readonly struct RecapSnapshotItem
    {
        public string Id { get; }
        public string Text { get; }
        public System.DateTime CreatedAt { get; }
        public RecapSnapshotItem(string id, string text, System.DateTime createdAt)
        {
            Id = id; Text = text; CreatedAt = createdAt;
        }
    }
}


