using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class ColonyPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public ColonyPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<string> GetPlayerNameAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var name = Faction.OfPlayer?.Name ?? "Player";
                return name;
            }, name: "GetPlayerName", ct: cts.Token);
        }

        public Task<int> GetCurrentDayNumberAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var abs = Find.TickManager?.TicksAbs ?? 0;
                int days = abs / 60000; // 60k tick/day
                return days;
            }, name: "GetCurrentDayNumber", ct: cts.Token);
        }

        public Task<ColonySnapshot> GetColonySnapshotAsync(int? pawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var faction = Faction.OfPlayer;
                var map = Find.CurrentMap;
                string colonyName = faction?.Name ?? map?.Parent?.Label ?? "Colony";
                var names = new List<string>();
                var records = new List<ColonistRecord>();
                int count = 0;
                foreach (var m in Find.Maps)
                {
                    var pawns = m?.mapPawns?.PawnsInFaction(faction);
                    if (pawns == null) continue;
                    foreach (var p in pawns)
                    {
                        if (p == null || p.RaceProps == null || p.RaceProps.Humanlike == false) continue;
                        if (p.HostFaction != null) continue; // exclude prisoners/guests of other factions
                        var dispName = p.Name?.ToStringShort ?? p.LabelCap ?? "Pawn";
                        names.Add(dispName);
                        var age = p.ageTracker != null ? (int)UnityEngine.Mathf.Floor(p.ageTracker.AgeBiologicalYearsFloat) : 0;
                        var gender = p.gender.ToString();
                        string job = null;
                        try { job = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetPawnTitle(p); } catch { }
                        records.Add(new ColonistRecord { Name = dispName, Age = age, Gender = gender, JobTitle = job ?? string.Empty });
                        count++;
                    }
                }
                return new ColonySnapshot { ColonyName = colonyName, ColonistCount = count, ColonistNames = names, Colonists = records };
            }, name: "GetColonySnapshot", ct: cts.Token);
        }

        public Task<IReadOnlyList<int>> GetAllColonistLoadIdsAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var list = new List<int>();
                try
                {
                    foreach (var map in Find.Maps)
                    {
                        var pawns = map?.mapPawns?.FreeColonists; if (pawns == null) continue;
                        foreach (var p in pawns)
                        {
                            if (p != null && !p.Dead) list.Add(p.thingIDNumber);
                        }
                    }
                }
                catch { }
                return (IReadOnlyList<int>)list;
            }, name: "GetAllColonistLoadIds", ct: cts.Token);
        }
    }
}
