using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Contracts.Services;
using Verse;

#nullable enable

namespace RimAI.Core.Services
{
    public class HistoryService : IHistoryService
    {
        private readonly ISchedulerService _scheduler;
        private readonly Dictionary<string, Conversation> _conversations = new();
        private readonly Dictionary<string, HashSet<string>> _participantIndex = new();
        private readonly object _lock = new();
        private const int MAX_ENTRIES_PER_CONVERSATION = 500; // 防止内存无限增长

        public HistoryService(ISchedulerService scheduler)
        {
            _scheduler = scheduler;
        }

        public Task RecordEntryAsync(IEnumerable<string> participants, ConversationEntry entry)
        {
            var partArray = participants.Distinct().OrderBy(p => p).ToArray();
            if (partArray.Length == 0) throw new ArgumentException("participants empty");
            entry.Timestamp = DateTime.UtcNow;
            var convId = string.Join("|", partArray);

            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                lock (_lock)
                {
                    if (!_conversations.TryGetValue(convId, out var conv))
                    {
                        conv = new Conversation { Id = convId, Participants = new HashSet<string>(partArray) };
                        _conversations[convId] = conv;
                        foreach (var p in partArray)
                        {
                            if (!_participantIndex.TryGetValue(p, out var set))
                            {
                                set = new HashSet<string>();
                                _participantIndex[p] = set;
                            }
                            set.Add(convId);
                        }
                    }
                    conv.Entries.Add(entry);
                    if (conv.Entries.Count > MAX_ENTRIES_PER_CONVERSATION)
                        conv.Entries.RemoveRange(0, conv.Entries.Count - MAX_ENTRIES_PER_CONVERSATION);
                }
                return 0;
            });
        }

        public Task<HistoricalContext> GetHistoryAsync(IEnumerable<string> participants, int maxEntries = 50)
        {
            var partSet = participants.ToHashSet();
            var convId = string.Join("|", partSet.OrderBy(p => p));
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                var ctx = new HistoricalContext();
                lock (_lock)
                {
                    if (_conversations.TryGetValue(convId, out var mainConv))
                        ctx.Mainline = mainConv.Entries.TakeLast(maxEntries).ToList();

                    // 背景
                    var candidate = partSet.Select(p => _participantIndex.TryGetValue(p, out var s) ? s : new HashSet<string>())
                                            .Aggregate((a, b) => { a.IntersectWith(b); return a; });
                    foreach (var id in candidate.Where(i => i != convId))
                    {
                        if (_conversations.TryGetValue(id, out var c))
                            ctx.Background.AddRange(c.Entries.TakeLast(maxEntries));
                    }
                }
                ctx.Background = ctx.Background.OrderBy(e => e.Timestamp).TakeLast(maxEntries).ToList();
                return ctx;
            });
        }

        public object GetStateForPersistence()
        {
            lock (_lock)
            {
                return new RimAI.Core.Contracts.Data.HistoryStateSnapshot { Conversations = _conversations.Values.Select(CloneConversation).ToList() };
            }
        }

        public void LoadStateFromPersistence(object state)
        {
            if (state is not RimAI.Core.Contracts.Data.HistoryStateSnapshot snap) return;
            lock (_lock)
            {
                _conversations.Clear();
                _participantIndex.Clear();
                foreach (var conv in snap.Conversations)
                {
                    _conversations[conv.Id] = conv;
                    foreach (var p in conv.Participants)
                    {
                        if (!_participantIndex.TryGetValue(p, out var set))
                        {
                            set = new HashSet<string>();
                            _participantIndex[p] = set;
                        }
                        set.Add(conv.Id);
                    }
                }
            }
        }

        private static Conversation CloneConversation(Conversation original) => new()
        {
            Id = original.Id,
            Participants = new HashSet<string>(original.Participants),
            Entries = original.Entries.Select(e => new ConversationEntry { Role = e.Role, Content = e.Content, Timestamp = e.Timestamp }).ToList()
        };

        private class Snapshot { public List<Conversation> Conversations { get; set; } = new(); }
    }
}