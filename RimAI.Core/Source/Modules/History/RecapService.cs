using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Modules.LLM;
using RimAI.Core.Services;

namespace RimAI.Core.Modules.History
{
    /// <summary>
    /// 总结/前情提要服务的最小占位实现（M1）。
    /// 当前不进行实际 LLM 调用，仅维护轮次计数。
    /// </summary>
    internal sealed class RecapService : IRecapService
    {
        private readonly ILLMService _llm;
        private readonly IHistoryWriteService _history;
        private readonly ConcurrentDictionary<string, int> _roundCounters = new();

        public RecapService(ILLMService llm, IHistoryWriteService history)
        {
            _llm = llm;
            _history = history;
            // M1：不订阅事件源，由调用方手动触发 OnEntryRecorded
        }

        public void OnEntryRecorded(string convKey, ConversationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(convKey) || entry == null) return;
            _roundCounters.AddOrUpdate(convKey, 1, (_, n) => n + 1);
        }

        public void OnEveryTenRounds(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return;
            // M1：占位，无操作
        }

        public Task RebuildRecapAsync(string convKey, CancellationToken ct = default)
        {
            // M1：占位，无操作
            return Task.CompletedTask;
        }

        public int GetCounter(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return 0;
            return _roundCounters.TryGetValue(convKey, out var n) ? n : 0;
        }
    }
}


