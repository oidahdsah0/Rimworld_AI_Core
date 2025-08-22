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
using RimAI.Core.Source.UI.Settings;
using RimAI.Core.Source.UI.Settings.Sections;

namespace RimAI.Core.Source.Boot
{
    public class RimAICoreMod : Mod
    {
        public static ServiceContainer Container { get; private set; } = new();
        public static string ModRootDir { get; private set; } = string.Empty;
        private SettingsWindow _settingsWindow;
        private SettingsController _settingsController;

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
                            Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>(),
                            Container.Resolve<IHistoryService>()
                        ));
                        stage.RegisterAct(new RimAI.Core.Source.Modules.Stage.Acts.InterServerGroupChatAct(
                            Container.Resolve<ILLMService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>(),
                            Container.Resolve<IWorldDataService>(),
                            Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>(),
                            Container.Resolve<IHistoryService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>(),
                            Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>()
                        ));
                        stage.RegisterTrigger(new GlobalTimedRandomActTrigger(stage, Container.Resolve<RimAI.Core.Source.Modules.Stage.Diagnostics.IStageLogging>()));
                    }
                }
                catch { }

                // P13: Server 周期任务发现与注册改由 SchedulerGameComponent 在 tick=2500 触发（避免加载早期世界数据未就绪导致的超时/取消）

                // Harmony patches (UI button etc.)
                var harmony = new Harmony("kilokio.rimai.core");
                HarmonyPatcher.Apply(harmony);

                sw.Stop();
                // P2: resolve ILLMService to self-check readiness
                _ = Container.Resolve<ILLMService>();
                // P4: ensure tooling index attempt load (non-blocking)
                try { _ = Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>(); } catch { }
                try
                {
                    var toolingSvc = Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>();
                    var topkAvail = toolingSvc?.IsTopKAvailable() ?? false;
                    Log.Message($"[RimAI.Core][P1][P2][P3][P4][P5][P7][P8][P9] Boot OK (services={Container.GetKnownServiceCount()}, elapsed={sw.ElapsedMilliseconds} ms, topk_available={topkAvail})");
                }
                catch
                {
                    Log.Message($"[RimAI.Core][P1][P2][P3][P4][P5][P7][P8][P9] Boot OK (services={Container.GetKnownServiceCount()}, elapsed={sw.ElapsedMilliseconds} ms)");
                }

                // 初始化设置窗口（注册分区）
                try
                {
                    _settingsController = new SettingsController();
                    _settingsController.Register(new ImportantSettingsSection());
                    _settingsWindow = new SettingsWindow(_settingsController);

                    // 设置默认本地化语言：跟随游戏语言，回退 en
                    try
                    {
                        var loc = Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                        var cfg = Container.Resolve<IConfigurationService>() as ConfigurationService;
                        var overrideLocale = cfg?.GetPromptLocaleOverrideOrNull();
                        if (!string.IsNullOrWhiteSpace(overrideLocale))
                        {
                            loc?.SetDefaultLocale(overrideLocale);
                        }
                        else
                        {
                            var langFolder = LanguageDatabase.activeLanguage?.folderName ?? "English";
                            var normalized = NormalizeLocaleFromRimworld(langFolder);
                            loc?.SetDefaultLocale(normalized);
                        }
                        try { var _ = loc?.GetAvailableLocales(); } catch { }
                    }
                    catch { }
                }
                catch { }
            }
            catch (Exception ex)
            {
                sw.Stop();
                Log.Error($"[RimAI.Core][P1] Boot FAILED after {sw.ElapsedMilliseconds} ms: {ex}");
                throw;
            }
        }

        public override string SettingsCategory()
        {
            // XML 本地化键：RimAI.Settings.Category
            try { return _settingsWindow?.GetCategory() ?? "RimAI.Core"; } catch { return "RimAI.Core"; }
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            try { _settingsWindow?.DoWindowContents(inRect); } catch { }
        }

        private static string NormalizeLocaleFromRimworld(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName)) return "en";
            switch (folderName)
            {
                case "English": return "en";
                case "ChineseSimplified": return "zh-Hans";
                case "ChineseTraditional": return "zh-Hant";
                case "French": return "fr";
                case "German": return "de";
                case "Japanese": return "ja";
                case "Korean": return "ko";
                case "Russian": return "ru";
                case "SpanishLatin":
                case "Spanish": return "es";
                case "PortugueseBrazilian": return "pt-BR";
                default: return "en";
            }
        }
    }
}


