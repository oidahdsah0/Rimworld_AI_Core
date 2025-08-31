using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class SetForcedWeatherExecutor : IToolExecutor
    {
        public string Name => "set_forced_weather";

    // Parameters now centralized in WeatherControlConfig

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            try
            {
                var cfgSvc = RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService;
                var world = RimAICoreMod.Container.Resolve<IWorldDataService>();
                var action = RimAICoreMod.Container.Resolve<IWorldActionService>();
                var persistence = RimAICoreMod.Container.Resolve<IPersistenceService>();
                if (world == null || action == null) return new { ok = false, error = "world_services_unavailable" };

                // 防御性校验：该工具必须被任意服务器加载
                try
                {
                    var loaded = await world.GetUniqueLoadedServerToolsAsync(ct).ConfigureAwait(false);
                    bool found = false;
                    foreach (var name in loaded)
                    {
                        if (string.Equals(name, Name, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    }
                    if (!found) return new { ok = false, error = "ERROR: tool_not_loaded_by_any_server" };
                }
                catch { }

                // 巡检提示模式：不执行，仅返回冷却与提示
                bool inspection = false; try { if (args != null && args.TryGetValue("inspection", out var ins)) bool.TryParse(ins?.ToString() ?? "false", out inspection); } catch { }

                // Centralized config (cooldown moved to fixed 12 in-game hours)
                var minDays = WeatherControlConfig.MinDurationDays;
                var maxDays = WeatherControlConfig.MaxDurationDays;
                var allowed = WeatherControlConfig.AllowedWeathers ?? Array.Empty<string>();
                var fuzzy = WeatherControlConfig.FuzzyMinScore;

                // 先处理巡检模式：仅回报冷却/提示，不要求 weather_name
                if (inspection)
                {
                    var snapI = persistence?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
                    snapI.WeatherControl ??= new WeatherControlState();
                    long nowAbsI = await world.GetNowAbsTicksAsync(ct).ConfigureAwait(false);
                    // cooldownDays 目前用于执行路径；巡检仅回报剩余秒数，按快照计算
                    var remaining = (int)Math.Max(0, (snapI.WeatherControl.NextAllowedAtAbsTicks - nowAbsI) / 60);
                    return new { ok = true, inspection = true, cooldown_seconds = remaining, tip = "RimAI.Weather.InspectionHint" };
                }

                // Parse args (new): server_level injected by callers; weather_name required
                int serverLevel = 1;
                try
                {
                    if (args != null && args.TryGetValue("server_level", out var lvObj))
                    {
                        int.TryParse(lvObj?.ToString() ?? "1", NumberStyles.Integer, CultureInfo.InvariantCulture, out serverLevel);
                    }
                }
                catch { serverLevel = 1; }
                var weatherInput = args != null && args.TryGetValue("weather_name", out var wn) ? (wn?.ToString() ?? string.Empty) : string.Empty;
                if (string.IsNullOrWhiteSpace(weatherInput)) return new { ok = false, error = "missing_weather_name" };

                // Research gate (executor self-check): Communication + climate implied; we rely on communication for now
                var researchOk = await world.IsResearchFinishedAsync("RimAI_GW_Communication", ct).ConfigureAwait(false);
                if (!researchOk) return new { ok = false, error = "research_locked", require = new { research = "RimAI_GW_Communication" } };

                // Antenna powered check
                var antennaOk = await world.HasPoweredAntennaAsync(ct).ConfigureAwait(false);
                if (!antennaOk) return new { ok = false, error = "require_antenna_powered" };

                // Level check (lv3) using injected
                serverLevel = Math.Max(1, Math.Min(3, serverLevel));
                if (serverLevel < 3) return new { ok = false, error = "require_server_level3" };

                // Cooldown check (persistence per-map global)
                var snap = persistence?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
                snap.WeatherControl ??= new WeatherControlState();
                long nowAbs = await world.GetNowAbsTicksAsync(ct).ConfigureAwait(false);
                // 冷却：游戏内 12 小时（半天）= 30,000 ticks
                int cooldownTicks = 30000;

                if (snap.WeatherControl.NextAllowedAtAbsTicks > 0 && nowAbs < snap.WeatherControl.NextAllowedAtAbsTicks)
                {
                    var remaining = (int)((snap.WeatherControl.NextAllowedAtAbsTicks - nowAbs) / 60); // seconds
                    return new { ok = false, error = "cooldown_active", seconds_left = remaining };
                }

                // Fuzzy match weather
                var canon = allowed;
                var (best, score) = BestFuzzy(weatherInput, canon);
                if (best == null || score < fuzzy)
                {
                    return new { ok = false, error = "unsupported_weather", allowed = canon };
                }

                // Biome compatibility: rely on game filters; we let the condition apply, but report a soft warning is not done to stay simple

                // Randomize duration and schedule
                var rng = new Random(unchecked(serverLevel.GetHashCode() ^ nowAbs.GetHashCode()));
                int durDays = rng.Next(Math.Max(1, minDays), Math.Max(minDays, maxDays) + 1);
                int durationTicks = durDays * 60000;

                // Apply via world action (main-thread) and set auto-clear by natural condition expiry
                bool ok = await action.TryForceWeatherAsync(best, durationTicks, ct).ConfigureAwait(false);
                if (!ok) return new { ok = false, error = "apply_failed" };

                // Set cooldown and bookkeeping
                snap.WeatherControl.LastAppliedWeather = best;
                snap.WeatherControl.LastAppliedAtAbsTicks = (int)nowAbs;
                snap.WeatherControl.NextAllowedAtAbsTicks = (int)(nowAbs + cooldownTicks);
                snap.WeatherControl.ExpectedEndAtAbsTicks = (int)(nowAbs + durationTicks);
                persistence?.ReplaceLastSnapshotForDebug(snap);

                return new
                {
                    ok = true,
                    weather = best,
                    duration_days = durDays,
                    cooldown_hours = 12,
                    cooldown_days = 0.5
                };
            }
            catch (Exception ex)
            {
                return new { ok = false, error = ex.Message };
            }
        }

    // Legacy placeholder removed: config centralized in WeatherControlConfig

        private static (string best, double score) BestFuzzy(string input, IEnumerable<string> candidates)
        {
            if (string.IsNullOrWhiteSpace(input) || candidates == null) return (null, 0);
            input = input.Trim();
            string best = null; double bestScore = 0;
            foreach (var c in candidates)
            {
                var s = FuzzyScore(input, c);
                if (s > bestScore) { bestScore = s; best = c; }
            }
            return (best, bestScore);
        }

        // Very small fuzzy: normalized LCS length and case-insensitive startswith bonus
        private static double FuzzyScore(string a, string b)
        {
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return 1.0;
            var na = a.ToLowerInvariant();
            var nb = b.ToLowerInvariant();
            double lcs = Lcs(na, nb);
            double norm = lcs / Math.Max(1, Math.Max(na.Length, nb.Length));
            if (nb.StartsWith(na)) norm = Math.Max(norm, Math.Min(1.0, (double)na.Length / Math.Max(1, nb.Length)) + 0.2);
            if (na.StartsWith(nb)) norm = Math.Max(norm, Math.Min(1.0, (double)nb.Length / Math.Max(1, na.Length)) + 0.2);
            return Math.Min(1.0, norm);
        }

        private static int Lcs(string a, string b)
        {
            int n = a.Length, m = b.Length;
            var dp = new int[n + 1, m + 1];
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                    dp[i, j] = a[i - 1] == b[j - 1] ? dp[i - 1, j - 1] + 1 : Math.Max(dp[i - 1, j], dp[i, j - 1]);
            return dp[n, m];
        }
    }
}
