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
    }
}


