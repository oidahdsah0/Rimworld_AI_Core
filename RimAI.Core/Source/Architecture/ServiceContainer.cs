using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Services;
using RimAI.Core.Officers;
using RimAI.Core.Analysis;
using RimAI.Core.Architecture.Interfaces;
using Verse;
using RimAI.Core.Settings;
using System.Text;
using RimWorld;
using RimAI.Core.Prompts;

namespace RimAI.Core.Architecture
{
    public static class CoreServices
    {
        public static Governor Governor => ServiceContainer.Instance?.GetService<Governor>();
        public static IColonyAnalyzer Analyzer => ServiceContainer.Instance?.GetService<IColonyAnalyzer>();
        public static ILLMService LLMService => ServiceContainer.Instance?.GetService<ILLMService>();
        public static ICacheService CacheService => ServiceContainer.Instance?.GetService<ICacheService>();
        public static IEventBus EventBus => ServiceContainer.Instance?.GetService<IEventBus>();
        public static IPersistenceService PersistenceService => ServiceContainer.Instance?.GetService<IPersistenceService>();
        public static ISafeAccessService SafeAccessService => ServiceContainer.Instance?.GetService<ISafeAccessService>();
        public static IHistoryService History => ServiceContainer.Instance?.GetService<IHistoryService>();
        public static IPromptFactoryService PromptFactory => ServiceContainer.Instance?.GetService<IPromptFactoryService>();
        public static IPromptBuilder PromptBuilder => ServiceContainer.Instance?.GetService<IPromptBuilder>();
        public static string PlayerStableId => Faction.OfPlayer.GetUniqueLoadID();
        public static string PlayerDisplayName => SettingsManager.Settings.Player.Nickname;

        public static bool AreServicesReady()
        {
            return Governor != null && Analyzer != null && LLMService != null && EventBus != null &&
                   CacheService != null && PersistenceService != null && SafeAccessService != null &&
                   History != null && PromptFactory != null && PromptBuilder != null;
        }

        public static string GetServiceStatusReport()
        {
            if (ServiceContainer.Instance == null)
            {
                return "--- RimAI Service Status ---\nService container not initialized.";
            }

            var report = new StringBuilder("--- RimAI Service Status ---\n");
            var services = new Dictionary<string, bool>
            {
                { "Governor", Governor != null },
                { "Analyzer", Analyzer != null },
                { "LLM", LLMService != null },
                { "EventBus", EventBus != null },
                { "Cache", CacheService != null },
                { "Persistence", PersistenceService != null },
                { "SafeAccess", SafeAccessService != null },
                { "History", History != null },
                { "PromptFactory", PromptFactory != null },
                { "PromptBuilder", PromptBuilder != null }
            };

            foreach (var service in services)
            {
                report.AppendLine($"{service.Key}: {(service.Value ? "✅ Ready" : "❌ Not Ready")}");
            }
            
            report.AppendLine("--------------------------");
            report.AppendLine(AreServicesReady() ? "Status: All systems nominal." : "Status: One or more services are not ready.");

            return report.ToString();
        }
    }

    public class ServiceContainer
    {
        private static ServiceContainer _instance;
        public static ServiceContainer Instance => _instance;

        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        private ServiceContainer() { }

        public static void Initialize()
        {
            _instance = new ServiceContainer();
            _instance.RegisterDefaultServices();
            Log.Message("[RimAI.Core] ServiceContainer initialized and all services registered.");
        }

        public T GetService<T>() where T : class
        {
            _services.TryGetValue(typeof(T), out var service);
            return service as T;
        }

        public Dictionary<Type, object> GetRegisteredServices()
        {
            return new Dictionary<Type, object>(_services);
        }

        private void RegisterService<T>(T service) where T : class
        {
            _services[typeof(T)] = service;
            if (typeof(T) != service.GetType())
            {
                _services[service.GetType()] = service;
            }
        }

        private void RegisterService<TInterface, TImplementation>(TImplementation service) where TImplementation : class, TInterface
        {
            _services[typeof(TInterface)] = service;
            _services[typeof(TImplementation)] = service;
        }

        private void RegisterDefaultServices()
        {
            RegisterService<ICacheService, CacheService>(new CacheService());
            RegisterService<IEventBus, EventBusService>(new EventBusService());
            RegisterService<ILLMService, LLMService>(new LLMService());
            RegisterService<IPersistenceService, PersistenceService>(new PersistenceService());
            RegisterService<IHistoryService, HistoryService>(new HistoryService());
            RegisterService<IPromptFactoryService, PromptFactoryService>(new PromptFactoryService());
            RegisterService<IColonyAnalyzer, ColonyAnalyzer>(new ColonyAnalyzer());
            RegisterService<ISafeAccessService, SafeAccessService>(new SafeAccessService());
            RegisterService<IAIOfficer, Governor>(new Governor());
            RegisterService<IPromptBuilder, PromptBuilder>(new PromptBuilder());
        }
    }
}
