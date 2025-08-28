using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class AiDiplomatExecutor : IToolExecutor
    {
        public string Name => "ai_diplomat";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            try
            {
                var cfgSvc = RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService;
                var world = RimAICoreMod.Container.Resolve<IWorldDataService>();
                var action = RimAICoreMod.Container.Resolve<IWorldActionService>();
                if (world == null || action == null) return new { ok = false, error = "world_services_unavailable" };

                // Parse args: server_level injected by callers (pawn=1, server=its level)
                int serverLevel = 1;
                try
                {
                    if (args != null && args.TryGetValue("server_level", out var lvObj))
                    {
                        int.TryParse(lvObj?.ToString() ?? "1", NumberStyles.Integer, CultureInfo.InvariantCulture, out serverLevel);
                    }
                }
                catch { serverLevel = 1; }

                // Research gate self-check (defensive; list gating also applies earlier)
                var researchOk = await world.IsResearchFinishedAsync("RimAI_AI_Level2", ct).ConfigureAwait(false);
                if (!researchOk) return new { ok = false, error = "research_locked", require = new { research = "RimAI_AI_Level2" } };

                // Level check (>=2), using injected server_level
                serverLevel = Math.Max(1, Math.Min(3, serverLevel));
                if (serverLevel < 2) return new { ok = false, error = "require_server_level2" };

                // Powered AI terminal required
                var terminalPowered = await world.HasPoweredBuildingAsync("RimAI_AITerminalA", ct).ConfigureAwait(false);
                if (!terminalPowered)
                {
                    return new { ok = false, error = "terminal_absent_or_unpowered" };
                }

                // Eligible factions snapshot via world service parts
                var factions = await world.GetEligibleFactionLoadIdsAsync(ct).ConfigureAwait(false);
                if (factions == null || factions.Count == 0)
                {
                    return new { ok = false, error = "no_eligible_faction" };
                }

                // Pick one randomly; delta in [-5, +15]
                int seed = unchecked((serverLevel.GetHashCode()) ^ (int)(DateTime.UtcNow.Ticks & 0xFFFFFFFF));
                var rng = new Random(seed);
                var pick = factions[rng.Next(factions.Count)];
                int delta = rng.Next(-5, 16);

                // Apply goodwill via WAS (main-thread)
                var result = await action.TryAdjustFactionGoodwillAsync(pick, delta, "ai_diplomat", ct).ConfigureAwait(false);
                if (result == null)
                {
                    return new { ok = false, error = "apply_failed" };
                }

                return new
                {
                    ok = true,
                    faction = new { id = result.FactionId, name = result.FactionName, defName = result.FactionDefName },
                    goodwill_before = result.Before,
                    delta,
                    goodwill_after = result.After,
                    note = "goodwill_adjusted"
                };
            }
            catch (Exception ex)
            {
                try { Verse.Log.Error($"[RimAI.Core][P13] ai_diplomat failed: {ex}"); } catch { }
                return new { ok = false, error = "exception" };
            }
        }

        
    }
}


