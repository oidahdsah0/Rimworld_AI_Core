using System;
using System.Collections.Generic;
using RimAI.Core.Analysis;
using RimAI.Core.Architecture.Interfaces;
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
            _services = new Dictionary<Type, object>();
            _factories = new Dictionary<Type, Func<object>>();
            
            RegisterDefaultServices();
        }

        /// <summary>
        /// 注册服务实例
        /// </summary>
        public void RegisterInstance<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            lock (_lock)
            {
                _services[typeof(T)] = instance;
                Log.Message($"[ServiceContainer] Registered instance of {typeof(T).Name}");
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
            // 注册单例服务
            RegisterInstance<IColonyAnalyzer>(ColonyAnalyzer.Instance);
            RegisterInstance<IPromptBuilder>(PromptBuilder.Instance);
            RegisterInstance<ILLMService>(LLMService.Instance);
            RegisterInstance<ICacheService>(CacheService.Instance);
            RegisterInstance<IEventBus>(EventBusService.Instance);
            
            // 注册新的分析器和服务
            RegisterInstance<SecurityAnalyzer>(SecurityAnalyzer.Instance);
            RegisterInstance<AutomationService>(AutomationService.Instance);

            Log.Message("[ServiceContainer] Default services registered");
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
        public static IColonyAnalyzer Analyzer => ServiceContainer.Instance.GetService<IColonyAnalyzer>();
        public static IPromptBuilder PromptBuilder => ServiceContainer.Instance.GetService<IPromptBuilder>();
        public static ILLMService LLMService => ServiceContainer.Instance.GetService<ILLMService>();
        public static ICacheService CacheService => ServiceContainer.Instance.GetService<ICacheService>();
        public static IEventBus EventBus => ServiceContainer.Instance.GetService<IEventBus>();

        /// <summary>
        /// 检查所有核心服务是否可用
        /// </summary>
        public static bool AreServicesReady()
        {
            try
            {
                return Analyzer != null &&
                       PromptBuilder != null &&
                       LLMService != null &&
                       CacheService != null &&
                       EventBus != null;
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
                report.AppendLine($"分析器: {(Analyzer != null ? "✅" : "❌")}");
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
