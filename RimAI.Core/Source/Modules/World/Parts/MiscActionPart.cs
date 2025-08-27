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
                    return true;
                }
                catch { return false; }
            }, name: "World.TryForceWeather", ct: cts.Token);
        }
    }
}
