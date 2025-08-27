using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class GroupChatActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        private sealed class SessionState
        {
            public string Id;
            public int InitiatorLoadId;
            public List<int> ParticipantLoadIds;
            public int Radius;
            public TimeSpan MaxDuration;
            public DateTime StartedUtc;
            public int StartedTicks;
            public bool Aborted;
            public bool Completed;
            public IDisposable Periodic;
        }

        private readonly ConcurrentDictionary<string, SessionState> _sessions = new ConcurrentDictionary<string, SessionState>();

        public GroupChatActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler; _cfg = cfg;
        }

        public Task ShowSpeechTextAsync(int pawnLoadId, string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    Pawn pawn = null;
                    foreach (var map in Find.Maps)
                    {
                        foreach (var p in map.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                        { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } }
                        if (pawn != null) break;
                    }
                    if (pawn == null) return Task.CompletedTask;
                    var mapRef = pawn.Map; if (mapRef == null) return Task.CompletedTask;
                    Color color = Color.white;
                    try { color = Color.white; } catch { }
                    try { MoteMaker.ThrowText(pawn.DrawPos, mapRef, text, color, 2f); } catch { }
                    return Task.CompletedTask;
                }
                catch { return Task.CompletedTask; }
            }, name: "World.ShowSpeechText", ct: cts.Token);
        }

        public Task ShowThingSpeechTextAsync(int thingLoadId, string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    Thing target = null; Map mapRef = null;
                    foreach (var map in Find.Maps)
                    {
                        foreach (var t in map.listerThings?.AllThings ?? Enumerable.Empty<Thing>())
                        {
                            if (t?.thingIDNumber == thingLoadId) { target = t; mapRef = map; break; }
                        }
                        if (target != null) break;
                    }
                    if (target == null || mapRef == null) return Task.CompletedTask;
                    Color color = Color.white;
                    try { color = Color.white; } catch { }
                    try { MoteMaker.ThrowText(target.DrawPos, mapRef, text, color, 2f); } catch { }
                    return Task.CompletedTask;
                }
                catch { return Task.CompletedTask; }
            }, name: "World.ShowThingSpeechText", ct: cts.Token);
        }

        public Task<GroupChatSessionHandle> StartAsync(int initiatorPawnLoadId, IReadOnlyList<int> participantLoadIds, int radius, TimeSpan maxDuration, CancellationToken ct = default)
        {
            if (participantLoadIds == null || participantLoadIds.Count == 0) return Task.FromResult<GroupChatSessionHandle>(null);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            const int guardEveryTicks = 120;
            var handle = new GroupChatSessionHandle { Id = Guid.NewGuid().ToString("N") };
            var state = new SessionState
            {
                Id = handle.Id,
                InitiatorLoadId = initiatorPawnLoadId,
                ParticipantLoadIds = participantLoadIds.Where(x => x != initiatorPawnLoadId).Distinct().ToList(),
                Radius = Mathf.Max(1, radius),
                MaxDuration = maxDuration,
                StartedUtc = DateTime.UtcNow,
                StartedTicks = 0,
                Aborted = false,
                Completed = false,
                Periodic = null
            };
            _sessions[state.Id] = state;

            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            cts.CancelAfter(Math.Max(timeoutMs, 3000));
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (Current.Game == null) return (GroupChatSessionHandle)null;
                    try { state.StartedTicks = Find.TickManager?.TicksGame ?? 0; } catch { state.StartedTicks = 0; }
                    Pawn initiator = null; Map map = null;
                    foreach (var m in Find.Maps)
                    {
                        foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                        { if (p?.thingIDNumber == initiatorPawnLoadId) { initiator = p; map = m; break; } }
                        if (initiator != null) break;
                    }
                    if (initiator == null || map == null) return (GroupChatSessionHandle)null;

                    foreach (var pid in state.ParticipantLoadIds)
                    {
                        try
                        {
                            Pawn pawn = null;
                            foreach (var m in Find.Maps)
                            {
                                foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                                { if (p?.thingIDNumber == pid) { pawn = p; break; } }
                                if (pawn != null) break;
                            }
                            if (pawn == null) continue;
                            var dest = CellFinder.RandomClosewalkCellNear(initiator.Position, initiator.Map, state.Radius);
                            try
                            {
                                try { if (pawn.drafter != null) pawn.drafter.Drafted = false; } catch { }
                                pawn.jobs?.StartJob(new Job(JobDefOf.Goto, dest), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
                                pawn.jobs?.jobQueue?.EnqueueLast(new Job(JobDefOf.Wait));
                            }
                            catch { }
                        }
                        catch { }
                    }

                    state.Periodic = _scheduler.SchedulePeriodic("World.GroupChatGuard." + state.Id, guardEveryTicks, async token =>
                    {
                        bool abort = false;
                        try
                        {
                            // 实时硬限制：3 个游戏内小时（3 * 2500 ticks）
                            int gameTicks = 0;
                            try { gameTicks = Find.TickManager?.TicksGame ?? 0; } catch { gameTicks = 0; }
                            const int ThreeHoursTicks = 3 * 2500;
                            if (state.StartedTicks > 0 && gameTicks > 0 && gameTicks - state.StartedTicks >= ThreeHoursTicks)
                                abort = true;

                            // 继续保留墙钟时间的上限（配置）
                            if (!abort && (DateTime.UtcNow - state.StartedUtc) > state.MaxDuration) abort = true;
                            var ids = new List<int>();
                            ids.Add(state.InitiatorLoadId);
                            ids.AddRange(state.ParticipantLoadIds);
                            foreach (var id in ids)
                            {
                                Pawn pawn = null;
                                foreach (var m in Find.Maps)
                                {
                                    foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                                    { if (p?.thingIDNumber == id) { pawn = p; break; } }
                                    if (pawn != null) break;
                                }
                                if (pawn == null) { abort = true; break; }
                                bool drafted = false, downed = false, mental = false, hunger = false, rest = false;
                                try { drafted = pawn.Drafted; } catch { }
                                try { downed = pawn.Downed; } catch { }
                                try { mental = pawn.mindState?.mentalStateHandler?.InMentalState ?? false; } catch { }
                                try { var cat = pawn.needs?.food?.CurCategory; hunger = (cat == HungerCategory.UrgentlyHungry || cat == HungerCategory.Starving); } catch { }
                                try { var rc = pawn.needs?.rest?.CurCategory; rest = (rc == RestCategory.VeryTired || rc == RestCategory.Exhausted); } catch { }
                                if (drafted || downed || mental || hunger || rest) { abort = true; break; }
                                try
                                {
                                    var ms = pawn.mindState;
                                    if (ms != null)
                                    {
                                        var tp = ms.GetType();
                                        var fields = tp.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                        int ticksNow = Find.TickManager.TicksGame;
                                        foreach (var f in fields)
                                        {
                                            if (f.FieldType == typeof(int))
                                            {
                                                var name = f.Name?.ToLowerInvariant() ?? string.Empty;
                                                if (name.Contains("damag") || name.Contains("harm"))
                                                {
                                                    try
                                                    {
                                                        int v = (int)f.GetValue(ms);
                                                        if (v > 0 && ticksNow - v <= 120) { abort = true; break; }
                                                    }
                                                    catch { }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }
                                if (abort) break;
                            }

                            if (!abort)
                            {
                                foreach (var pid in state.ParticipantLoadIds)
                                {
                                    Pawn pawn = null;
                                    foreach (var m in Find.Maps)
                                    {
                                        foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                                        { if (p?.thingIDNumber == pid) { pawn = p; break; } }
                                        if (pawn != null) break;
                                    }
                                    if (pawn == null) continue;
                                    try
                                    {
                                        if (pawn.CurJobDef != JobDefOf.Wait && pawn.CurJobDef != JobDefOf.Goto)
                                        {
                                            var dest = CellFinder.RandomClosewalkCellNear(initiator.Position, initiator.Map, state.Radius);
                                            pawn.jobs?.StartJob(new Job(JobDefOf.Goto, dest), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
                                            pawn.jobs?.jobQueue?.EnqueueLast(new Job(JobDefOf.Wait));
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        catch { abort = true; }
                        if (abort)
                        {
                            try { await EndAsync(handle, "Aborted", CancellationToken.None).ConfigureAwait(false); } catch { }
                        }
                    }, CancellationToken.None);

                    return handle;
                }
                catch { return (GroupChatSessionHandle)null; }
            }, name: "World.StartGroupChatDuty", ct: cts.Token);
        }

        public Task<bool> EndAsync(GroupChatSessionHandle handle, string reason, CancellationToken ct = default)
        {
            if (handle == null || string.IsNullOrWhiteSpace(handle.Id)) return Task.FromResult(false);
            if (!_sessions.TryRemove(handle.Id, out var state)) return Task.FromResult(false);
            state.Aborted = string.Equals(reason, "Aborted", StringComparison.OrdinalIgnoreCase);
            state.Completed = !state.Aborted;
            try { state.Periodic?.Dispose(); } catch { }
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Math.Max(timeoutMs, 2000));
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    var ids = new List<int>();
                    ids.Add(state.InitiatorLoadId);
                    ids.AddRange(state.ParticipantLoadIds);
                    foreach (var id in ids)
                    {
                        try
                        {
                            Pawn pawn = null;
                            foreach (var m in Find.Maps)
                            {
                                foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                                { if (p?.thingIDNumber == id) { pawn = p; break; } }
                                if (pawn != null) break;
                            }
                            if (pawn == null) continue;
                            try
                            {
                                // 强制打断当前 Job，且不启动队列中的下一个；然后清空队列，确保 Goto/Wait 均被清除
                                pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
                                pawn.jobs?.ClearQueuedJobs();
                            }
                            catch { }
                        }
                        catch { }
                    }
                    return true;
                }
                catch { return true; }
            }, name: "World.EndGroupChatDuty", ct: cts.Token);
        }

        public bool IsAlive(GroupChatSessionHandle handle)
        {
            if (handle == null || string.IsNullOrWhiteSpace(handle.Id)) return false;
            if (!_sessions.TryGetValue(handle.Id, out var state)) return false;
            if (state == null) return false;
            if (state.Aborted) return false;
            // 硬性：超过 3 个游戏内小时必定结束
            try
            {
                int ticksNow = Find.TickManager?.TicksGame ?? 0;
                const int ThreeHoursTicks = 3 * 2500;
                if (state.StartedTicks > 0 && ticksNow > 0 && ticksNow - state.StartedTicks >= ThreeHoursTicks) return false;
            }
            catch { }
            // 同时保留墙钟时间的上限
            if ((DateTime.UtcNow - state.StartedUtc) > state.MaxDuration) return false;
            return true;
        }
    }
}
