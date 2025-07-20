using System;
using System.Collections.Generic;
using RimWorld;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Analysis;
using RimAI.Core.Officers;
using RimAI.Core.Prompts;
using RimAI.Core.Services;
using Verse;

namespace RimAI.Core.Architecture
{
    /// <summary>
    /// 服务容器 - 管理所有核心服务的生命周期
    /// </summary>
    public class ServiceContainer
    {
        private static ServiceContainer _instance;
        public static ServiceContainer Instance => _instance ??= new ServiceContainer();

        private readonly Dictionary<Type, object> _services;
        private readonly Dictionary<Type, Func<object>> _factories;
        private readonly object _lock = new object();

        private ServiceContainer()
        {
            try
            {
                Log.Message("[ServiceContainer] 🔧 Initializing ServiceContainer...");
                
                _services = new Dictionary<Type, object>();
                _factories = new Dictionary<Type, Func<object>>();
                
                Log.Message("[ServiceContainer] 📋 Registering default services...");
                RegisterDefaultServices();
                
                Log.Message("[ServiceContainer] ✅ ServiceContainer initialized successfully");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ServiceContainer] ❌ CRITICAL: Failed to initialize ServiceContainer: {ex}");
                Log.Error($"[ServiceContainer] Stack trace: {ex.StackTrace}");
                throw; // 重新抛出，这是关键错误
            }
        }

        /// <summary>
        /// 注册服务实例
        /// </summary>
        public void RegisterInstance<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            try
            {
                lock (_lock)
                {
                    _services[typeof(T)] = instance;
                    Log.Message($"[ServiceContainer] ✅ Registered instance of {typeof(T).Name}");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ServiceContainer] ❌ Failed to register {typeof(T).Name}: {ex}");
                throw;
            }
        }

        /// <summary>
        /// 注册服务工厂
        /// </summary>
        public void RegisterFactory<T>(Func<T> factory) where T : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            lock (_lock)
            {
                _factories[typeof(T)] = () => factory();
                Log.Message($"[ServiceContainer] Registered factory for {typeof(T).Name}");
            }
        }

        /// <summary>
        /// 获取服务实例
        /// </summary>
        public T GetService<T>() where T : class
        {
            var serviceType = typeof(T);
            
            lock (_lock)
            {
                // 首先检查已注册的实例
                if (_services.TryGetValue(serviceType, out var instance))
                {
                    return (T)instance;
                }

                // 然后检查工厂
                if (_factories.TryGetValue(serviceType, out var factory))
                {
                    var newInstance = (T)factory();
                    _services[serviceType] = newInstance; // 缓存实例
                    return newInstance;
                }

                Log.Warning($"[ServiceContainer] Service {serviceType.Name} not found");
                return null;
            }
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public bool IsRegistered<T>() where T : class
        {
            var serviceType = typeof(T);
            lock (_lock)
            {
                return _services.ContainsKey(serviceType) || _factories.ContainsKey(serviceType);
            }
        }

        /// <summary>
        /// 移除服务
        /// </summary>
        public void RemoveService<T>() where T : class
        {
            var serviceType = typeof(T);
            lock (_lock)
            {
                _services.Remove(serviceType);
                _factories.Remove(serviceType);
                Log.Message($"[ServiceContainer] Removed service {serviceType.Name}");
            }
        }

        /// <summary>
        /// 清除所有服务（主要用于测试）
        /// </summary>
        public void ClearAll()
        {
            lock (_lock)
            {
                _services.Clear();
                _factories.Clear();
                RegisterDefaultServices(); // 重新注册默认服务
                Log.Message("[ServiceContainer] All services cleared and defaults re-registered");
            }
        }

        /// <summary>
        /// 注册默认服务
        /// </summary>
        private void RegisterDefaultServices()
        {
            try
            {
                Log.Message("[ServiceContainer] 📋 Step 1: Registering ColonyAnalyzer...");
                RegisterInstance<IColonyAnalyzer>(ColonyAnalyzer.Instance);
                
                Log.Message("[ServiceContainer] 📋 Step 2: Registering PromptBuilder...");
                RegisterInstance<IPromptBuilder>(PromptBuilder.Instance);
                
                Log.Message("[ServiceContainer] 📋 Step 3: Registering LLMService...");
                RegisterInstance<ILLMService>(LLMService.Instance);
                
                Log.Message("[ServiceContainer] 📋 Step 4: Registering CacheService...");
                RegisterInstance<ICacheService>(CacheService.Instance);
                
                Log.Message("[ServiceContainer] 📋 Step 5: Registering EventBusService...");
                RegisterInstance<IEventBus>(EventBusService.Instance);
                
                Log.Message("[ServiceContainer] 📋 Step 6: Registering Governor...");
                // 注册AI官员 - 重要的架构修正！
                RegisterInstance<IAIOfficer>(Governor.Instance); // 注册总督为默认官员
                RegisterInstance<Governor>(Governor.Instance);   // 也允许直接类型访问

                Log.Message("[ServiceContainer] 📋 Step 7: Setting up EventBus integration...");
                // 🎯 注册事件监听器 - 展示完整的企业级架构！
                var eventBus = GetService<IEventBus>();
                if (eventBus != null)
                {
                    var governorEventListener = new RimAI.Core.Officers.Events.GovernorEventListener();
                    eventBus.Subscribe<RimAI.Core.Officers.Events.GovernorAdviceEvent>(governorEventListener);
                    Log.Message("[ServiceContainer] ✅ GovernorEventListener registered with EventBus");
                }
                else
                {
                    Log.Warning("[ServiceContainer] ⚠️ EventBus is null, skipping listener registration");
                }

                Log.Message("[ServiceContainer] ✅ All default services registered successfully");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ServiceContainer] ❌ CRITICAL: Failed to register default services: {ex}");
                Log.Error($"[ServiceContainer] Stack trace: {ex.StackTrace}");
                throw; // 重新抛出，这是关键错误
            }
        }

        /// <summary>
        /// 获取服务状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            lock (_lock)
            {
                var instanceCount = _services.Count;
                var factoryCount = _factories.Count;
                
                return $"服务容器状态: {instanceCount} 个实例, {factoryCount} 个工厂";
            }
        }

        /// <summary>
        /// 获取所有已注册服务的详细信息
        /// </summary>
        public List<string> GetRegisteredServices()
        {
            var services = new List<string>();
            
            lock (_lock)
            {
                foreach (var kvp in _services)
                {
                    services.Add($"实例: {kvp.Key.Name}");
                }
                
                foreach (var kvp in _factories)
                {
                    services.Add($"工厂: {kvp.Key.Name}");
                }
            }
            
            return services;
        }
    }

    /// <summary>
    /// 核心服务访问器 - 提供便捷的服务访问方式
    /// </summary>
    public static class CoreServices
    {
        public static IColonyAnalyzer Analyzer => ServiceContainer.Instance.GetService<IColonyAnalyzer>(); // 重新启用
        public static IPromptBuilder PromptBuilder => ServiceContainer.Instance.GetService<IPromptBuilder>();
        public static ILLMService LLMService => ServiceContainer.Instance.GetService<ILLMService>();
        public static ICacheService CacheService => ServiceContainer.Instance.GetService<ICacheService>();
        public static IEventBus EventBus => ServiceContainer.Instance.GetService<IEventBus>();
        
        // RimWorld API 安全访问服务（静态服务）
        public static class SafeAccess
        {
            public static List<Pawn> GetColonistsSafe(Map map) => SafeAccessService.GetColonistsSafe(map);
            public static List<Pawn> GetPrisonersSafe(Map map) => SafeAccessService.GetPrisonersSafe(map);
            public static List<Pawn> GetAllPawnsSafe(Map map) => SafeAccessService.GetAllPawnsSafe(map);
            public static List<Building> GetBuildingsSafe(Map map) => SafeAccessService.GetBuildingsSafe(map);
            public static List<Thing> GetThingsSafe(Map map, ThingDef thingDef) => SafeAccessService.GetThingsSafe(map, thingDef);
            public static List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group) => SafeAccessService.GetThingGroupSafe(map, group);
            public static int GetColonistCountSafe(Map map) => SafeAccessService.GetColonistCountSafe(map);
            public static WeatherDef GetCurrentWeatherSafe(Map map) => SafeAccessService.GetCurrentWeatherSafe(map);
            public static int GetTicksGameSafe() => SafeAccessService.GetTicksGameSafe();
            public static Season GetCurrentSeasonSafe(Map map) => SafeAccessService.GetCurrentSeasonSafe(map);
            public static string GetStatusReport() => SafeAccessService.GetStatusReport();
        }
        
        // AI官员服务
        public static IAIOfficer DefaultOfficer => ServiceContainer.Instance.GetService<IAIOfficer>();
        public static Governor Governor => ServiceContainer.Instance.GetService<Governor>();

        /// <summary>
        /// 检查所有核心服务是否可用
        /// </summary>
        public static bool AreServicesReady()
        {
            try
            {
                return Analyzer != null && // 重新启用分析器检查
                       PromptBuilder != null &&
                       LLMService != null &&
                       CacheService != null &&
                       EventBus != null &&
                       Governor != null; // 添加总督检查
            }
            catch (Exception ex)
            {
                Log.Error($"[CoreServices] Error checking service readiness: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取服务就绪状态报告
        /// </summary>
        public static string GetReadinessReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("🔧 核心服务状态报告:");

            try
            {
                report.AppendLine($"殖民地分析器: {(Analyzer != null ? "✅" : "❌")}"); // 重新启用
                report.AppendLine($"提示词构建器: {(PromptBuilder != null ? "✅" : "❌")}");
                report.AppendLine($"LLM服务: {(LLMService != null ? "✅" : "❌")}");
                report.AppendLine($"缓存服务: {(CacheService != null ? "✅" : "❌")}");
                report.AppendLine($"事件总线: {(EventBus != null ? "✅" : "❌")}");

                if (LLMService != null)
                {
                    report.AppendLine($"AI框架连接: {(LLMService.IsInitialized ? "✅ 已连接" : "❌ 未连接")}");
                    report.AppendLine($"流式支持: {(LLMService.IsStreamingAvailable ? "✅ 支持" : "❌ 不支持")}");
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ 状态检查失败: {ex.Message}");
            }

            return report.ToString();
        }
    }
}
