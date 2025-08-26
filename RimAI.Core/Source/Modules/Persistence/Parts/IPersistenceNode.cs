using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal interface IPersistenceNode
    {
        string Name { get; }
        void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector);
        void Load(PersistenceSnapshot result, List<NodeStat> statsCollector);
    }
}
