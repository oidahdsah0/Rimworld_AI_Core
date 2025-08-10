using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RimAI.Core.Services
{
    internal interface IRelationsIndexService
    {
        Task<IReadOnlyList<string>> ListConversationsByParticipantAsync(string participantId);
    }

    internal sealed class RelationsIndexService : IRelationsIndexService
    {
        private readonly IHistoryWriteService _history;
        public RelationsIndexService(IHistoryWriteService history)
        {
            _history = history;
        }

        public async Task<IReadOnlyList<string>> ListConversationsByParticipantAsync(string participantId)
        {
            if (string.IsNullOrWhiteSpace(participantId)) return Array.Empty<string>();
            // 通过 History v2 暴露的只读方法实现
            var list = await _history.ListByParticipantAsync(participantId);
            return list?.ToList() ?? new List<string>();
        }
    }
}


