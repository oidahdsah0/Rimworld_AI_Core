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
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.Persona.Job;
using RimAI.Core.Source.Modules.Persona.Biography;
using RimAI.Core.Source.Modules.Persona.Ideology;
using RimAI.Core.Source.Modules.Persona.FixedPrompt;
using RimAI.Core.Source.Modules.Persona.Templates;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.History.Relations;
using RimAI.Core.Source.Modules.Stage;
using RimAI.Core.Source.Modules.Stage.Acts;
using RimAI.Core.Source.Modules.Stage.Diagnostics;
using RimAI.Core.Source.Modules.Stage.History;
using RimAI.Core.Source.Modules.Stage.Kernel;
using RimAI.Core.Source.Modules.Stage.Triggers;
using RimAI.Core.Source.Modules.Prompting;
// using RimAI.Core.Source.Modules.Prompting.Composers.ChatUI; // composers are instantiated inside PromptService
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.Modules.Server;
using Verse;

namespace RimAI.Core.Source.Boot
{
    public class RimAICoreMod : Mod
    {
        public static ServiceContainer Container { get; private set; } = new();
        public static string ModRootDir { get; private set; } = string.Empty;

        public RimAICoreMod(ModContentPack content) : base(content)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // Capture Mod root directory for runtime resource lookup (e.g., localization files)
                try { ModRootDir = content?.RootDir ?? string.Empty; } catch { ModRootDir = string.Empty; }
                // Register services (P1 + P2 + P3)
                Container.Register<IConfigurationService, ConfigurationService>();
                Container.Register<ILLMService, LLMService>();
                Container.Register<ISchedulerService, SchedulerService>();
                Container.Register<IWorldDataService, WorldDataService>();
                Container.Register<RimAI.Core.Source.Modules.World.IWorldActionService, RimAI.Core.Source.Modules.World.WorldActionService>();
                // P4 + P6 minimal services
                Container.Register<RimAI.Core.Source.Modules.Persistence.IPersistenceService, RimAI.Core.Source.Modules.Persistence.PersistenceService>();
                Container.Register<RimAI.Core.Source.Modules.Tooling.IToolRegistryService, RimAI.Core.Source.Modules.Tooling.ToolRegistryService>();
                // P5 Orchestration
                Container.Register<IOrchestrationService, OrchestrationService>();

                // Register P7 Persona services
                Container.Register<IPersonaService, PersonaService>();
                Container.Register<IPersonaJobService, PersonaJobService>();
                Container.Register<IBiographyService, BiographyService>();
                Container.Register<IIdeologyService, IdeologyService>();
                Container.Register<IFixedPromptService, FixedPromptService>();
                Container.Register<IPersonaTemplateManager, PersonaTemplateManager>();
                Container.Register<IPersonaAutoSettingsService, PersonaAutoSettingsService>();
                // 自动生成后台任务（P7）：每 15 天为殖民者尝试生成传记与世界观（顺序执行，1分钟间隔）
                try { Container.RegisterInstance(new PersonaAutoGenerator(Container.Resolve<RimAI.Core.Source.Infrastructure.Scheduler.ISchedulerService>(), Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>(), Container.Resolve<RimAI.Core.Source.Modules.Persona.Biography.IBiographyService>(), Container.Resolve<RimAI.Core.Source.Modules.Persona.Ideology.IIdeologyService>(), Container.Resolve<IPersonaAutoSettingsService>())); } catch { }

                // Register P8 History services (Recap depends on History, so register History first, but Recap requires history in ctor and history no longer depends on recap)
                Container.Register<IHistoryService, HistoryService>();
                Container.Register<IRecapService, RecapService>();
                Container.Register<IRelationsService, RelationsService>();

                // Register P9 Stage services
                Container.Register<IStageKernel, StageKernel>();
                Container.Register<StageLogging, StageLogging>();
                Container.Register<StageHistorySink, StageHistorySink>();
                Container.Register<IStageService, StageService>();

                // Register Prompting (P11)
                Container.Register<IPromptService, PromptService>();
                Container.Register<ILocalizationService, LocalizationService>();

                // Register P13 Server services
                Container.Register<IServerService, ServerService>();
                Container.Register<IServerPromptPresetManager, ServerPromptPresetManager>();

                // Register built-in Acts/Triggers via StageService after construction

                // Prewarm and fail fast
                Container.Init();

                // After Init, resolve StageService and register built-ins
                try
                {
                    var stage = Container.Resolve<IStageService>() as StageService;
                    if (stage != null)
                    {
                        stage.RegisterAct(new RimAI.Core.Source.Modules.Stage.Acts.GroupChatAct(
                            Container.Resolve<ILLMService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.World.IWorldActionService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>(),
                            Container.Resolve<IConfigurationService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>(),
                            Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>()
                        ));
                        stage.RegisterAct(new RimAI.Core.Source.Modules.Stage.Acts.InterServerGroupChatAct(
                            Container.Resolve<ILLMService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>(),
                            Container.Resolve<IWorldDataService>(),
                            Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>()
                        ));
                        stage.RegisterTrigger(new ProximityGroupChatTrigger());
                        // 移除 AlphaFiberLinkTrigger，改用新的服务器定时触发器
                        stage.RegisterTrigger(new TimedInterServerChatTrigger(Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>()));
                        stage.RegisterTrigger(new TimedGroupChatTrigger(Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>()));
                        stage.RegisterTrigger(new ManualGroupChatTrigger(Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>()));
                    }
                }
                catch { }

                // P13: 启动/进入地图后，自动发现通电服务器并注册周期任务（后台）
                try
                {
                    var world = Container.Resolve<IWorldDataService>();
                    var server = Container.Resolve<IServerService>();
                    var scheduler = Container.Resolve<ISchedulerService>();
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            var ids = await world.GetPoweredAiServerThingIdsAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
                            foreach (var id in ids)
                            {
                                var entityId = $"thing:{id}";
                                int level = 1;
                                try { level = await world.GetAiServerLevelAsync(id).ConfigureAwait(false); } catch { level = 1; }
                                server.GetOrCreate(entityId, level);
                            }
                            server.StartAllSchedulers(System.Threading.CancellationToken.None);
                            Verse.Log.Message($"[RimAI.Core][P13] discovered_servers={ids?.Count ?? 0}; periodic_registered=true");
                        }
                        catch (System.Exception ex)
                        {
                            Verse.Log.Error($"[RimAI.Core][P13] discover/start schedulers failed: {ex.Message}");
                        }
                    });
                }
                catch { }

                // Harmony patches (UI button etc.)
                var harmony = new Harmony("kilokio.rimai.core");
                HarmonyPatcher.Apply(harmony);

                sw.Stop();
                // P2: resolve ILLMService to self-check readiness
                _ = Container.Resolve<ILLMService>();
                // P4: ensure tooling index attempt load (non-blocking)
                try { _ = Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>(); } catch { }
                Log.Message($"[RimAI.Core][P1][P2][P3][P4][P5][P7][P8][P9] Boot OK (services={Container.GetKnownServiceCount()}, elapsed={sw.ElapsedMilliseconds} ms)");
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


