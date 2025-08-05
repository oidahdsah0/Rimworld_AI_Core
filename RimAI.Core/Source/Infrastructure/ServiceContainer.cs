using System;
using System.Collections.Generic;
using System.Linq;

namespace RimAI.Core.Infrastructure
{
    /// <summary>
    /// Lightweight DI container with constructor injection and cycle detection (P1).
    /// </summary>
    public static class ServiceContainer
    {
        private static readonly Dictionary<Type, Type> _typeMap = new();
        private static readonly Dictionary<Type, object> _singletons = new();
        private static readonly HashSet<Type> _resolving = new();
        private static bool _initialized;

        #region Registration

        public static void Register<TInterface, TImplementation>() where TImplementation : class
        {
            _typeMap[typeof(TInterface)] = typeof(TImplementation);
        }

        public static void RegisterInstance<TInterface>(TInterface instance)
        {
            _singletons[typeof(TInterface)] = instance!;
        }

        #endregion

        #region Resolve

        public static TInterface Resolve<TInterface>() => (TInterface)Resolve(typeof(TInterface));

        private static object Resolve(Type type)
        {
            if (_singletons.TryGetValue(type, out var cached))
                return cached;

            if (_resolving.Contains(type))
                throw new InvalidOperationException($"Cyclic dependency detected while resolving {type.FullName}");

            _resolving.Add(type);
            try
            {
                var implType = ResolveImplementationType(type);
                var ctor = SelectConstructor(implType);
                var parameters = ctor.GetParameters();
                var args = parameters.Select(p => Resolve(p.ParameterType)).ToArray();
                var instance = Activator.CreateInstance(implType, args)!;
                _singletons[type] = instance;
                return instance;
            }
            finally
            {
                _resolving.Remove(type);
            }
        }

        private static Type ResolveImplementationType(Type abstraction)
        {
            if (_typeMap.TryGetValue(abstraction, out var impl))
                return impl;

            if (!abstraction.IsAbstract && !abstraction.IsInterface)
                return abstraction; // concrete type registered by itself

            throw new InvalidOperationException($"No registration for {abstraction.FullName}");
        }

        private static System.Reflection.ConstructorInfo SelectConstructor(Type type)
        {
            // Prefer the ctor with most parameters
            return type.GetConstructors()
                       .OrderByDescending(c => c.GetParameters().Length)
                       .First();
        }

        #endregion

        /// <summary>
        /// Initialise container with built-in registrations.
        /// </summary>
        public static void Init()
        {
            if (_initialized) return;
            _initialized = true;

            // Register IConfigurationService singleton (created via DI later)
            Register<Infrastructure.Configuration.IConfigurationService, Infrastructure.Configuration.ConfigurationService>();
            // Register ILLMService
            Register<Modules.LLM.ILLMService, Modules.LLM.LLMService>();
        }
    }
}
