using System;
using System.Collections.Generic;

namespace RimAI.Core.Architecture.DI
{
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<TService, TImplementation>()
            where TService : class
            where TImplementation : class, TService, new()
        {
            var implementation = new TImplementation();
            _services[typeof(TService)] = implementation;
        }

        public TService Resolve<TService>() where TService : class
        {
            if (!_services.TryGetValue(typeof(TService), out var service))
            {
                throw new InvalidOperationException($"服务 {typeof(TService).Name} 未注册。");
            }
            return (TService)service;
        }
    }    
}