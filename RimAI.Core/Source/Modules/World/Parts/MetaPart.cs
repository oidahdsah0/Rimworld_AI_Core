using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;
using System.Linq;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class MetaPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public MetaPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        // 杂项：当前绝对 tick（安全在主线程读取）
        public Task<long> GetNowAbsTicksAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => (long)(Find.TickManager?.TicksAbs ?? 0), name: "Meta.NowAbs", ct: cts.Token);
        }

        // 杂项：当前地图的“密文种子”（用于生成短伪加密串）
        public Task<int> GetCurrentMapCipherSeedAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                var map = Find.CurrentMap;
                int s = (int)(Find.TickManager?.TicksGame ?? 0) ^ 0x5F3759DF;
                try { if (map != null) unchecked { s ^= (map.uniqueID * 397) ^ map.Tile; } } catch { }
                return s;
            }, name: "Meta.CipherSeed", ct: cts.Token);
        }

        public Task<string> GetCurrentGameTimeStringAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    var abs = Find.TickManager?.TicksAbs ?? 0;
                    int tile = Find.CurrentMap?.Tile ?? 0;
                    var longLat = Find.WorldGrid?.LongLatOf(tile) ?? UnityEngine.Vector2.zero;
                    return GenDate.DateFullStringAt(abs, longLat);
                }
                catch
                {
                    return System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                }
            }, name: "GetCurrentGameTimeString", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<GameLogItem>> GetRecentGameLogsAsync(int maxCount, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                var list = new List<GameLogItem>();
                try
                {
                    var logs = Find.PlayLog?.AllEntries ?? new List<LogEntry>();
                    for (int i = logs.Count - 1; i >= 0 && list.Count < Mathf.Max(0, maxCount); i--)
                    {
                        var e = logs[i]; if (e == null) continue;
                        string text = string.Empty;
                        try
                        {
                            if (e is PlayLogEntry_Interaction inter)
                            {
                                Pawn pov = null;
                                try { pov = inter.GetConcerns()?.OfType<Pawn>()?.FirstOrDefault(); } catch { }
                                try { text = inter.ToGameStringFromPOV(pov, false); }
                                catch { text = inter.ToString(); }
                            }
                            else
                            {
                                text = e.ToString();
                            }
                        }
                        catch { try { text = e.ToString(); } catch { text = string.Empty; } }
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        string time = string.Empty;
                        try
                        {
                            var abs = Find.TickManager?.TicksAbs ?? 0;
                            int tile = Find.CurrentMap?.Tile ?? 0;
                            var longLat = Find.WorldGrid?.LongLatOf(tile) ?? UnityEngine.Vector2.zero;
                            time = GenDate.DateFullStringAt(abs, longLat);
                        }
                        catch { time = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"); }
                        list.Add(new GameLogItem { GameTime = time, Text = text });
                    }
                }
                catch { }
                return (System.Collections.Generic.IReadOnlyList<GameLogItem>)list;
            }, name: "GetRecentGameLogs", ct: cts.Token);
        }
    }
}
