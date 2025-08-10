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
        // V2 主存：conversationId → ConversationRecord
        private readonly Dictionary<string, ConversationRecord> _conversations = new();
        // 二级索引：convKey → List<conversationId>
        private readonly Dictionary<string, List<string>> _convKeyIndex = new();
        // 二级索引：participantId → List<conversationId>
        private readonly Dictionary<string, List<string>> _participantIndex = new();
        private readonly object _gate = new();

        public event System.Action<string, ConversationEntry>? OnEntryRecorded;

        private static string BuildConvKey(IReadOnlyList<string> participantIds)
        {
            var sorted = participantIds.OrderBy(id => id, StringComparer.Ordinal);
            return string.Join("|", sorted);
        }

        public string CreateConversation(IReadOnlyList<string> participantIds)
        {
            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("participantIds cannot be null or empty", nameof(participantIds));
            var convId = Guid.NewGuid().ToString("N");
            var convKey = BuildConvKey(participantIds);
            lock (_gate)
            {
                var record = new ConversationRecord(convId, participantIds.ToList(), new List<ConversationEntry>());
                _conversations[convId] = record;
                // 索引：convKey
                if (!_convKeyIndex.TryGetValue(convKey, out var listByKey))
                {
                    listByKey = new List<string>();
                    _convKeyIndex[convKey] = listByKey;
                }
                listByKey.Add(convId);
                // 索引：participantId
                foreach (var pid in participantIds)
                {
                    if (!_participantIndex.TryGetValue(pid, out var list))
                    {
                        list = new List<string>();
                        _participantIndex[pid] = list;
                    }
                    list.Add(convId);
                }
            }
            return convId;
        }

        public Task AppendEntryAsync(string conversationId, ConversationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException(nameof(conversationId));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            lock (_gate)
            {
                if (!_conversations.TryGetValue(conversationId, out var record))
                    throw new KeyNotFoundException($"Conversation not found: {conversationId}");
                var newEntries = record.Entries.Concat(new[] { entry }).ToList();
                _conversations[conversationId] = new ConversationRecord(record.ConversationId, record.ParticipantIds, newEntries);
            }
            try { OnEntryRecorded?.Invoke(conversationId, entry); } catch { /* ignore */ }
            return Task.CompletedTask;
        }

        public Task<ConversationRecord> GetConversationAsync(string conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException(nameof(conversationId));
            lock (_gate)
            {
                if (!_conversations.TryGetValue(conversationId, out var record))
                    throw new KeyNotFoundException($"Conversation not found: {conversationId}");
                return Task.FromResult(record);
            }
        }

        public Task<IReadOnlyList<string>> FindByConvKeyAsync(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) throw new ArgumentException(nameof(convKey));
            lock (_gate)
            {
                if (_convKeyIndex.TryGetValue(convKey, out var list))
                    return Task.FromResult((IReadOnlyList<string>)list.ToList());
                return Task.FromResult((IReadOnlyList<string>)new List<string>());
            }
        }

        public Task<IReadOnlyList<string>> ListByParticipantAsync(string participantId)
        {
            if (string.IsNullOrWhiteSpace(participantId)) throw new ArgumentException(nameof(participantId));
            lock (_gate)
            {
                if (_participantIndex.TryGetValue(participantId, out var list))
                    return Task.FromResult((IReadOnlyList<string>)list.ToList());
                return Task.FromResult((IReadOnlyList<string>)new List<string>());
            }
        }

        public Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds)
        {
            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("participantIds cannot be null or empty", nameof(participantIds));

            var convKey = BuildConvKey(participantIds);
            List<Conversation> main = new();
            List<Conversation> background = new();

            lock (_gate)
            {
                if (_convKeyIndex.TryGetValue(convKey, out var byKey))
                {
                    foreach (var cid in byKey)
                    {
                        if (_conversations.TryGetValue(cid, out var rec))
                            main.Add(new Conversation(rec.Entries.ToList()));
                    }
                }

                // 背景：参与者交集（取交集的 conversationId）
                HashSet<string>? commonSet = null;
                foreach (var id in participantIds)
                {
                    if (!_participantIndex.TryGetValue(id, out var set))
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
                        // 跳过已在 main 中的 conv
                        if (_conversations.TryGetValue(cid, out var rec))
                        {
                            var conv = new Conversation(rec.Entries.ToList());
                            if (!main.Contains(conv)) background.Add(conv);
                        }
                    }
                }
            }
            return Task.FromResult(new HistoricalContext(main, background));
        }

        // --- Obsolete compatibility methods (required by IHistoryService) ---
        // 保留以满足接口签名，但内部走 V2 逻辑。
        public Task RecordEntryAsync(IReadOnlyList<string> participantIds, ConversationEntry entry)
        {
            if (participantIds == null || participantIds.Count == 0)
                throw new ArgumentException("participantIds cannot be null or empty", nameof(participantIds));
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            string convId;
            var convKey = BuildConvKey(participantIds);
            lock (_gate)
            {
                if (_convKeyIndex.TryGetValue(convKey, out var list) && list.Count > 0)
                {
                    convId = list[list.Count - 1]; // 选择最近的一个会话
                }
                else
                {
                    convId = CreateConversation(participantIds);
                }
            }
            return AppendEntryAsync(convId, entry);
        }

        public HistoryState GetStateForPersistence()
        {
            // V2 已不再使用 V1 的 HistoryState；此方法仅为满足旧接口存在。
            throw new NotSupportedException("GetStateForPersistence (V1) is not supported in History V2. Use GetV2StateForPersistence instead.");
        }

        public void LoadStateFromPersistence(HistoryState state)
        {
            // V2 已不再使用 V1 的 HistoryState；此方法仅为满足旧接口存在。
            throw new NotSupportedException("LoadStateFromPersistence (V1) is not supported in History V2. Use LoadV2StateFromPersistence instead.");
        }

        public Task EditEntryAsync(string conversationId, int entryIndex, string newContent)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException(nameof(conversationId));
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            if (newContent == null) throw new ArgumentNullException(nameof(newContent));
            lock (_gate)
            {
                if (!_conversations.TryGetValue(conversationId, out var rec))
                    throw new KeyNotFoundException($"Conversation not found: {conversationId}");
                if (entryIndex >= rec.Entries.Count)
                    throw new ArgumentOutOfRangeException(nameof(entryIndex));
                var old = rec.Entries[entryIndex];
                var edited = new ConversationEntry(old.SpeakerId, newContent, old.Timestamp);
                var list = rec.Entries.ToList();
                list[entryIndex] = edited;
                _conversations[conversationId] = new ConversationRecord(rec.ConversationId, rec.ParticipantIds, list);
            }
            return Task.CompletedTask;
        }

        public Task DeleteEntryAsync(string conversationId, int entryIndex)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException(nameof(conversationId));
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            lock (_gate)
            {
                if (!_conversations.TryGetValue(conversationId, out var rec))
                    throw new KeyNotFoundException($"Conversation not found: {conversationId}");
                if (entryIndex >= rec.Entries.Count)
                    throw new ArgumentOutOfRangeException(nameof(entryIndex));
                var list = rec.Entries.ToList();
                list.RemoveAt(entryIndex);
                _conversations[conversationId] = new ConversationRecord(rec.ConversationId, rec.ParticipantIds, list);
            }
            return Task.CompletedTask;
        }

        public Task RestoreEntryAsync(string conversationId, int entryIndex, ConversationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(conversationId)) throw new ArgumentException(nameof(conversationId));
            if (entryIndex < 0) throw new ArgumentOutOfRangeException(nameof(entryIndex));
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            lock (_gate)
            {
                if (!_conversations.TryGetValue(conversationId, out var rec))
                {
                    var list = new List<ConversationEntry> { entry };
                    _conversations[conversationId] = new ConversationRecord(conversationId, Array.Empty<string>(), list);
                }
                else
                {
                    var list = rec.Entries.ToList();
                    entryIndex = Math.Min(Math.Max(0, entryIndex), list.Count);
                    list.Insert(entryIndex, entry);
                    _conversations[conversationId] = new ConversationRecord(rec.ConversationId, rec.ParticipantIds, list);
                }
            }
            return Task.CompletedTask;
        }

        public HistoryV2State GetV2StateForPersistence()
        {
            lock (_gate)
            {
                var convCopy = _conversations.ToDictionary(k => k.Key, v => v.Value);
                var keyIndexCopy = _convKeyIndex.ToDictionary(k => k.Key, v => v.Value.ToList());
                var partIndexCopy = _participantIndex.ToDictionary(k => k.Key, v => v.Value.ToList());
                return new HistoryV2State(convCopy, keyIndexCopy, partIndexCopy);
            }
        }

        public void LoadV2StateFromPersistence(HistoryV2State state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            lock (_gate)
            {
                _conversations.Clear();
                _convKeyIndex.Clear();
                _participantIndex.Clear();
                foreach (var kvp in state.Conversations) _conversations[kvp.Key] = kvp.Value;
                foreach (var kvp in state.ConvKeyIndex) _convKeyIndex[kvp.Key] = kvp.Value?.ToList() ?? new List<string>();
                foreach (var kvp in state.ParticipantIndex) _participantIndex[kvp.Key] = kvp.Value?.ToList() ?? new List<string>();
            }
        }
    }
}
