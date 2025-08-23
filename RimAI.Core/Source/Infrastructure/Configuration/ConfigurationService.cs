using System;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Localization;

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
            // 优先使用包含 ToolCallMode 的构造，确保对外可见。
            return new CoreConfigSnapshot(
                version: cfg.Version,
                locale: cfg.General.Locale,
                debugPanelEnabled: cfg.UI.DebugPanelEnabled,
                verboseLogs: cfg.Diagnostics.VerboseLogs,
                toolCallMode: (cfg?.Tooling != null) ? cfg.Tooling.ToolCallMode : RimAI.Core.Contracts.Config.ToolCallMode.Classic
            );
        }

        // Helper: get current player title for UI (internal consumers may cast and read directly)
        // Best practice: if not set, return null so callers can localize fallback per current locale
        public string GetPlayerTitleOrDefault()
        {
            var t = _current?.UI?.ChatWindow?.PlayerTitle;
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }

        // Helper: set and broadcast change (persisting via ModSettings deferred; P6 config file persistence via IPersistenceService)
        public void SetPlayerTitle(string title)
        {
            var t = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
            if (_current?.UI?.ChatWindow != null)
            {
                _current.UI.ChatWindow.PlayerTitle = t;
                var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
            }
        }

        // Called by PersistenceService after reading disk configuration
        public void ApplyFullConfig(CoreConfig cfg)
        {
            if (cfg == null) return;
            _current = cfg;
            var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
        }

		public void SetVerboseLogs(bool enabled)
		{
			if (_current?.Diagnostics != null)
			{
				_current.Diagnostics.VerboseLogs = enabled;
				var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
			}
		}

		public void SetToolingEnabled(bool enabled)
		{
			if (_current?.Tooling != null)
			{
				_current.Tooling.Enabled = enabled;
				var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
			}
		}

		public void SetToolingDangerousConfirm(bool enabled)
		{
			if (_current?.Tooling != null)
			{
				_current.Tooling.DangerousToolConfirmation = enabled;
				var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
			}
		}

		public void SetLlmDefaultTimeoutMs(int ms)
		{
			if (_current?.LLM != null)
			{
				_current.LLM.DefaultTimeoutMs = ms;
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

		// Prompt locale override helpers
		public string GetPromptLocaleOverrideOrNull() => _current?.General?.PromptLocaleOverride;
		public void SetPromptLocaleOverride(string localeOrNull)
		{
			if (_current?.General == null) return;
			_current.General.PromptLocaleOverride = string.IsNullOrWhiteSpace(localeOrNull) ? null : localeOrNull.Trim();
			var handler = OnConfigurationChanged; if (handler != null) handler.Invoke(MapToSnapshot(_current));
		}

		// Ensure UI player title is initialized from localization once, then always read from config memory
		internal void EnsurePlayerTitleInitialized(ILocalizationService loc)
		{
			if (string.IsNullOrWhiteSpace(GetPlayerTitleOrDefault()))
			{
				try
				{
					var locale = _current?.General?.Locale ?? "en";
					var fallback = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
					SetPlayerTitle(fallback);
				}
				catch { }
			}
		}
    }
}


