using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class MiscActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public MiscActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task ShowTopLeftMessageAsync(string text, Verse.MessageTypeDef type, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try { Messages.Message(text, type ?? MessageTypeDefOf.NeutralEvent); } catch { }
                return Task.CompletedTask;
            }, name: "World.ShowTopLeftMessage", ct: cts.Token);
        }

        public Task<bool> TryForceWeatherAsync(string weatherDefName, int durationTicks, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(weatherDefName) || durationTicks <= 0) return Task.FromResult(false);
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Mathf.Max(timeoutMs, 3000));
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (Current.Game == null) return false;
                    var map = Find.CurrentMap ?? Find.Maps?.FirstOrDefault();
                    if (map == null) return false;
                    var def = DefDatabase<WeatherDef>.GetNamedSilentFail(weatherDefName);
                    if (def == null) return false;
                    var gcDef = RimWorld.GameConditionDefOf.WeatherController ?? DefDatabase<GameConditionDef>.GetNamedSilentFail("WeatherController");
                    if (gcDef == null) return false;
                    var cond = RimWorld.GameConditionMaker.MakeCondition(gcDef, durationTicks);
                    if (cond is RimWorld.GameCondition_ForceWeather fw) { fw.weather = def; }
                    map.gameConditionManager.RegisterCondition(cond);

                    // Start notice: show top-left message immediately (localized)
                    try
                    {
                        var label = string.IsNullOrWhiteSpace(def.label) ? def.defName : def.label;
                        var days = Mathf.Max(1, Mathf.RoundToInt(durationTicks / 60000f));
                        var startMsg = "RimAI.Core.World.Weather.Force.Start".Translate(label, days);
                        Messages.Message(startMsg, MessageTypeDefOf.PositiveEvent);
                    }
                    catch { }

                    // End notice: schedule a one-off message around expected expiry (game ticks based)
                    try
                    {
                        int startedTicks = 0;
                        try { startedTicks = Find.TickManager?.TicksGame ?? 0; } catch { startedTicks = 0; }
                        var label = string.IsNullOrWhiteSpace(def.label) ? def.defName : def.label;
                        IDisposable periodic = null;
                        const int interval = 250; // check ~every 4 seconds of game time
                        periodic = _scheduler.SchedulePeriodic(
                            $"World.ForceWeatherEndNotice.{startedTicks}",
                            interval,
                            async token =>
                            {
                                try
                                {
                                    int now = 0;
                                    try { now = Find.TickManager?.TicksGame ?? 0; } catch { now = 0; }
                                    if (now > 0 && startedTicks > 0 && (now - startedTicks) >= Mathf.Max(1, durationTicks - interval))
                                    {
                                        try
                                        {
                                            var endMsg = "RimAI.Core.World.Weather.Force.End".Translate(label);
                                            Messages.Message(endMsg, MessageTypeDefOf.NeutralEvent);
                                        }
                                        catch { }
                                        try { periodic?.Dispose(); } catch { }
                                    }
                                }
                                catch { }
                                await Task.CompletedTask;
                            },
                            CancellationToken.None);
                    }
                    catch { }
                    return true;
                }
                catch { return false; }
            }, name: "World.TryForceWeather", ct: cts.Token);
        }
    }
}
