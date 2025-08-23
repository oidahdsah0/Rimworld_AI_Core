using UnityEngine;
using Verse;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class TitleSettingsTabView
	{
		private string _input;

		public void Draw(Rect inRect, ILocalizationService loc, IConfigurationService cfg, System.Action<string> onSaved)
		{
			string current = null;
			try
			{
				var cfgInt = cfg as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var locale = cfgInt?.GetInternal()?.General?.Locale ?? "en";
				current = cfgInt?.GetPlayerTitleOrDefault();
				if (string.IsNullOrWhiteSpace(current))
				{
					// 仅初始化时，通过本地化获取默认，并立即写回配置（内存变量），以后始终从配置读
					var fallback = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
					cfgInt?.SetPlayerTitle(fallback);
					current = fallback;
				}
			}
			catch { current = "governor"; }

			Text.Font = GameFont.Medium; Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "RimAI.ChatUI.TitleSettings.Question".Translate()); Text.Font = GameFont.Small;
			var box = new Rect(inRect.x, inRect.y + 36f, Mathf.Min(260f, inRect.width - 20f), 28f);
			_input = Widgets.TextField(box, _input ?? current ?? string.Empty);
			var btnY = box.yMax + 8f;
			if (Widgets.ButtonText(new Rect(inRect.x, btnY, 90f, 26f), "RimAI.Common.Save".Translate()))
			{
				try { var cfgInt = cfg as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; if (string.IsNullOrWhiteSpace(_input)) cfgInt?.SetPlayerTitle(null); else cfgInt?.SetPlayerTitle(_input.Trim()); onSaved?.Invoke(_input); } catch { }
			}
			if (Widgets.ButtonText(new Rect(inRect.x + 100f, btnY, 90f, 26f), "RimAI.Common.Reset".Translate()))
			{
				try { var cfgInt = cfg as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; cfgInt?.SetPlayerTitle(null); var locale = cfgInt?.GetInternal()?.General?.Locale ?? "en"; var fallback = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor"; _input = fallback; onSaved?.Invoke(_input); } catch { }
			}
		}
	}
}


