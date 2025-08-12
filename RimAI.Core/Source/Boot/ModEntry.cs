using System;
using System.Diagnostics;
using HarmonyLib;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Infrastructure.Configuration;
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
                // Register P1 services only
                Container.Register<IConfigurationService, ConfigurationService>();

                // Prewarm and fail fast
                Container.Init();

                // Harmony patches (UI button etc.)
                var harmony = new Harmony("kilokio.rimai.core");
                HarmonyPatcher.Apply(harmony);

                sw.Stop();
                Log.Message($"[RimAI.Core][P1] Boot OK (services={Container.GetKnownServiceCount()}, elapsed={sw.ElapsedMilliseconds} ms)");
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


