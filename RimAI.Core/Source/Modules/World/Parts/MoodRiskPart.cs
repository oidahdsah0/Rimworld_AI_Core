using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    // v1：心情分布与主因（使用 ThoughtHandler 正确聚合）
    internal sealed class MoodRiskPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        private const int DefaultTopN = 5;
        private const float NearDelta = 0.05f; // 距轻度阈值 5%

        public MoodRiskPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("MoodRiskPart requires ConfigurationService");
        }

        public Task<MoodRiskSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;

                int nValid = 0;
                float sumMood = 0f;
                int minor = 0, major = 0, extreme = 0, nearBreak = 0;
                var causeImpact = new Dictionary<string, (float impact, HashSet<int> pawns)>(StringComparer.Ordinal);

                // 临时列表由 ThoughtHandler 管理，我们只持有拷贝结果
                var tmpGroups = new List<Thought>();

                foreach (var p in pawns)
                {
                    try
                    {
                        var mood = p?.needs?.mood;
                        var breaker = p?.mindState?.mentalBreaker;
                        if (mood == null || breaker == null) continue;
                        float cur = mood.CurLevel;
                        nValid++; sumMood += cur;

                        float thMinor = breaker.BreakThresholdMinor;
                        float thMajor = breaker.BreakThresholdMajor;
                        float thExtreme = breaker.BreakThresholdExtreme;
                        if (cur <= thExtreme) extreme++;
                        else if (cur <= thMajor) major++;
                        else if (cur <= thMinor) minor++;
                        if (cur <= thMinor + NearDelta) nearBreak++;

                        // 思潮主因：用“去重的思潮分组 + 组加权偏移”保证与 UI 一致
                        var th = p.needs.mood.thoughts;
                        tmpGroups.Clear();
                        th.GetDistinctMoodThoughtGroups(tmpGroups);
                        for (int i = 0; i < tmpGroups.Count; i++)
                        {
                            var g = tmpGroups[i];
                            float offset = th.MoodOffsetOfGroup(g);
                            if (offset >= 0f) continue; // 仅负面
                            string label = SafeThoughtLabel(g);
                            if (string.IsNullOrEmpty(label)) label = g.def?.defName ?? "(unknown)";
                            if (!causeImpact.TryGetValue(label, out var tup))
                            {
                                tup = (0f, new HashSet<int>());
                            }
                            tup.impact += -offset; // 取绝对值累加
                            tup.pawns.Add(p.thingIDNumber);
                            causeImpact[label] = tup;
                        }
                    }
                    catch { }
                }

                float avg = nValid > 0 ? (sumMood / nValid) : 0f;
                var causes = causeImpact
                    .Select(kv => new MoodCauseItem { Label = kv.Key, TotalImpact = kv.Value.impact, PawnsAffected = kv.Value.pawns.Count })
                    .OrderByDescending(c => c.TotalImpact)
                    .Take(DefaultTopN)
                    .ToList();

                return new MoodRiskSnapshot
                {
                    AvgPct = avg,
                    MinorCount = minor,
                    MajorCount = major,
                    ExtremeCount = extreme,
                    NearBreakCount = nearBreak,
                    TopCauses = causes
                };
            }, name: "MoodRiskPart.Get", ct: cts.Token);
        }

        private static string SafeThoughtLabel(Thought t)
        {
            try { var s = t.LabelCap.ToString(); if (!string.IsNullOrWhiteSpace(s)) return s; } catch { }
            try { return t.def?.label ?? t.def?.defName; } catch { }
            return null;
        }
    }
}
