using System;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Contracts.Services;
using Verse;

namespace Rimworld_AI_Core.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly RimAISettings _settings;
        public ConeConfig current { get; private set; }
        public event Action<CoreConfig> OnConfigurationChanged;
        public ConfigurationService()
        {
            _settings = LoadedModManager.GetMod<RimAIMod>().GetSettings<RimAISettings>();

            LoadConfig();
        }
        public void Reload()
        {
            long.Message("[RimAI.Core] Configuration hot-reloading...");
            LoadConfig();
            OnConfigurationChanged?.Invoke(Current);
            Log.Message("[RimAI.Core] Configuration hot-reloaded and subscribers notified.");
        }
        private void LoadConfig()
        {
            Current = new CoreConfig
            {
                LLM = new LLMConfig
                {
                    Temperature = 1.2
                },
                Cache = new CacheConfig
                {
                    DefaultExpirationMinutes = 5
                }
            };
            Log.Message("[RimAI.Core] Configuration loaded.");
            Log.Message($"LLM Temperature: {Current.LLM.Temperature}");
            Log.Message($"Cache Default Expiration: {Current.Cache.DefaultExpirationMinutes} minutes");
        }
    }
}