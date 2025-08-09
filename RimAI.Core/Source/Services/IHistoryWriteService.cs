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
        Task RecordEntryAsync(IReadOnlyList<string> participantIds, ConversationEntry entry);
        HistoryState GetStateForPersistence();
        void LoadStateFromPersistence(HistoryState state);
        
        // --- P10-M1: 扩展能力（内部） ---
        System.Threading.Tasks.Task EditEntryAsync(string convKey, int entryIndex, string newContent);
        System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<string>> ListConversationKeysAsync(string filter = null, int? skip = null, int? take = null);
        System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Conversation>> GetConversationsBySubsetAsync(System.Collections.Generic.IReadOnlyList<string> queryIds);

        /// <summary>
        /// 历史新增条目事件（仅内部使用）。
        /// </summary>
        event System.Action<string, ConversationEntry> OnEntryRecorded;
    }
}


