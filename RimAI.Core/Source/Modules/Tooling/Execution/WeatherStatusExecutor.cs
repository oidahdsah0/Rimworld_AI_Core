using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class WeatherStatusExecutor : IToolExecutor
    {
        public string Name => "get_weather_status";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetWeatherAnalysisAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                object res = new
                {
                    ok = true,
                    time = new { hour = s?.Time?.HourOfDay ?? 0, season = s?.Time?.Season, quadrum = s?.Time?.Quadrum, day = s?.Time?.DayOfQuadrum ?? 0 },
                    weather = new { def = s?.Weather?.DefName, label = s?.Weather?.Label, rain = s?.Weather?.RainRate ?? 0f, snow = s?.Weather?.SnowRate ?? 0f, wind = s?.Weather?.WindSpeed ?? 0f },
                    temp = new { now = s?.Temp?.OutdoorNowC ?? 0f, seasonal = s?.Temp?.SeasonalNowC ?? 0f, next = s?.Temp?.NextHoursC ?? new float[0], min = s?.Temp?.MinNextC ?? 0f, max = s?.Temp?.MaxNextC ?? 0f, trend = s?.Temp?.Trend },
                    conditions = s?.ActiveConditions ?? new string[0],
                    growth = s?.GrowthSeasonNow ?? false,
                    enjoyableOutside = s?.EnjoyableOutside ?? false,
                    advisories = s?.Advisories ?? new string[0]
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
