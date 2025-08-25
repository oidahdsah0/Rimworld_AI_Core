using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class WeatherPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public WeatherPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<WeatherStatus> GetWeatherStatusAsync(int pawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var mapRef = pawn.Map ?? Find.CurrentMap;
                string weather = string.Empty; try { weather = mapRef?.weatherManager?.curWeather?.label ?? mapRef?.weatherManager?.curWeather?.defName ?? string.Empty; } catch { }
                float temp = 0f; try { temp = pawn.AmbientTemperature; } catch { }
                float glow = 0f; try { var pg = mapRef?.glowGrid?.PsychGlowAt(pawn.Position) ?? PsychGlow.Lit; glow = pg == PsychGlow.Dark ? 0f : (pg == PsychGlow.Lit ? 1f : 0.5f); } catch { }
                return new WeatherStatus { Weather = weather, OutdoorTempC = temp, Glow = glow };
            }, name: "GetWeatherStatus", ct: cts.Token);
        }
    }
}
