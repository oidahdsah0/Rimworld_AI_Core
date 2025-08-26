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
    internal sealed class AlertDigestPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public AlertDigestPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<AlertDigestSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => BuildSnapshotSafe(), name: "GetAlertDigest", ct: cts.Token);
        }

        private AlertDigestSnapshot BuildSnapshotSafe()
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing || Find.Alerts == null)
                {
                    return new AlertDigestSnapshot { Alerts = Array.Empty<AlertItem>() };
                }

                // 驱动 UI 更新一次，确保 activeAlerts 刷新
                try { Find.Alerts.AlertsReadoutUpdate(); } catch { }

                var items = new List<AlertItem>();

                // 通过反射访问 AlertsReadout.activeAlerts（私有）以获取当前活跃警报列表
                try
                {
                    var readout = Find.Alerts;
                    var fi = typeof(AlertsReadout).GetField("activeAlerts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var list = fi?.GetValue(readout) as IEnumerable<Alert>;
                    foreach (var a in list ?? Enumerable.Empty<Alert>())
                    {
                        if (a == null) continue;
                        string id = a.GetType().FullName;
                        string label = SafeLabel(a);
                        string sev = SeverityToString(a.Priority);
                        string hint = SafeExplanation(a);
                        items.Add(new AlertItem { Id = id, Label = label, Severity = sev, Hint = hint });
                    }
                }
                catch { }

                // 排序：Critical > High > Medium，其次 Label
                int Rank(string s) => s == "critical" ? 2 : s == "high" ? 1 : 0;
                items = items
                    .OrderByDescending(i => Rank(i.Severity))
                    .ThenBy(i => i.Label)
                    .ToList();

                return new AlertDigestSnapshot { Alerts = items };
            }
            catch (Exception ex)
            {
                try { Log.Warning($"[RimAI.Core] Alert digest failed: {ex.Message}"); } catch { }
                return new AlertDigestSnapshot { Alerts = Array.Empty<AlertItem>() };
            }
        }

        private static string SeverityToString(AlertPriority p)
        {
            try
            {
                switch (p)
                {
                    case AlertPriority.Critical: return "critical";
                    case AlertPriority.High: return "high";
                    default: return "medium";
                }
            }
            catch { return "medium"; }
        }

        private static string SafeLabel(Alert a)
        {
            try { return a.Label; } catch { }
            try { return a.GetLabel(); } catch { }
            return a?.GetType()?.Name ?? "alert";
        }

        private static string SafeExplanation(Alert a)
        {
            try { return a.GetExplanation().Resolve(); } catch { }
            try { return a.GetLabel(); } catch { }
            return null;
        }
    }
}
