using System;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Contracts.Services;
using Verse;

namespace RimAI.Core.Services // Corrected namespace
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly RimAISettings _settings;
        public CoreConfig Current { get; private set; } // Corrected type and name
        public event Action<CoreConfig> OnConfigurationChanged;

        public ConfigurationService()
        {
            // Note: RimAISettings needs to be defined for this to work
            // _settings = LoadedModManager.GetMod<RimAIMod>().GetSettings<RimAISettings>();
            
            Log.Message("[RimAI.Core] Initializing ConfigurationService...");
            LoadConfig();
        }

        public void Reload()
        {
            Log.Message("[RimAI.Core] Configuration hot-reloading..."); // Corrected Log
            LoadConfig();
            OnConfigurationChanged?.Invoke(Current);
            Log.Message("[RimAI.Core] Configuration hot-reloaded and subscribers notified.");
        }

        private void LoadConfig()
        {
            // This part should read from _settings in the future.
            // For now, we use default values.
            Current = new CoreConfig
            {
                LLM = new LLMConfig
                {
                    // Temperature = _settings?.Temperature ?? 0.7,
                    // ApiKey = _settings?.ApiKey ?? string.Empty
                },
                Cache = new CacheConfig
                {
                    // CacheDurationMinutes = _settings?.CacheDurationMinutes ?? 5
                }
            };
            Log.Message("[RimAI.Core] Configuration loaded.");
            Log.Message($"[RimAI.Core] LLM Temperature: {Current.LLM.Temperature}");
            Log.Message($"[RimAI.Core] Cache Default Expiration: {Current.Cache.CacheDurationMinutes} minutes");
        }
    }
}