#nullable enable
using RimAI.Core.Settings;
using Verse;

namespace RimAI.Core.Infrastructure.Configuration
{
    /// <summary>
    /// Loads RimAI settings from RimWorld ModSettings (stubbed for P1 – uses defaults).
    /// Supports hot-reload via OnConfigurationChanged.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private CoreConfig _current = new();
        public CoreConfig Current => _current;

        public event System.Action<CoreConfig>? OnConfigurationChanged;

        public void Reload()
        {
            // TODO: integrate with RimAIFrameworkSettings when available.
            _current = new CoreConfig(); // For now, just reset to defaults
            OnConfigurationChanged?.Invoke(_current);
            Log.Message($"[RimAI.Core] Configuration reloaded. Temperature={_current.LLM.Temperature}");
        }
    }
}
