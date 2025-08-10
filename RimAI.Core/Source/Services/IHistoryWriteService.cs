using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 内部历史写入/快照接口。仅供 Core 内部使用，不对外暴露。
    /// </summary>
    internal interface IHistoryWriteService
    {
        // V2 主流程
        string CreateConversation(IReadOnlyList<string> participantIds);
        Task AppendEntryAsync(string conversationId, ConversationEntry entry);
        Task<ConversationRecord> GetConversationAsync(string conversationId);
        Task<IReadOnlyList<string>> FindByConvKeyAsync(string convKey);
        Task<IReadOnlyList<string>> ListByParticipantAsync(string participantId);

        // 便捷入口：按参与者集合检索主线 + 背景（用于提示组装等场景）
        Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds);

        // 编辑能力改为按 conversationId
        Task EditEntryAsync(string conversationId, int entryIndex, string newContent);
        Task DeleteEntryAsync(string conversationId, int entryIndex);
        Task RestoreEntryAsync(string conversationId, int entryIndex, ConversationEntry entry);

        // 快照（内部持久化）
        HistoryV2State GetV2StateForPersistence();
        void LoadV2StateFromPersistence(HistoryV2State state);

        // 历史新增条目事件（仅内部使用），conversationId 维度
        event System.Action<string, ConversationEntry> OnEntryRecorded;
    }
}


