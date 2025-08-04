using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#nullable enable

namespace RimAI.Core.Architecture.DI
{
    public sealed class ServiceContainer : IServiceProvider
    {
        private enum ServiceLifetime { Singleton, Transient }

        private sealed record Registration(Func<ServiceContainer, object> Factory, ServiceLifetime Lifetime)
        {
            public object? Instance { get; set; }
        }

        private readonly ConcurrentDictionary<Type, Registration> _registrations = new();

        private readonly ConcurrentDictionary<Type, object> _singletons = new();

        // ------------------- 注册 API -------------------
        public void RegisterSingleton<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            _registrations[typeof(TService)] = new Registration(c => c.CreateInstance(typeof(TImplementation)), ServiceLifetime.Singleton);
        }

        public void RegisterSingleton<TService>(Func<ServiceContainer, TService> factory) where TService : class
        {
            _registrations[typeof(TService)] = new Registration(c => factory(c), ServiceLifetime.Singleton);
        }

        public void RegisterTransient<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService
        {
            _registrations[typeof(TService)] = new Registration(c => c.CreateInstance(typeof(TImplementation)), ServiceLifetime.Transient);
        }

        // 兼容旧代码：直接注册实例
        public void Register<TService>(TService instance) where TService : class
        {
            _registrations[typeof(TService)] = new Registration(_ => instance, ServiceLifetime.Singleton) { Instance = instance };
        }

        // ------------------- 解析 API -------------------
        public TService Resolve<TService>() where TService : class
        {
            return (TService)Resolve(typeof(TService));
        }

        public bool TryResolve<TService>(out TService? service) where TService : class
        {
            var success = TryResolve(typeof(TService), out var obj);
            service = (TService?)obj;
            return success;
        }

        public object Resolve(Type serviceType)
        {
            if (TryResolve(serviceType, out var instance))
                return instance!;

            throw new InvalidOperationException($"服务 {serviceType.Name} 未注册。");
        }

        private bool TryResolve(Type serviceType, out object? instance)
        {
            if (_registrations.TryGetValue(serviceType, out var registration))
            {
                if (registration.Lifetime == ServiceLifetime.Singleton)
                {
                    if (registration.Instance == null)
                    {
                        lock (registration)
                        {
                            if (registration.Instance == null)
                            {
                                registration.Instance = registration.Factory(this);
                            }
                        }
                    }
                    instance = registration.Instance;
                    return true;
                }
                else // Transient
                {
                    instance = registration.Factory(this);
                    return true;
                }
            }

            instance = null;
            return false;
        }

        // ------------------- IServiceProvider -------------------
        public object? GetService(Type serviceType) => TryResolve(serviceType, out var svc) ? svc : null;

        // ------------------- 工具方法 -------------------
        private object CreateInstance(Type implementationType)
        {
            // 选择参数最多的构造函数（更适合依赖注入）
            var ctor = implementationType.GetConstructors()
                                          .OrderByDescending(c => c.GetParameters().Length)
                                          .FirstOrDefault();
            if (ctor == null)
                throw new InvalidOperationException($"类型 {implementationType.Name} 没有公共构造函数，无法实例化。");

            var parameters = ctor.GetParameters();
            if (parameters.Length == 0)
                return Activator.CreateInstance(implementationType)!;

            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                args[i] = Resolve(parameters[i].ParameterType);
            }
            return ctor.Invoke(args);
        }
    }
}