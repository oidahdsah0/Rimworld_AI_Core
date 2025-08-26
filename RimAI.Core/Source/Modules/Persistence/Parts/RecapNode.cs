using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class RecapNode : IPersistenceNode
    {
        public string Name => "RimAI_RecapV1";
        private readonly Func<int> _getMaxTextLength;

        public RecapNode(Func<int> getMaxTextLength)
        {
            _getMaxTextLength = getMaxTextLength ?? (() => 4000);
        }

        public void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 1;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 1);
                var recaps = snapshot?.Recap?.Recaps ?? new Dictionary<string, List<RecapSnapshotItem>>();
                // 按配置最大长度裁剪
                try
                {
                    int maxLen = Math.Max(0, _getMaxTextLength());
                    foreach (var list in recaps.Values)
                    {
                        if (list == null) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var t = list[i]?.Text ?? string.Empty;
                            if (t.Length > maxLen) list[i].Text = t.Substring(0, maxLen);
                        }
                    }
                }
                catch { }
                Scribe_Poco.LookJsonDict(ref recaps, "items");
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = recaps?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
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
                var recaps = result.Recap.Recaps;
                Scribe_Poco.LookJsonDict(ref recaps, "items");
                recaps ??= new Dictionary<string, List<RecapSnapshotItem>>();
                result.Recap.Recaps = recaps;
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = recaps?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
