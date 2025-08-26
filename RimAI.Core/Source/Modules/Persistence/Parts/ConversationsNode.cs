using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.ScribeAdapters;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence.Parts
{
    internal sealed class ConversationsNode : IPersistenceNode
    {
        public string Name => "RimAI_ConversationsV2";

        public void Save(PersistenceSnapshot snapshot, List<NodeStat> statsCollector)
        {
            try
            {
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var convs = snapshot?.History?.Conversations ?? new Dictionary<string, ConversationRecord>();
                // 迁移自 PersistenceService：按“发言条目”为单位进行裁剪，防止字符串节点超限
                try
                {
                    const int scribeStringNodeMax = 32760; // 与 Scribe_Poco 中的字符串节点上限一致
                    const int safetyMargin = 256;          // 预留安全余量
                    int limit = scribeStringNodeMax - safetyMargin;
                    foreach (var key in convs.Keys.ToList())
                    {
                        var rec = convs[key];
                        if (rec == null) continue;
                        var entries = rec.Entries ?? new List<ConversationEntry>();
                        // 快速路径：无需截断
                        try
                        {
                            var initialLen = Newtonsoft.Json.JsonConvert.SerializeObject(rec).Length;
                            if (initialLen <= limit) continue;
                        }
                        catch { }

                        // 二分+保留最近 mid 条发言
                        int total = entries.Count;
                        int low = 0, high = total;
                        while (low < high)
                        {
                            int mid = (low + high + 1) / 2;
                            var test = new ConversationRecord
                            {
                                ParticipantIds = rec.ParticipantIds?.ToList() ?? new List<string>(),
                                Entries = mid > 0 ? entries.Skip(total - mid).ToList() : new List<ConversationEntry>()
                            };
                            int len;
                            try { len = Newtonsoft.Json.JsonConvert.SerializeObject(test).Length; }
                            catch { len = int.MaxValue; }
                            if (len <= limit) low = mid; else high = mid - 1;
                        }
                        if (low < total)
                        {
                            rec.Entries = low > 0 ? entries.Skip(total - low).ToList() : new List<ConversationEntry>();
                            var removed = total - low;
                            try { Verse.Log.Message($"[RimAI.Core][P6.Persistence] truncate=ConversationsV2 convId={key}, removed={removed}, kept={low}"); } catch { }
                        }
                    }
                }
                catch { }

                Scribe_Poco.LookJsonDict(ref convs, "items");
                Scribe.ExitNode();
                nodeSw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = convs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
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
                var nodeSw = System.Diagnostics.Stopwatch.StartNew();
                Scribe.EnterNode(Name);
                int schemaVersion = 2;
                Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
                var convs = result.History.Conversations;
                Scribe_Poco.LookJsonDict(ref convs, "items");
                convs ??= new Dictionary<string, ConversationRecord>();
                result.History.Conversations = convs;
                Scribe.ExitNode();
                nodeSw.Stop();
                statsCollector.Add(new NodeStat { Node = Name, Ok = true, Entries = convs?.Count ?? 0, BytesApprox = 0, ElapsedMs = nodeSw.ElapsedMilliseconds });
            }
            catch (Exception ex)
            {
                statsCollector.Add(new NodeStat { Node = Name, Ok = false, Error = ex.Message });
            }
        }
    }
}
