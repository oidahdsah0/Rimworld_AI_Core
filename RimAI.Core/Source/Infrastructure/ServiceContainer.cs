using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using RimAI.Core.Source.Infrastructure.Diagnostics;

namespace RimAI.Core.Source.Infrastructure
{
    /// <summary>
    /// Simple DI container focused on constructor injection and singletons.
    /// Provides pre-warm Init, cycle detection, ResolveAll and health reporting.
    /// </summary>
    public sealed class ServiceContainer
    {
        private readonly Dictionary<Type, Type> _registrations = new();
        private readonly Dictionary<Type, object> _singletons = new();
        private readonly Dictionary<Type, TimeSpan> _constructTimes = new();

        public void Register<TInterface, TImplementation>()
            where TImplementation : class, TInterface
        {
            var iface = typeof(TInterface);
            var impl = typeof(TImplementation);
            if (_registrations.ContainsKey(iface))
            {
                throw new InvalidOperationException($"Service '{iface.FullName}' already registered.");
            }
            _registrations[iface] = impl;
        }

        public void RegisterInstance<TInterface>(TInterface instance)
        {
            var iface = typeof(TInterface);
            if (_singletons.ContainsKey(iface) || _registrations.ContainsKey(iface))
            {
                throw new InvalidOperationException($"Service '{iface.FullName}' already registered.");
            }
            _singletons[iface] = instance!;
            _constructTimes[iface] = TimeSpan.Zero;
        }

        public TInterface Resolve<TInterface>() => (TInterface)Resolve(typeof(TInterface));

        public object Resolve(Type serviceType)
        {
            return Resolve(serviceType, new Stack<Type>());
        }

        private object Resolve(Type serviceType, Stack<Type> constructingStack)
        {
            if (_singletons.TryGetValue(serviceType, out var existing))
            {
                return existing;
            }

            if (!_registrations.TryGetValue(serviceType, out var implType))
            {
                // allow resolving implementation directly if registered by iface? keep strict
                throw new InvalidOperationException($"Service '{serviceType.FullName}' not registered.");
            }

            var sw = Stopwatch.StartNew();
            var instance = CreateWithCtorInjection(implType, constructingStack);
            sw.Stop();
            _singletons[serviceType] = instance;
            _constructTimes[serviceType] = sw.Elapsed;
            return instance;
        }

        public void Init()
        {
            // Prewarm: resolve all registered services. Fail Fast on first error.
            foreach (var serviceType in _registrations.Keys.ToList())
            {
                _ = Resolve(serviceType);
            }
        }

        public IReadOnlyList<ServiceHealth> ResolveAllAndGetHealth()
        {
            var results = new List<ServiceHealth>();
            foreach (var t in _registrations.Keys)
            {
                try
                {
                    _ = Resolve(t, new Stack<Type>());
                    _constructTimes.TryGetValue(t, out var elapsed);
                    results.Add(new ServiceHealth(t.FullName ?? t.Name, true, elapsed, null));
                }
                catch (Exception ex)
                {
                    results.Add(new ServiceHealth(t.FullName ?? t.Name, false, TimeSpan.Zero, ex.Message));
                }
            }
            foreach (var singleton in _singletons.Keys)
            {
                if (!_registrations.ContainsKey(singleton))
                {
                    // manually registered instances health row
                    _constructTimes.TryGetValue(singleton, out var elapsed);
                    results.Add(new ServiceHealth(singleton.FullName ?? singleton.Name, true, elapsed, null));
                }
            }
            return results;
        }

        public int GetKnownServiceCount()
        {
            var set = new HashSet<Type>(_registrations.Keys);
            foreach (var s in _singletons.Keys)
            {
                set.Add(s);
            }
            return set.Count;
        }

        private object CreateWithCtorInjection(Type implementationType, Stack<Type> constructingStack)
        {
            if (constructingStack.Contains(implementationType))
            {
                var cycle = string.Join(" -> ", constructingStack.Reverse().Select(t => t.Name).Concat(new[] { implementationType.Name }));
                throw new InvalidOperationException($"Cyclic dependency detected: {cycle}");
            }
            constructingStack.Push(implementationType);
            try
            {
                var ctor = SelectConstructor(implementationType);
                var parameters = ctor.GetParameters();
                var args = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    var paramType = parameters[i].ParameterType;
                    // only resolve interfaces or concrete types explicitly registered
                    args[i] = Resolve(paramType);
                }
                var sw = Stopwatch.StartNew();
                var instance = ctor.Invoke(args);
                sw.Stop();
                _constructTimes[implementationType] = sw.Elapsed;
                return instance;
            }
            finally
            {
                constructingStack.Pop();
            }
        }

        private static ConstructorInfo SelectConstructor(Type implementationType)
        {
            var ctors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (ctors.Length == 0)
            {
                throw new InvalidOperationException($"Type '{implementationType.FullName}' has no public constructors.");
            }
            // pick the constructor with the most parameters
            return ctors.OrderByDescending(c => c.GetParameters().Length).First();
        }
    }
}


