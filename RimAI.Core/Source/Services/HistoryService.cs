#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 认知历史服务的默认实现（内存版）。
    /// 目标：提供 P6 持久化所需的状态导入/导出能力，
    /// 线程安全采用 <see cref="lock"/> 简单保护即可满足当前需求。
    /// </summary>
    internal sealed class HistoryService : IHistoryService, RimAI.Core.Contracts.Services.IHistoryQueryService, IHistoryWriteService
    {
        private readonly Dictionary<string, Conversation> _primaryStore = new();
        private readonly Dictionary<string, HashSet<string>> _invertedIndex = new();
        private readonly object _gate = new();
        
        public event System.Action<string, ConversationEntry>? OnEntryRecorded;

        public Task RecordEntryAsync(IReadOnlyList<string> participantIds, ConversationEntry entry)
        {
            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("participantIds cannot be null or empty", nameof(participantIds));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var convId = GetConversationId(participantIds);
            lock (_gate)
            {
                if (!_primaryStore.TryGetValue(convId, out var conv))
                {
                    conv = new Conversation(new List<ConversationEntry>());
                    _primaryStore[convId] = conv;
                }

                // 写入条目（由于 Conversation 封装为只读，需重新包装）
                var updatedEntries = conv.Entries.Concat(new[] { entry }).ToList();
                _primaryStore[convId] = new Conversation(updatedEntries);

                foreach (var id in participantIds)
                {
                    if (!_invertedIndex.TryGetValue(id, out var set))
                    {
                        set = new HashSet<string>();
                        _invertedIndex[id] = set;
                    }
                    set.Add(convId);
                }
            }
            // 事件回调放在锁外，避免潜在的重入与死锁
            try { OnEntryRecorded?.Invoke(convId, entry); } catch { /* ignore */ }
            return Task.CompletedTask;
        }

        public Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds)
        {
            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("participantIds cannot be null or empty", nameof(participantIds));

            var convId = GetConversationId(participantIds);
            List<Conversation> main = new();
            List<Conversation> background = new();

            lock (_gate)
            {
                if (_primaryStore.TryGetValue(convId, out var conv))
                {
                    main.Add(conv);
                }

                // 背景：包含所有参与者交集的对话
                HashSet<string>? commonSet = null;
                foreach (var id in participantIds)
                {
                    if (!_invertedIndex.TryGetValue(id, out var set))
                    {
                        commonSet = null;
                        break;
                    }
                    commonSet = commonSet == null ? new HashSet<string>(set) : new HashSet<string>(commonSet.Intersect(set));
                }
                if (commonSet != null)
                {
                    foreach (var cid in commonSet)
                    {
                        if (cid == convId) continue; // exclude main
                        if (_primaryStore.TryGetValue(cid, out var c))
                            background.Add(c);
                    }
                }
            }

            var ctx = new HistoricalContext(main, background);
            return Task.FromResult(ctx);
        }

        public HistoryState GetStateForPersistence()
        {
            lock (_gate)
            {
                // 深拷贝以免外部修改
                var primaryCopy = _primaryStore.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                var indexCopy = _invertedIndex.ToDictionary(kvp => kvp.Key, kvp => new HashSet<string>(kvp.Value));
                return new HistoryState(primaryCopy, indexCopy);
            }
        }

        public void LoadStateFromPersistence(HistoryState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (_gate)
            {
                _primaryStore.Clear();
                _invertedIndex.Clear();

                foreach (var kvp in state.PrimaryStore)
                    _primaryStore[kvp.Key] = kvp.Value;
                foreach (var kvp in state.InvertedIndex)
                    _invertedIndex[kvp.Key] = new HashSet<string>(kvp.Value);
            }
        }

        // --- P10-M1: 新增内部能力 ---

        public Task EditEntryAsync(string convKey, int entryIndex, string newContent)
        {
            if (string.IsNullOrWhiteSpace(convKey)) throw new ArgumentException("convKey cannot be null or empty", nameof(convKey));
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            if (newContent == null) throw new ArgumentNullException(nameof(newContent));

            lock (_gate)
            {
                if (!_primaryStore.TryGetValue(convKey, out var conv))
                    throw new KeyNotFoundException($"Conversation not found: {convKey}");
                if (entryIndex >= conv.Entries.Count)
                    throw new ArgumentOutOfRangeException(nameof(entryIndex));

                var old = conv.Entries[entryIndex];
                var edited = new ConversationEntry(old.SpeakerId, newContent, old.Timestamp);
                var list = conv.Entries.ToList();
                list[entryIndex] = edited;
                _primaryStore[convKey] = new Conversation(list);
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListConversationKeysAsync(string? filter = null, int? skip = null, int? take = null)
        {
            List<string> keys;
            lock (_gate)
            {
                keys = _primaryStore.Keys.ToList();
            }
            if (!string.IsNullOrWhiteSpace(filter))
            {
                keys = keys.Where(k => k.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            if (skip.HasValue && skip.Value > 0) keys = keys.Skip(skip.Value).ToList();
            if (take.HasValue && take.Value >= 0) keys = keys.Take(take.Value).ToList();
            return Task.FromResult((IReadOnlyList<string>)keys);
        }

        public Task<IReadOnlyList<Conversation>> GetConversationsBySubsetAsync(IReadOnlyList<string> queryIds)
        {
            if (queryIds == null || queryIds.Count == 0)
                throw new ArgumentException("queryIds cannot be null or empty", nameof(queryIds));

            HashSet<string>? resultIds = null;
            lock (_gate)
            {
                foreach (var id in queryIds)
                {
                    if (!_invertedIndex.TryGetValue(id, out var set))
                    {
                        resultIds = new HashSet<string>();
                        break;
                    }
                    resultIds = resultIds == null ? new HashSet<string>(set) : new HashSet<string>(resultIds.Intersect(set));
                    if (resultIds.Count == 0) break;
                }

                var res = new List<Conversation>();
                if (resultIds != null && resultIds.Count > 0)
                {
                    foreach (var cid in resultIds)
                    {
                        if (_primaryStore.TryGetValue(cid, out var conv))
                            res.Add(conv);
                    }
                }
                return Task.FromResult((IReadOnlyList<Conversation>)res);
            }
        }

        private static string GetConversationId(IReadOnlyList<string> participantIds)
        {
            var sorted = participantIds.OrderBy(id => id, StringComparer.Ordinal);
            return string.Join("|", sorted);
        }
    }
}
