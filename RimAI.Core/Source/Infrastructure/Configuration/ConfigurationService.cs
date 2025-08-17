using System;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Config;

namespace RimAI.Core.Source.Infrastructure.Configuration
{
    /// <summary>
    /// Core implementation of IConfigurationService.
    /// Reads RimWorld ModSettings (placeholder: defaults for P1), maps to immutable snapshot, supports Reload event.
    /// </summary>
    public sealed class ConfigurationService : IConfigurationService
    {
        private CoreConfig _current;

        public ConfigurationService()
        {
            _current = LoadFromModSettingsOrDefaults();
        }

        public CoreConfigSnapshot Current => MapToSnapshot(_current);

        public event Action<CoreConfigSnapshot> OnConfigurationChanged;

        public void Reload()
        {
            _current = LoadFromModSettingsOrDefaults();
            var handler = OnConfigurationChanged;
            if (handler != null)
            {
                handler.Invoke(MapToSnapshot(_current));
            }
        }

        private static CoreConfig LoadFromModSettingsOrDefaults()
        {
            // P1: Use defaults. P2/P6 may integrate with actual RimWorld ModSettings/Scribe adapter.
            return new CoreConfig();
        }

        private static CoreConfigSnapshot MapToSnapshot(CoreConfig cfg)
        {
            return new CoreConfigSnapshot(
                version: cfg.Version,
                locale: cfg.General.Locale,
                debugPanelEnabled: cfg.UI.DebugPanelEnabled,
                verboseLogs: cfg.Diagnostics.VerboseLogs
            );
        }

        // Helper: get current player title for UI (internal consumers may cast and read directly)
        public string GetPlayerTitleOrDefault() => _current?.UI?.ChatWindow?.PlayerTitle ?? "总督";

        // Helper: set and broadcast change (persisting via ModSettings deferred; P6 config file persistence via IPersistenceService)
        public void SetPlayerTitle(string title)
        {
            var t = string.IsNullOrWhiteSpace(title) ? "总督" : title.Trim();
            if (_current?.UI?.ChatWindow != null)
            {
                _current.UI.ChatWindow.PlayerTitle = t;
                var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
            }
        }

        public string GetSnapshotJsonPretty()
        {
            return JsonConvert.SerializeObject(Current, Formatting.Indented);
        }

        // Helpers for UI without referencing contracts at compile-time elsewhere
        public string GetVersion() => _current.Version;
        public bool IsDebugPanelEnabled() => _current.UI.DebugPanelEnabled;

		// Internal accessors (Core-only consumers may cast to ConfigurationService)
		public CoreConfig.LlmSection GetLlmConfig() => _current.LLM;
		public CoreConfig.SchedulerSection GetSchedulerConfig() => _current.Scheduler;
		public CoreConfig.WorldDataSection GetWorldDataConfig() => _current.WorldData;
		public CoreConfig.ToolingSection GetToolingConfig() => _current.Tooling;
		public CoreConfig GetInternal() => _current;
    }
}


