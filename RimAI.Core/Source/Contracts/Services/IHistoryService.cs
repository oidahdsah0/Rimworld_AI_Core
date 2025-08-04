using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Data;

namespace RimAI.Core.Contracts.Services
{
    public interface IHistoryService
    {
        Task RecordEntryAsync(IEnumerable<string> participants, ConversationEntry entry);

        Task<HistoricalContext> GetHistoryAsync(IEnumerable<string> participants, int maxEntries = 50);

        object GetStateForPersistence();
        void LoadStateFromPersistence(object state);
    }
}