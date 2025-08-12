using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimAI.Core.Contracts;
using ContractsOrc = RimAI.Core.Contracts;

namespace RimAI.Core.Infrastructure
{
    /// <summary>
    /// P1 版本的 <c>ServiceContainer</c>。
    /// 1. 支持 <c>Register&lt;TInterface, TImplementation&gt;()</c> 反射构造注册。
    /// 2. 支持递归依赖解析与循环依赖检测。
    /// 3. 保留 <c>RegisterInstance</c> 以便测试或特殊场景注入。
    /// </summary>
    public static class ServiceContainer
    {
        private static readonly Dictionary<Type, object> _singletons = new();
        private static readonly Dictionary<Type, Type> _registrations = new();
        private static bool _initialized;
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// 初始化容器并注册核心内部服务（如 ConfigurationService）。
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;

            // ---- 在此注册核心默认服务 ----
            Register<RimAI.Core.Infrastructure.Configuration.IConfigurationService,
                     RimAI.Core.Infrastructure.Configuration.ConfigurationService>();
            // CacheService 已下沉至 Framework，Core 不再注册本地缓存实现
            Register<RimAI.Core.Modules.LLM.ILLMService,
                     RimAI.Core.Modules.LLM.LLMService>();
            // P9-S2: Embedding & RAG
            Register<RimAI.Core.Modules.Embedding.IEmbeddingService,
                     RimAI.Core.Modules.Embedding.EmbeddingService>();
            Register<RimAI.Core.Modules.Embedding.IRagIndexService,
                     RimAI.Core.Modules.Embedding.RagIndexService>();
            // P3: SchedulerService 注册
            Register<RimAI.Core.Infrastructure.ISchedulerService,
                     RimAI.Core.Infrastructure.SchedulerService>();
            // P3: WorldDataService 注册
            Register<RimAI.Core.Modules.World.IWorldDataService,
                     RimAI.Core.Modules.World.WorldDataService>();
            // P4: ToolRegistryService 注册
            Register<RimAI.Core.Contracts.Tooling.IToolRegistryService,
                     RimAI.Core.Modules.Tooling.ToolRegistryService>();
            // P12-D1: 工具仅编排统一入口（不触达 LLM、不做自动判断）。
            Register<RimAI.Core.Contracts.IOrchestrationService,
                     RimAI.Core.Modules.Orchestration.OrchestrationService>();
            // 注册四种工具匹配模式与解析器
            Register<RimAI.Core.Modules.Orchestration.Modes.ClassicMode,
                     RimAI.Core.Modules.Orchestration.Modes.ClassicMode>();
            Register<RimAI.Core.Modules.Orchestration.Modes.FastTop1Mode,
                     RimAI.Core.Modules.Orchestration.Modes.FastTop1Mode>();
            Register<RimAI.Core.Modules.Orchestration.Modes.NarrowTopKMode,
                     RimAI.Core.Modules.Orchestration.Modes.NarrowTopKMode>();
            Register<RimAI.Core.Modules.Orchestration.Modes.LightningFastMode,
                     RimAI.Core.Modules.Orchestration.Modes.LightningFastMode>();
            Register<RimAI.Core.Modules.Orchestration.Modes.ToolMatchModeResolver,
                     RimAI.Core.Modules.Orchestration.Modes.ToolMatchModeResolver>();
            // S2.5: 注册工具向量索引服务（需在策略解析前就绪）
            Register<RimAI.Core.Modules.Embedding.IToolVectorIndexService,
                     RimAI.Core.Modules.Embedding.ToolVectorIndexService>();
            // P8: PersonaService 注册（策略构造需要）
            Register<RimAI.Core.Contracts.Services.IPersonaService,
                     RimAI.Core.Modules.Persona.PersonaService>();
            // P10-M4: Persona Binding Service 注册
            Register<RimAI.Core.Modules.Persona.IPersonaBindingService,
                     RimAI.Core.Modules.Persona.PersonaBindingService>();
            // P8: Event Bus / Aggregator 注册（非构造期依赖，但提前注册更安全）
            Register<RimAI.Core.Contracts.Eventing.IEventBus,
                     RimAI.Core.Modules.Eventing.EventBus>();
            Register<RimAI.Core.Contracts.Eventing.IEventAggregatorService,
                     RimAI.Core.Modules.Eventing.EventAggregatorService>();
            // P6: HistoryService 注册
            Register<RimAI.Core.Contracts.Services.IHistoryService,
                     RimAI.Core.Services.HistoryService>();
            // 对外只读历史查询接口复用同一实例 + 内部写接口
            Register<RimAI.Core.Contracts.Services.IHistoryQueryService,
                     RimAI.Core.Services.HistoryService>();
            Register<RimAI.Core.Services.IHistoryWriteService,
                     RimAI.Core.Services.HistoryService>();
            // P6: PersistenceService 注册
            Register<RimAI.Core.Infrastructure.Persistence.IPersistenceService,
                     RimAI.Core.Infrastructure.Persistence.PersistenceService>();
            // P12-D1: 清理策略注册（去除直接触达 LLM 的编排策略）。
            // 若后续保留策略架构，请在 D2/D3 以 Tool-only 为核心重塑。

            // P10-M1: 新增内部服务注册
            Register<RimAI.Core.Modules.World.IParticipantIdService,
                     RimAI.Core.Modules.World.ParticipantIdService>();
            Register<RimAI.Core.Modules.History.IRecapService,
                     RimAI.Core.Modules.History.RecapService>();
            // 提示词服务合并（P11.6）：统一入口 IPromptService
            Register<RimAI.Core.Modules.Prompt.IPromptService,
                     RimAI.Core.Modules.Prompt.PromptService>();
            // 内部仍需模板与 Composer 实现
            Register<RimAI.Core.Modules.Prompting.IPromptTemplateService,
                     RimAI.Core.Modules.Prompting.PromptTemplateService>();
            Register<RimAI.Core.Modules.Prompting.IPromptComposer,
                     RimAI.Core.Modules.Prompting.PromptComposer>();

            // D2+: Organizer（Chat-闲聊默认组织者）
            Register<RimAI.Core.Modules.Orchestration.PromptOrganizers.IPromptOrganizer,
                     RimAI.Core.Modules.Orchestration.PromptOrganizers.ChatIdlePromptOrganizer>();

            // 暂时保留 Persona 会话服务（D3/D4 后删除）。
            Register<RimAI.Core.Modules.Persona.IPersonaConversationService,
                     RimAI.Core.Modules.Persona.PersonaConversationService>();

            // P10-M3: 固定提示词 & 人物传记（内存 MVP）
            Register<RimAI.Core.Modules.Persona.IFixedPromptService,
                     RimAI.Core.Modules.Persona.FixedPromptService>();
            Register<RimAI.Core.Modules.Persona.IBiographyService,
                     RimAI.Core.Modules.Persona.BiographyService>();

            // P10.5-M1: 个人观点与意识形态服务
            Register<RimAI.Core.Modules.Persona.IPersonalBeliefsAndIdeologyService,
                     RimAI.Core.Modules.Persona.PersonalBeliefsAndIdeologyService>();

            // P10-M5: Relations Index（只读）
            Register<RimAI.Core.Services.IRelationsIndexService,
                     RimAI.Core.Services.RelationsIndexService>();

            // 预先构造配置服务实例，便于后续使用。
            var cfgImpl = Resolve(typeof(RimAI.Core.Infrastructure.Configuration.IConfigurationService));
            // 将同一实现同时注册为对外只读接口实例，避免双实例不一致
            RegisterInstance(typeof(RimAI.Core.Contracts.Services.IConfigurationService), cfgImpl);

            // P11.5: Stage/Organizer 服务 + Kernel
            Register<RimAI.Core.Modules.Stage.Kernel.IStageKernel,
                     RimAI.Core.Modules.Stage.Kernel.StageKernel>();
            Register<RimAI.Core.Modules.Stage.History.IStageHistoryService,
                     RimAI.Core.Modules.Stage.History.StageHistoryService>();
            Register<RimAI.Core.Modules.Stage.IStageService,
                     RimAI.Core.Modules.Stage.StageService>();
            // 气泡显示订阅器（轻量构造，订阅事件总线）
            Register<RimAI.Core.Modules.Stage.Bubbles.StageBubbleSink,
                     RimAI.Core.Modules.Stage.Bubbles.StageBubbleSink>();

            // P11-M3: Topic & Act 注册（最小）
            Register<RimAI.Core.Modules.Stage.Topic.ITopicService,
                     RimAI.Core.Modules.Stage.Topic.TopicService>();
            Register<RimAI.Core.Modules.Stage.Topic.ITopicProvider,
                     RimAI.Core.Modules.Stage.Topic.HistoryRecapProvider>();
            Register<RimAI.Core.Modules.Stage.Topic.ITopicProvider,
                     RimAI.Core.Modules.Stage.Topic.RandomPoolProvider>();
            Register<RimAI.Core.Modules.Stage.Acts.IStageAct,
                     RimAI.Core.Modules.Stage.Acts.GroupChatAct>();
            // 触发器注册
            Register<RimAI.Core.Modules.Stage.Triggers.IStageTrigger,
                     RimAI.Core.Modules.Stage.Triggers.GroupChatTrigger>();
            try
            {
                var proxTrig = (RimAI.Core.Modules.Stage.Triggers.GroupChatTrigger)
                    Resolve(typeof(RimAI.Core.Modules.Stage.Triggers.GroupChatTrigger));
                var trigList = new System.Collections.Generic.List<RimAI.Core.Modules.Stage.Triggers.IStageTrigger> { proxTrig };
                RegisterInstance(typeof(System.Collections.Generic.IEnumerable<RimAI.Core.Modules.Stage.Triggers.IStageTrigger>), trigList);
            }
            catch { }

            // 组装 Topic Providers 列表供 ITopicService 注入（IEnumerable<ITopicProvider>）
            try
            {
                var histProv = (RimAI.Core.Modules.Stage.Topic.HistoryRecapProvider)
                    Resolve(typeof(RimAI.Core.Modules.Stage.Topic.HistoryRecapProvider));
                var randomProv = (RimAI.Core.Modules.Stage.Topic.RandomPoolProvider)
                    Resolve(typeof(RimAI.Core.Modules.Stage.Topic.RandomPoolProvider));
                var topicProviders = new System.Collections.Generic.List<RimAI.Core.Modules.Stage.Topic.ITopicProvider>
                {
                    histProv,
                    randomProv
                };
                RegisterInstance(typeof(System.Collections.Generic.IEnumerable<RimAI.Core.Modules.Stage.Topic.ITopicProvider>), topicProviders);
            }
            catch { /* ignore */ }

            // P12-D1: 移除策略集合注入，避免触发不必要的构造。

            // P10-M1: 订阅历史新增事件 → RecapService（一次性订阅）
            try
            {
                var historyWrite = (RimAI.Core.Services.IHistoryWriteService)Resolve(typeof(RimAI.Core.Services.IHistoryWriteService));
                var recap = (RimAI.Core.Modules.History.IRecapService)Resolve(typeof(RimAI.Core.Modules.History.IRecapService));
                historyWrite.OnEntryRecorded += recap.OnEntryRecorded;
            }
            catch { /* ignore */ }

            // P11.5: 扫描迁移至 Triggers（兼容扫描注册已移除）

            _initialized = true;
        }

        #region Registration APIs

        public static void Register<TInterface, TImplementation>() where TImplementation : TInterface
        {
            lock (_syncRoot)
            {
                var key = typeof(TInterface);
                var newImpl = typeof(TImplementation);
                if (_registrations.TryGetValue(key, out var existing))
                {
                    if (existing == newImpl)
                    {
                        CoreServices.Logger.Warn($"[DI] Duplicate registration ignored: {key.FullName} → {newImpl.FullName}");
                    }
                    else
                    {
                        CoreServices.Logger.Warn($"[DI] Registration override: {key.FullName} from {existing.FullName} → {newImpl.FullName}");
                    }
                }
                _registrations[key] = newImpl;
            }
        }

        public static void RegisterInstance<TInterface>(TInterface instance) where TInterface : class
        {
            RegisterInstance(typeof(TInterface), instance);
        }

        public static void RegisterInstance(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (_syncRoot)
            {
                _singletons[serviceType] = instance;
            }
        }

        #endregion

        #region Resolve APIs

        public static T Get<T>() where T : class => (T)Resolve(typeof(T));

        /// <summary>
        /// 尝试获取已构造的单例实例（若不存在则不触发构造）。
        /// </summary>
        public static bool TryGetExisting<T>(out T instance) where T : class
        {
            if (_singletons.TryGetValue(typeof(T), out var existing))
            {
                instance = (T)existing;
                return true;
            }
            instance = null;
            return false;
        }

        private static object Resolve(Type serviceType)
        {
            return Resolve(serviceType, new HashSet<Type>());
        }

        private static object Resolve(Type serviceType, HashSet<Type> resolutionStack)
        {
            lock (_syncRoot)
            {
                // 1. 已有实例
                if (_singletons.TryGetValue(serviceType, out var existing)) return existing;

                // 2. 找到注册的实现
                if (!_registrations.TryGetValue(serviceType, out var implType))
                {
                    // 若请求的是实现自身，允许直接构造
                    implType = serviceType.IsInterface ? null : serviceType;
                    if (implType == null)
                        throw new InvalidOperationException($"[RimAI] Service {serviceType.FullName} 未注册。");
                }

                // 3. 反射构造（携带共享的解析栈以检测循环依赖）
                var instance = CreateInstance(implType, resolutionStack);
                _singletons[serviceType] = instance;
                return instance;
            }
        }

        private static object CreateInstance(Type implType, HashSet<Type> resolutionStack)
        {
            if (resolutionStack.Contains(implType))
            {
                var chain = string.Join(" → ", resolutionStack.Select(t => t.FullName).Concat(new[] { implType.FullName }));
                var msg = $"[RimAI] 循环依赖: {chain}";
                try { CoreServices.Logger.Error(msg); } catch { /* ignore */ }
                throw new InvalidOperationException(msg);
            }
            resolutionStack.Add(implType);

            // 选择参数最多的公共构造函数
            var ctor = implType
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null)
                throw new InvalidOperationException($"[RimAI] 类型 {implType.FullName} 缺少公共构造函数。");

            var parameters = ctor.GetParameters();
            object[] args = Array.Empty<object>();
            if (parameters.Length > 0)
            {
                args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = Resolve(parameters[i].ParameterType, resolutionStack);
                }
            }

            resolutionStack.Remove(implType);
            return Activator.CreateInstance(implType, args)!;
        }

        #endregion
    }
}