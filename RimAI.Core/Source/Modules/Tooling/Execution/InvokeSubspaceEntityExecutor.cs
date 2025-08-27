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
    internal sealed class InvokeSubspaceEntityExecutor : IToolExecutor
    {
        public string Name => "invoke_subspace_entity";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            try
            {
                var cfgSvc = RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService;
                var world = RimAICoreMod.Container.Resolve<IWorldDataService>();
                var action = RimAICoreMod.Container.Resolve<IWorldActionService>();
                var persistence = RimAICoreMod.Container.Resolve<IPersistenceService>();
                if (world == null || action == null) return new { ok = false, error = "world_services_unavailable" };

                // Parse args
                var serverId = args != null && args.TryGetValue("server_id", out var sv) ? (sv?.ToString() ?? string.Empty) : string.Empty;
                int score = 0;
                if (args != null && args.TryGetValue("llm_score", out var sc)) int.TryParse(sc?.ToString() ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture, out score);
                if (string.IsNullOrWhiteSpace(serverId)) return new { ok = false, error = "missing_server_id" };
                score = Math.Max(0, Math.Min(100, score));

                // Parity with weather-controller validations, except research:
                // 1) Research gate (new): RimAI_Subspace_Gravitic_Penetration
                var researchOk = await world.IsResearchFinishedAsync("RimAI_Subspace_Gravitic_Penetration", ct).ConfigureAwait(false);
                if (!researchOk) return new { ok = false, error = "research_locked", require = new { research = "RimAI_Subspace_Gravitic_Penetration" } };

                // 2) Antenna powered check (reuse)
                var antennaOk = await world.HasPoweredAntennaAsync(ct).ConfigureAwait(false);
                if (!antennaOk) return new { ok = false, error = "require_antenna_powered" };

                // 3) Server level check (Lv3)
                int lv = 1;
                try
                {
                    var idPart = serverId.Split(':').LastOrDefault();
                    if (int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var thingId))
                    {
                        lv = await world.GetAiServerLevelAsync(thingId, ct).ConfigureAwait(false);
                    }
                }
                catch { lv = 1; }
                if (lv < 3) return new { ok = false, error = "require_server_level3" };

                // 4) Cooldown check
                var snap = persistence?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
                snap.SubspaceInvocation ??= new SubspaceInvocationState();
                long nowAbs = await world.GetNowAbsTicksAsync(ct).ConfigureAwait(false);
                // Cooldown: reuse weather controller’s default 2 in-game days unless we have a dedicated knob later
                int cooldownTicks = 2 * 60000; // 2 days
                if (snap.SubspaceInvocation.NextAllowedAtAbsTicks > 0 && nowAbs < snap.SubspaceInvocation.NextAllowedAtAbsTicks)
                {
                    var remaining = (int)((snap.SubspaceInvocation.NextAllowedAtAbsTicks - nowAbs) / 60); // seconds
                    return new { ok = false, error = "cooldown_active", seconds_left = remaining };
                }

                // Trigger via WAS (main-thread). Backfire chance grows with score but small constant for now.
                var outcome = await action.TryInvokeSubspaceAsync(score, ct).ConfigureAwait(false);
                if (outcome == null) return new { ok = false, error = "apply_failed" };

                // Persistence bookkeeping
                snap.SubspaceInvocation.LastInvokedAtAbsTicks = (int)nowAbs;
                snap.SubspaceInvocation.NextAllowedAtAbsTicks = (int)(nowAbs + cooldownTicks);
                snap.SubspaceInvocation.LastTier = outcome.Tier;
                snap.SubspaceInvocation.LastComposition = outcome.Composition;
                snap.SubspaceInvocation.LastCount = outcome.Count;
                persistence?.ReplaceLastSnapshotForDebug(snap);

                // Inspection-style return (similar to weather)
                return new
                {
                    ok = true,
                    tier = outcome.Tier,
                    composition = outcome.Composition,
                    count = outcome.Count,
                    cooldown_days = 2
                };
            }
            catch (Exception ex)
            {
                return new { ok = false, error = ex.Message };
            }
        }
    }
}
