using System;
using UnityEngine;
using Verse;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure;
using System.Linq;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.UI.Settings.Sections
{
	internal sealed class ImportantSettingsSection : RimAI.Core.Source.UI.Settings.ISettingsSection
	{
		private readonly IConfigurationService _config;
		private readonly ILocalizationService _loc;
		private readonly ConfigurationService _cfgInternal;

		private string _playerTitle;
		private bool _verboseLogs;
		private bool _toolingEnabled;
		private bool _dangerousConfirm;
		private int _llmTimeoutMs;
		private bool _overrideLocale;
		private string _overrideLocaleValue;
        private string _autoLocaleDisplay;
        // 新增：Tool Call 模式（默认 Classic）
        private RimAI.Core.Contracts.Config.ToolCallMode _toolCallMode = RimAI.Core.Contracts.Config.ToolCallMode.Classic;
        private bool _embeddingEnabled;

		public ImportantSettingsSection()
		{
			_config = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IConfigurationService>();
			_loc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<ILocalizationService>();
			_cfgInternal = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService;
			// 初始化本地可编辑副本
			try
			{
				_playerTitle = _cfgInternal?.GetPlayerTitleOrDefault() ?? "总督";
				_verboseLogs = _config?.Current?.VerboseLogs ?? false;
				_toolingEnabled = _cfgInternal?.GetToolingConfig()?.Enabled ?? true;
				_dangerousConfirm = _cfgInternal?.GetToolingConfig()?.DangerousToolConfirmation ?? false;
				_llmTimeoutMs = _cfgInternal?.GetLlmConfig()?.DefaultTimeoutMs ?? 15000;
				_overrideLocaleValue = _cfgInternal?.GetPromptLocaleOverrideOrNull();
                // 读取模式与 Embedding 开关
                try { _toolCallMode = (_config?.Current != null) ? _config.Current.ToolCallMode : RimAI.Core.Contracts.Config.ToolCallMode.Classic; } catch { _toolCallMode = RimAI.Core.Contracts.Config.ToolCallMode.Classic; }
                try { var llm = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.LLM.ILLMService>(); _embeddingEnabled = llm?.IsEmbeddingEnabled() ?? false; } catch { _embeddingEnabled = false; }
			}
			catch { }
		}

		public string Id => "important";
		public string GetTitle()
		{
			// 使用 XML Key：RimAI.Settings.Section.Important
			try { return "RimAI.Settings.Section.Important".Translate().ToString(); } catch { return "重要设置"; }
		}

		public float GetHeight(float width)
		{
			// 估算高度：标题 ~ 28f + 5 行控件 * 32f + 2 个按钮行 * 32f + padding
			return 28f + 5 * 34f + 2 * 34f + 24f;
		}

		public void Draw(Rect rect)
		{
			var listing = new Listing_Standard();
			listing.Begin(rect);

			// 提示词语言选择（自动跟随游戏语言，只读展示 + 按钮：同步/重置）
			var currentGameLang = LanguageDatabase.activeLanguage?.folderName ?? "English";
			_autoLocaleDisplay = currentGameLang;
			listing.Label("RimAI.Settings.Important.Locale.Auto".Translate(_autoLocaleDisplay).ToString());
			if (listing.ButtonText("RimAI.Settings.Important.Locale.Sync".Translate().ToString()))
			{
				// 设置默认本地化语言，未找到对应时回退英文
				var langFolder = LanguageDatabase.activeLanguage?.folderName ?? "English";
				var lang = langFolder;
				var picked = string.IsNullOrWhiteSpace(lang) ? "en" : lang;
				if (_loc != null) _loc.SetDefaultLocale(picked);
				// 清空覆盖，恢复自动
				_overrideLocale = false;
				_overrideLocaleValue = null;
				if (_cfgInternal != null) _cfgInternal.SetPromptLocaleOverride(null);
				Verse.Log.Message("[RimAI.Core][P1] settings.sync_locale -> " + (string.IsNullOrWhiteSpace(lang) ? "en" : lang));
			}
			// 单标签+两个按钮：手动下拉 + 同步游戏语言
			var avail = _loc?.GetAvailableLocales();
			var opts = avail == null ? new string[] { _loc?.GetDefaultLocale() ?? "en" } : avail.ToArray();
			if (listing.ButtonText("选择提示词语言（手动）"))
			{
				var menu = new System.Collections.Generic.List<FloatMenuOption>();
				foreach (var name in opts)
				{
					var pick = name;
					menu.Add(new FloatMenuOption(pick, () =>
					{
						_overrideLocale = true;
						_overrideLocaleValue = pick;
						if (_loc != null) _loc.SetDefaultLocale(pick); // 热加载
						// 固化到配置，后续启动时不再自动跟随
						if (_cfgInternal != null) _cfgInternal.SetPromptLocaleOverride(pick);
					}, MenuOptionPriority.Default, null, null, 0f, null, null));
				}
				Find.WindowStack.Add(new FloatMenu(menu));
			}

			// 玩家称谓
			_playerTitle = listing.TextEntryLabeled("RimAI.Settings.Important.PlayerTitle".Translate().ToString() + ": ", _playerTitle ?? "");

			// Verbose 日志
			listing.CheckboxLabeled("RimAI.Settings.Important.VerboseLogs".Translate().ToString(), ref _verboseLogs);

			// 工具系统
			listing.CheckboxLabeled("RimAI.Settings.Important.ToolingEnabled".Translate().ToString(), ref _toolingEnabled);
			listing.CheckboxLabeled("RimAI.Settings.Important.DangerousConfirm".Translate().ToString(), ref _dangerousConfirm);

			// LLM 默认超时
			var timeoutStr = _llmTimeoutMs.ToString();
			timeoutStr = listing.TextEntryLabeled("RimAI.Settings.Important.LlmTimeout".Translate().ToString(), timeoutStr);
			if (int.TryParse(timeoutStr, out var parsed)) _llmTimeoutMs = Math.Max(1000, parsed);

			// Tool Call 模式下拉
			listing.GapLine();
			var modeLabel = "RimAI.Settings.Important.ToolCallMode".Translate().ToString();
			var textClassic = "RimAI.Settings.Important.ToolCallMode.Option.Classic".Translate().ToString();
			var textTopK = "RimAI.Settings.Important.ToolCallMode.Option.TopK".Translate().ToString();
			var textTopKDisabled = "RimAI.Settings.Important.ToolCallMode.Option.TopK.Disabled".Translate().ToString();
			Widgets.Label(new Rect(rect.x, listing.CurHeight, rect.width, 24f), modeLabel);
			listing.Gap(26f);
			if (Widgets.ButtonText(new Rect(rect.x, listing.CurHeight, 220f, 28f), _toolCallMode == RimAI.Core.Contracts.Config.ToolCallMode.Classic ? textClassic : textTopK))
			{
				var menu = new System.Collections.Generic.List<FloatMenuOption>();
				menu.Add(new FloatMenuOption(textClassic, () => { _toolCallMode = RimAI.Core.Contracts.Config.ToolCallMode.Classic; }, MenuOptionPriority.Default));
				var topkLabel = _embeddingEnabled ? textTopK : textTopKDisabled;
				menu.Add(new FloatMenuOption(topkLabel, () => { if (_embeddingEnabled) _toolCallMode = RimAI.Core.Contracts.Config.ToolCallMode.TopK; }, MenuOptionPriority.Default));
				Find.WindowStack.Add(new FloatMenu(menu));
			}

			listing.GapLine();
			if (listing.ButtonText("RimAI.Settings.Actions.Apply".Translate().ToString()))
			{
				Apply();
			}
			if (listing.ButtonText("RimAI.Settings.Actions.Reset".Translate().ToString()))
			{
				ReloadFromConfig();
			}

			listing.End();
		}

		private void ReloadFromConfig()
		{
			try
			{
				_playerTitle = _cfgInternal?.GetPlayerTitleOrDefault() ?? "总督";
				_verboseLogs = _config?.Current?.VerboseLogs ?? false;
				_toolingEnabled = _cfgInternal?.GetToolingConfig()?.Enabled ?? true;
				_dangerousConfirm = _cfgInternal?.GetToolingConfig()?.DangerousToolConfirmation ?? false;
				_llmTimeoutMs = _cfgInternal?.GetLlmConfig()?.DefaultTimeoutMs ?? 15000;
			}
			catch { }
		}

		private void Apply()
		{
			try
			{
				_cfgInternal?.SetPlayerTitle(_playerTitle);
				var internalCfg = _cfgInternal != null ? _cfgInternal.GetInternal() : null;
				if (internalCfg != null && internalCfg.Diagnostics != null)
				{
					internalCfg.Diagnostics.VerboseLogs = _verboseLogs;
				}
				var tool = _cfgInternal?.GetToolingConfig();
				if (tool != null) { tool.Enabled = _toolingEnabled; tool.DangerousToolConfirmation = _dangerousConfirm; }
				var llm = _cfgInternal?.GetLlmConfig();
				if (llm != null) { llm.DefaultTimeoutMs = _llmTimeoutMs; }
				if (_overrideLocale && !string.IsNullOrWhiteSpace(_overrideLocaleValue))
				{
					var target = _overrideLocaleValue.Trim();
					if (_loc != null) _loc.SetDefaultLocale(target);
				}
				// 写入 Tool Call 模式（内部配置→快照）
				var tooling = _cfgInternal?.GetToolingConfig();
				if (tooling != null)
				{
					tooling.ToolCallMode = _toolCallMode;
				}

				// 若选择 TopK，保存时立即检查索引；若缺失则触发一次构建并持久化
				try
				{
					if (_toolCallMode == RimAI.Core.Contracts.Config.ToolCallMode.TopK && _embeddingEnabled)
					{
						var toolingSvc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>();
						_ = System.Threading.Tasks.Task.Run(async () => await toolingSvc.EnsureIndexReadyAsync(true));
					}
				}
				catch (System.Exception ex)
				{
					Verse.Log.Warning("[RimAI.Core][P4] ensure_index_on_save warn: " + ex.Message);
				}
				// 广播新快照
				if (_cfgInternal != null) _cfgInternal.Reload();
				Verse.Log.Message("[RimAI.Core][P1] settings.applied");
			}
			catch (Exception ex)
			{
				Verse.Log.Error("[RimAI.Core][P1] settings.apply.error: " + ex.Message);
			}
		}
	}
}


