using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class UnknownCivNode : IPersistenceNode
    {
        public string Name => "RimAI_UnknownCivV1";

        public void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var state = snapshot?.UnknownCiv ?? new UnknownCivState();
                Scribe_Poco.LookJson(ref state, "state");
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = 1, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
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
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var state = result.UnknownCiv;
                Scribe_Poco.LookJson(ref state, "state");
                state ??= new UnknownCivState();
                result.UnknownCiv = state;
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = 1, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
