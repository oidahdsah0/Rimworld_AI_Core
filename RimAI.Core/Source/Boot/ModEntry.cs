using System;
using System.Diagnostics;
using HarmonyLib;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.World;
using Verse;

namespace RimAI.Core.Source.Boot
{
    public class RimAICoreMod : Mod
    {
        public static ServiceContainer Container { get; private set; } = new();

        public RimAICoreMod(ModContentPack content) : base(content)
        {
            var sw = Stopwatch.StartNew();
            try
            {
				// Register services (P1 + P2 + P3)
                Container.Register<IConfigurationService, ConfigurationService>();
                Container.Register<ILLMService, LLMService>();
                Container.Register<ISchedulerService, SchedulerService>();
				Container.Register<IWorldDataService, WorldDataService>();
				// P4 + P6 minimal services
				Container.Register<RimAI.Core.Source.Modules.Persistence.IPersistenceService, RimAI.Core.Source.Modules.Persistence.PersistenceService>();
				Container.Register<RimAI.Core.Source.Modules.Tooling.IToolRegistryService, RimAI.Core.Source.Modules.Tooling.ToolRegistryService>();
                // P5 Orchestration
                Container.Register<IOrchestrationService, OrchestrationService>();

                // Prewarm and fail fast
                Container.Init();

                // Harmony patches (UI button etc.)
                var harmony = new Harmony("kilokio.rimai.core");
                HarmonyPatcher.Apply(harmony);

                sw.Stop();
				// P2: resolve ILLMService to self-check readiness
                _ = Container.Resolve<ILLMService>();
				// P4: ensure tooling index attempt load (non-blocking)
				try { _ = Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>(); } catch { }
                Log.Message($"[RimAI.Core][P1][P2][P3][P4][P5] Boot OK (services={Container.GetKnownServiceCount()}, elapsed={sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Log.Error($"[RimAI.Core][P1] Boot FAILED after {sw.ElapsedMilliseconds} ms: {ex}");
                throw;
            }
        }
    }
}


