using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using Verse;

namespace RimAI.Core.Services
{
    public class HistoryService : IHistoryService, IPersistable
    {
        private Dictionary<string, List<ConversationEntry>> _conversationStore = new Dictionary<string, List<ConversationEntry>>();
        private Dictionary<string, HashSet<string>> _participantIndex = new Dictionary<string, HashSet<string>>();

        public string StartOrGetConversation(List<string> participantIds)
        {
            if (participantIds == null || participantIds.Count == 0)
            {
                Log.Warning("Attempted to start a conversation with no participants.");
                return null;
            }

            var sortedIds = participantIds.Distinct().OrderBy(id => id).ToList();
            var conversationId = string.Join("_", sortedIds);

            if (!_conversationStore.ContainsKey(conversationId))
            {
                _conversationStore[conversationId] = new List<ConversationEntry>();
                foreach (var id in sortedIds)
                {
                    if (!_participantIndex.ContainsKey(id))
                    {
                        _participantIndex[id] = new HashSet<string>();
                    }
                    _participantIndex[id].Add(conversationId);
                }
            }

            return conversationId;
        }

        public void AddEntry(string conversationId, ConversationEntry entry)
        {
            if (string.IsNullOrEmpty(conversationId) || entry == null) return;

            if (_conversationStore.TryGetValue(conversationId, out var history))
            {
                history.Add(entry);
            }
            else
            {
                Log.Warning($"Attempted to add entry to a non-existent conversation: {conversationId}");
            }
        }

        public HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10)
        {
            var context = new HistoricalContext();
            if (primaryParticipants == null || primaryParticipants.Count == 0) return context;

            var sortedPrimaryIds = primaryParticipants.Distinct().OrderBy(id => id).ToList();
            var primaryConversationId = string.Join("_", sortedPrimaryIds);

            // 1. Get Primary History
            if (_conversationStore.TryGetValue(primaryConversationId, out var primaryHistory))
            {
                context.PrimaryHistory = primaryHistory.OrderByDescending(e => e.GameTicksTimestamp).Take(limit).Reverse().ToList();
            }

            // 2. Find all relevant conversations using the inverted index
            HashSet<string> relevantConversationIds = null;
            foreach (var id in sortedPrimaryIds)
            {
                if (_participantIndex.TryGetValue(id, out var conversations))
                {
                    if (relevantConversationIds == null)
                    {
                        relevantConversationIds = new HashSet<string>(conversations);
                    }
                    else
                    {
                        relevantConversationIds.IntersectWith(conversations);
                    }
                }
                else
                {
                    // If any participant is not in the index, there can be no common conversations.
                    return context;
                }
            }

            if (relevantConversationIds == null) return context;

            // 3. Filter for ancillary history and combine
            var ancillaryHistory = new List<ConversationEntry>();
            foreach (var convId in relevantConversationIds)
            {
                if (convId != primaryConversationId)
                {
                    if (_conversationStore.TryGetValue(convId, out var history))
                    {
                        ancillaryHistory.AddRange(history);
                    }
                }
            }
            
            context.AncillaryHistory = ancillaryHistory.OrderByDescending(e => e.GameTicksTimestamp).Take(limit).Reverse().ToList();

            return context;
        }
        
        public void ExposeData()
        {
            Scribe_Collections.Look(ref _conversationStore, "conversationStore", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref _participantIndex, "participantIndex", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _conversationStore ??= new Dictionary<string, List<ConversationEntry>>();
                _participantIndex ??= new Dictionary<string, HashSet<string>>();
            }
        }
    }
} 