using RimAI.Core.Architecture.DI;
using System.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Services;
using Verse;

namespace RimAI.Core.Lifecycle
{
    public class RimAIMod : Mod
    {
        public RimAIMod(ModContentPack content) : base(content)
        {
            Log.Message("[RimAI.Core] Initializing...");

            var container = new ServiceContainer();

            CoreServices.Container = container;

            // 配置服务
            var configService = new ConfigurationService();
            container.Register<IConfigurationService>(configService);

            // 调度器（主线程）
            var schedulerService = new RimAI.Core.Architecture.Scheduling.SchedulerService(null);
            container.Register<ISchedulerService>(schedulerService);

            // 缓存服务（LLM 缓存）
            var chatCache = new RimAI.Core.Architecture.Caching.CacheService<string, RimAI.Framework.Contracts.UnifiedChatResponse>();
            container.Register<RimAI.Core.Contracts.Services.ICacheService<string, RimAI.Framework.Contracts.UnifiedChatResponse>>(chatCache);

            // LLM 服务
            var llmService = new LLMService(configService, chatCache);
            container.Register<ILLMService>(llmService);

            // World 数据服务
            var worldDataService = new WorldDataService(schedulerService);
            container.Register<IWorldDataService>(worldDataService);

            // 指令服务
            var commandService = new CommandService(schedulerService);
            container.Register<ICommandService>(commandService);

            // 工具注册服务（自动发现）
            var tools = RimAI.Core.Services.ToolRegistryService.DiscoverTools(container).ToList();
            var toolRegistry = new ToolRegistryService(tools);
            container.Register<IToolRegistryService>(toolRegistry);

            // 历史服务
            var historyService = new HistoryService(schedulerService);
            container.Register<IHistoryService>(historyService);

            // 持久化服务
            var persistenceService = new PersistenceService();
            container.Register<IPersistenceService>(persistenceService);

            // Prompt 工厂
            var promptFactory = new PromptFactoryService(worldDataService, historyService);
            container.Register<IPromptFactoryService>(promptFactory);

            // Orchestration Service
            var orchestrationService = new OrchestrationService(llmService, promptFactory, toolRegistry, historyService);
            container.Register<IOrchestrationService>(orchestrationService);

            // EventBus
            var eventBus = new EventBus();
            container.Register<IEventBus>(eventBus);

            container.Register(container);

            // 在已有游戏上下文中挂接 PersistenceManager & GameEventHooks
            if (Verse.Current.Game != null && !Verse.Current.Game.components.Any(c => c is PersistenceManager))
            {
                Verse.Current.Game.components.Add(new PersistenceManager());
                Verse.Current.Game.components.Add(new RimAI.Core.Events.GameEventHooks());
            }

            Log.Message("[RimAI.Core] Initialization Complete.");
        }
    }
}