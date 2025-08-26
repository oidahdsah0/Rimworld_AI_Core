using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class BiographiesNode : IPersistenceNode
    {
        public string Name => "RimAI_BiographiesV1";
        private readonly Func<int> _getMaxTextLength;

        public BiographiesNode(Func<int> getMaxTextLength)
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
                var bios = snapshot?.Biographies?.Items ?? new Dictionary<string, List<BiographyItem>>();
                // 按配置最大长度裁剪
                try
                {
                    int maxLen = Math.Max(0, _getMaxTextLength());
                    foreach (var list in bios.Values)
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
                Scribe_Poco.LookJsonDict(ref bios, "items");
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = bios?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
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
                var bios = result.Biographies.Items;
                Scribe_Poco.LookJsonDict(ref bios, "items");
                bios ??= new Dictionary<string, List<BiographyItem>>();
                result.Biographies.Items = bios;
                Scribe.ExitNode();
                sw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = bios?.Count ?? 0, BytesApprox = 0, ElapsedMs = sw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
