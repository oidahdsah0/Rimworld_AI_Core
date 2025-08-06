using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        /// <summary>
        /// 初始化容器并注册核心内部服务（如 ConfigurationService）。
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;

            // ---- 在此注册核心默认服务 ----
            Register<RimAI.Core.Infrastructure.Configuration.IConfigurationService,
                     RimAI.Core.Infrastructure.Configuration.ConfigurationService>();
            Register<RimAI.Core.Infrastructure.Cache.ICacheService,
                     RimAI.Core.Infrastructure.Cache.CacheService>();
            Register<RimAI.Core.Modules.LLM.ILLMService,
                     RimAI.Core.Modules.LLM.LLMService>();
            // P3: SchedulerService 注册
            Register<RimAI.Core.Infrastructure.ISchedulerService,
                     RimAI.Core.Infrastructure.SchedulerService>();
            // P3: WorldDataService 注册
            Register<RimAI.Core.Modules.World.IWorldDataService,
                     RimAI.Core.Modules.World.WorldDataService>();
            // P4: ToolRegistryService 注册
            Register<RimAI.Core.Contracts.Tooling.IToolRegistryService,
                     RimAI.Core.Modules.Tooling.ToolRegistryService>();

            // 预先构造配置服务实例，便于后续使用。
            Resolve(typeof(RimAI.Core.Infrastructure.Configuration.IConfigurationService));

            _initialized = true;
        }

        #region Registration APIs

        public static void Register<TInterface, TImplementation>() where TImplementation : TInterface
        {
            _registrations[typeof(TInterface)] = typeof(TImplementation);
        }

        public static void RegisterInstance<TInterface>(TInterface instance) where TInterface : class
        {
            RegisterInstance(typeof(TInterface), instance);
        }

        public static void RegisterInstance(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _singletons[serviceType] = instance;
        }

        #endregion

        #region Resolve APIs

        public static T Get<T>() where T : class => (T)Resolve(typeof(T));

        private static object Resolve(Type serviceType)
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

            // 3. 反射构造
            var instance = CreateInstance(implType, new HashSet<Type>());
            _singletons[serviceType] = instance;
            return instance;
        }

        private static object CreateInstance(Type implType, HashSet<Type> resolutionStack)
        {
            if (resolutionStack.Contains(implType))
                throw new InvalidOperationException($"[RimAI] 循环依赖检测: {implType.FullName}.");
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
                    args[i] = Resolve(parameters[i].ParameterType);
                }
            }

            resolutionStack.Remove(implType);
            return Activator.CreateInstance(implType, args)!;
        }

        #endregion
    }
}