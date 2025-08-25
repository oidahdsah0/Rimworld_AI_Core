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
    /// <summary>
    /// WeatherStatusPart v1
    /// - 主线程读取当前地图的天气/风/降水/时间与游戏条件
    /// - 使用 TileTemperaturesComp 进行短期（默认 6 小时）逐小时室外温度采样，构建趋势
    /// - 不尝试精准预测“下一次天气开始时间”，避免依赖内部时序细节
    /// </summary>
    internal sealed class WeatherStatusPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public WeatherStatusPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<WeatherAnalysisSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap ?? Find.Maps?.FirstOrDefault();
                if (map == null) throw new WorldDataException("No map available");

                // 时间信息（使用 GenLocalDate 基于地图经纬度）
                int hour = 0; string season = string.Empty; string quadrum = string.Empty; int dayOfQuadrum = 0;
                try
                {
                    hour = GenLocalDate.HourOfDay(map);
                    season = GenLocalDate.Season(map).LabelCap();
                    var ll = Find.WorldGrid.LongLatOf(map.Tile);
                    var q = GenDate.Quadrum(Find.TickManager.TicksAbs, ll.x);
                    quadrum = q.LabelShort();
                    dayOfQuadrum = GenLocalDate.DayOfQuadrum(map);
                }
                catch { }

                // 当前天气/风/降水
                string defName = string.Empty; string label = string.Empty; float rain = 0f; float snow = 0f; float wind = 0f;
                try
                {
                    var wm = map.weatherManager;
                    defName = wm?.curWeather?.defName ?? string.Empty;
                    label = wm?.curWeather?.label ?? defName ?? string.Empty;
                    rain = wm?.RainRate ?? 0f;
                    snow = wm?.SnowRate ?? 0f;
                }
                catch { }
                try { wind = map.windManager?.WindSpeed ?? 0f; } catch { }

                // 温度：当前+季节
                float outdoorNow = 0f; float seasonalNow = 0f;
                try { outdoorNow = map.mapTemperature?.OutdoorTemp ?? 0f; } catch { }
                try { seasonalNow = map.mapTemperature?.SeasonalTemp ?? 0f; } catch { }

                // 短期趋势：未来 6 小时的每小时室外温度（使用 TileTemperaturesComp.OutdoorTemperatureAt）
                var temps = new List<float>();
                try
                {
                    var tile = map.Tile;
                    int nowAbs = Find.TickManager.TicksAbs;
                    for (int i = 1; i <= 6; i++)
                    {
                        int abs = nowAbs + i * 2500; // 2500 tick ≈ 1 游戏小时
                        float t = Find.World.tileTemperatures.OutdoorTemperatureAt(tile, abs);
                        temps.Add(t);
                    }
                }
                catch { }
                float minNext = temps.Count > 0 ? temps.Min() : outdoorNow;
                float maxNext = temps.Count > 0 ? temps.Max() : outdoorNow;
                string trend = "steady";
                if (temps.Count >= 2)
                {
                    var first = temps.First();
                    var last = temps.Last();
                    if (last - first > 1.0f) trend = "rising"; else if (first - last > 1.0f) trend = "falling"; // 1°C 阈值
                }

                // 条件/可户外/生长季
                var conditionLabels = Array.Empty<string>(); bool enjoyable = false; bool growth = false;
                try
                {
                    var conds = map.gameConditionManager?.ActiveConditions ?? new List<GameCondition>();
                    conditionLabels = conds.Select(c => c?.LabelCap ?? c?.def?.label ?? c?.def?.defName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                }
                catch { }
                try { enjoyable = JoyUtility.EnjoyableOutsideNow(map); } catch { enjoyable = false; }
                try { growth = PlantUtility.GrowthSeasonNow(map, ThingDefOf.Plant_Potato); } catch { growth = false; }

                var timeInfo = new TimeInfo { HourOfDay = hour, Season = season, Quadrum = quadrum, DayOfQuadrum = dayOfQuadrum };
                var weatherNow = new WeatherNow { DefName = defName, Label = label, RainRate = rain, SnowRate = snow, WindSpeed = wind };
                var trendInfo = new TemperatureTrend { OutdoorNowC = outdoorNow, SeasonalNowC = seasonalNow, NextHoursC = temps, MinNextC = minNext, MaxNextC = maxNext, Trend = trend };

                // 简单建议：基于温度与降水/风
                var adv = new List<string>();
                try
                {
                    if (outdoorNow < 0 || minNext < 0) adv.Add("Prepare warm clothing: subzero temperatures expected.");
                    if (outdoorNow > 30 || maxNext > 35) adv.Add("Heat risk: ensure cooling and hydration.");
                    if ((rain > 0.1f) || (snow > 0.1f)) adv.Add("Precipitation ongoing: plan indoor work.");
                    if (wind > 1.2f) adv.Add("Strong wind: wind turbines output high; watch for fire spread.");
                    if (!growth) adv.Add("Not in growth season: outdoor crops won't grow.");
                }
                catch { }

                return new WeatherAnalysisSnapshot
                {
                    Time = timeInfo,
                    Weather = weatherNow,
                    Temp = trendInfo,
                    ActiveConditions = conditionLabels,
                    GrowthSeasonNow = growth,
                    EnjoyableOutside = enjoyable,
                    Advisories = adv.ToArray()
                };
            }, name: "WeatherStatusPart.Get", ct: cts.Token);
        }
    }
}
