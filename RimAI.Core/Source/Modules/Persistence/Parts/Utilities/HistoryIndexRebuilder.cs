using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persistence.Parts.Utilities
{
    internal static class HistoryIndexRebuilder
    {
        public static (int convKeyFixed, int participantFixed) Rebuild(PersistenceSnapshot snap)
        {
            int ck = 0, pk = 0;
            if (snap?.History == null) return (0, 0);
            var convs = snap.History.Conversations ?? new Dictionary<string, ConversationRecord>();
            if (convs.Count == 0) return (0, 0);
            var convKeyIdx = new Dictionary<string, List<string>>();
            var partIdx = new Dictionary<string, List<string>>();
            foreach (var kv in convs)
            {
                var convId = kv.Key;
                var cr = kv.Value;
                if (cr?.ParticipantIds == null || cr.ParticipantIds.Count == 0) continue;
                var convKey = string.Join("|", cr.ParticipantIds.OrderBy(x => x));
                if (!convKeyIdx.TryGetValue(convKey, out var list1)) { list1 = new List<string>(); convKeyIdx[convKey] = list1; }
                if (!list1.Contains(convId)) list1.Add(convId);
                foreach (var pid in cr.ParticipantIds)
                {
                    if (!partIdx.TryGetValue(pid, out var list2)) { list2 = new List<string>(); partIdx[pid] = list2; }
                    if (!list2.Contains(convId)) list2.Add(convId);
                }
            }
            if (snap.History.ConvKeyIndex == null || snap.History.ConvKeyIndex.Count != convKeyIdx.Count) ck = Math.Abs((snap.History.ConvKeyIndex?.Count ?? 0) - convKeyIdx.Count);
            if (snap.History.ParticipantIndex == null || snap.History.ParticipantIndex.Count != partIdx.Count) pk = Math.Abs((snap.History.ParticipantIndex?.Count ?? 0) - partIdx.Count);
            snap.History.ConvKeyIndex = convKeyIdx;
            snap.History.ParticipantIndex = partIdx;
            return (ck, pk);
        }
    }
}
