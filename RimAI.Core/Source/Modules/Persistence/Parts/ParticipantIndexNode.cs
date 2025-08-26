using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class ParticipantIndexNode : IPersistenceNode
    {
        public string Name => "RimAI_ParticipantIndexV2";

        public void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx = snapshot?.History?.ParticipantIndex ?? new Dictionary<string, List<string>>();
                Scribe_Dict.Look(ref idx, "items");
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = idx?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }

        public void Load(PersistenceSnapshot result, List<NodeStat> statsCollector)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var idx = result.History.ParticipantIndex;
                Scribe_Dict.Look(ref idx, "items");
                idx ??= new Dictionary<string, List<string>>();
                result.History.ParticipantIndex = idx;
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = idx?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
