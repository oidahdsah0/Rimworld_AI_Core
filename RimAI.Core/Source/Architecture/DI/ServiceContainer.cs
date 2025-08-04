using System;
using System.Collections.Generic;

#nullable enable

namespace RimAI.Core.Architecture.DI
{
    public sealed class ServiceContainer : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new()
        {
            var implementation = new TImplementation();
            _services[typeof(TService)] = implementation;
        }

        // 允许直接注册现有实例，满足需要传入构造参数的场景
        public void Register<TService>(TService instance) where TService : class
        {
            _services[typeof(TService)] = instance;
        }

        public TService Resolve<TService>() where TService : class
        {
            if (!_services.TryGetValue(typeof(TService), out var service))
            {
                throw new InvalidOperationException($"服务 {typeof(TService).Name} 未注册。");
            }
            return (TService)service;
        }

        // IServiceProvider implementation
        public object? GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out var svc);
            return svc;
        }
    }    
}