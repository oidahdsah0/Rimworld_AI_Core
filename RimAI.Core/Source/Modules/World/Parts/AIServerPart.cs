using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;
using RimWorld;
using UnityEngine;
using System.Linq;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class AIServerPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public AIServerPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<System.Collections.Generic.IReadOnlyList<int>> GetPoweredAiServerThingIdsAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            // 指定名称用于 Scheduler 的长任务诊断；origin 会在 Scheduler 内自动捕获
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var ids = new List<int>();
                try
                {
                    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "RimAI_AIServer_Lv1A","RimAI_AIServer_Lv2A","RimAI_AIServer_Lv3A"
                    };
                    foreach (var map in Find.Maps)
                    {
                        if (map == null) continue;
                        var things = map.listerThings?.AllThings;
                        if (things == null) continue;
                        foreach (var t in things)
                        {
                            if (t == null) continue;
                            var defName = t.def?.defName;
                            if (string.IsNullOrEmpty(defName) || !allowed.Contains(defName)) continue;
                            var power = t.TryGetComp<CompPowerTrader>();
                            if (power == null || !power.PowerOn) continue;
                            ids.Add(t.thingIDNumber);
                        }
                    }
                }
                catch { }
                return (System.Collections.Generic.IReadOnlyList<int>)ids;
            }, name: "GetPoweredAiServerThingIds", ct: cts.Token);
        }

        public Task<int> GetAiServerLevelAsync(int thingId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                try
                {
                    foreach (var map in Find.Maps)
                    {
                        var things = map?.listerThings?.AllThings; if (things == null) continue;
                        foreach (var t in things)
                        {
                            if (t == null || t.thingIDNumber != thingId) continue;
                            var def = t.def?.defName ?? string.Empty;
                            if (def.IndexOf("Lv1", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
                            if (def.IndexOf("Lv2", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
                            if (def.IndexOf("Lv3", StringComparison.OrdinalIgnoreCase) >= 0) return 3;
                            return 1;
                        }
                    }
                }
                catch { }
                return 1;
            }, name: "GetAiServerLevel", ct: cts.Token);
        }

        public Task<bool> ExistsAsync(int thingId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                try
                {
                    foreach (var map in Find.Maps)
                    {
                        var things = map?.listerThings?.AllThings; if (things == null) continue;
                        foreach (var t in things)
                        {
                            if (t == null) continue;
                            if (t.thingIDNumber == thingId) return true;
                        }
                    }
                }
                catch { }
                return false;
            }, name: "AiServerExists", ct: cts.Token);
        }

        public Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var snap = new AiServerSnapshot { ServerId = serverId, TemperatureC = 37, LoadPercent = 50, PowerOn = false, HasAlarm = false };
                try
                {
                    int thingId = 0;
                    if (!string.IsNullOrWhiteSpace(serverId))
                    {
                        var idText = serverId;
                        if (idText.StartsWith("thing:", StringComparison.OrdinalIgnoreCase)) idText = idText.Substring(6);
                        else if (idText.StartsWith("server:", StringComparison.OrdinalIgnoreCase)) idText = idText.Substring(7);
                        int.TryParse(idText, out thingId);
                    }

                    Thing found = null;
                    foreach (var map in Find.Maps)
                    {
                        var things = map?.listerThings?.AllThings; if (things == null) continue;
                        foreach (var t in things)
                        {
                            if (t == null) continue;
                            if (thingId != 0)
                            {
                                if (t.thingIDNumber == thingId) { found = t; break; }
                            }
                            else
                            {
                                var defName = t.def?.defName;
                                if (string.IsNullOrEmpty(defName)) continue;
                                if (defName.StartsWith("RimAI_AIServer_", StringComparison.OrdinalIgnoreCase)) { found = t; break; }
                            }
                        }
                        if (found != null) break;
                    }

                    if (found != null)
                    {
                        try { var power = found.TryGetComp<CompPowerTrader>(); snap.PowerOn = (power != null && power.PowerOn); } catch { snap.PowerOn = false; }
                        float temp = 37f;
                        try
                        {
                            var map = found.Map;
                            var pos = found.Position;
                            var room = pos.GetRoom(map);
                            if (room != null)
                            {
                                temp = room.Temperature;
                            }
                            else
                            {
                                try { temp = GenTemperature.GetTemperatureForCell(pos, map); } catch { temp = found.AmbientTemperature; }
                            }
                        }
                        catch { try { temp = found.AmbientTemperature; } catch { temp = 37f; } }
                        snap.TemperatureC = Mathf.RoundToInt(temp);
                    }
                }
                catch { }
                return snap;
            }, name: "GetAiServerSnapshot", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<string>> GetUniqueLoadedServerToolsAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                // 虽然 ServerService 是托管数据，但统一走主线程，避免潜在并发可见性问题
                try
                {
                    var server = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
                    var list = server?.List() ?? new List<RimAI.Core.Source.Modules.Persistence.Snapshots.ServerRecord>();
                    var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rec in list)
                    {
                        if (rec == null) continue;
                        foreach (var sl in (rec.InspectionSlots ?? new List<RimAI.Core.Source.Modules.Persistence.Snapshots.InspectionSlot>()))
                        {
                            if (sl == null || !sl.Enabled) continue;
                            var name = sl.ToolName;
                            if (!string.IsNullOrWhiteSpace(name)) set.Add(name);
                        }
                    }
                    var sorted = set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
                    return (System.Collections.Generic.IReadOnlyList<string>)sorted;
                }
                catch
                {
                    return (System.Collections.Generic.IReadOnlyList<string>)new List<string>();
                }
            }, name: "GetUniqueLoadedServerTools", ct: cts.Token);
        }
    }
}
