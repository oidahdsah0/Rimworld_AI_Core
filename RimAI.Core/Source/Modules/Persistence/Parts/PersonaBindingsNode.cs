using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class PersonaBindingsNode : IPersistenceNode
    {
        public string Name => "RimAI_PersonaBindingsV1";

        public void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var items = snapshot?.PersonaBindings?.Items ?? new Dictionary<string, string>();
                Scribe_Dict.Look(ref items, "items");
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = items?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
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
                var items = result.PersonaBindings.Items;
                Scribe_Dict.Look(ref items, "items");
                items ??= new Dictionary<string, string>();
                result.PersonaBindings.Items = items;
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = items?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
