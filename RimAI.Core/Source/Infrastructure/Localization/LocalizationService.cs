using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence;

namespace RimAI.Core.Source.Infrastructure.Localization
{
	internal sealed class LocalizationService : ILocalizationService
	{
		private readonly IPersistenceService _persistence;
		private readonly object _gate = new object();
		private readonly Dictionary<string, Dictionary<string, string>> _cache = new Dictionary<string, Dictionary<string, string>>();
		private readonly HashSet<string> _knownLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private string _defaultLocale = "en";

		public event Action<string> OnLocaleChanged;

		public LocalizationService(IPersistenceService persistence)
		{
			_persistence = persistence;
		}

		public string GetDefaultLocale() => _defaultLocale;
		public void SetDefaultLocale(string locale)
		{
			if (string.IsNullOrWhiteSpace(locale)) return;
			var normalized = NormalizeLocale(locale);
			if (string.Equals(_defaultLocale, normalized, StringComparison.OrdinalIgnoreCase)) return;
			_defaultLocale = normalized;
			try { OnLocaleChanged?.Invoke(normalized); } catch { }
		}

		public IEnumerable<string> GetAvailableLocales()
		{
			try
			{
				var user = _persistence.ListFilesUnderConfig("Localization/Locales", "*.json");
				var mod = _persistence.ListFilesUnderModRoot("Config/RimAI/Localization/Locales", "*.json");
				var names = user.Concat(mod)
					.Select(f => System.IO.Path.GetFileNameWithoutExtension(f))
					.Concat(_knownLocales)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (!names.Contains(_defaultLocale, StringComparer.OrdinalIgnoreCase)) names.Add(_defaultLocale);
				names.Sort(StringComparer.OrdinalIgnoreCase);
				return names;
			}
			catch
			{
				lock (_gate)
				{
					return _knownLocales.Count == 0 ? new [] { _defaultLocale } : _knownLocales.ToArray();
				}
			}
		}

		public string Get(string locale, string key, string fallback = "")
		{
			var dict = LoadLocale(locale);
			if (dict != null && key != null && dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
			// Fallback to English if current locale missing
			if (!string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
			{
				var en = LoadLocale("en");
				if (en != null && key != null && en.TryGetValue(key, out var v2) && !string.IsNullOrEmpty(v2)) return v2;
			}
			return fallback ?? string.Empty;
		}

		public string Format(string locale, string key, IDictionary<string, string> args, string fallback = "")
		{
			var tpl = Get(locale, key, fallback);
			if (string.IsNullOrEmpty(tpl)) return fallback ?? string.Empty;
			if (args == null || args.Count == 0) return tpl;
			foreach (var kv in args)
			{
				var placeholder = "{" + kv.Key + "}";
				var optionalLeading = "{" + kv.Key + ",optional,leadingComma}";
				if (tpl.Contains(optionalLeading))
				{
					var val = kv.Value ?? string.Empty;
					tpl = tpl.Replace(optionalLeading, string.IsNullOrWhiteSpace(val) ? string.Empty : ("," + val));
				}
				tpl = tpl.Replace(placeholder, kv.Value ?? string.Empty);
			}
			return tpl;
		}

		private Dictionary<string, string> LoadLocale(string locale)
		{
			locale = string.IsNullOrWhiteSpace(locale) ? _defaultLocale : locale;
			locale = NormalizeLocale(locale);
			lock (_gate)
			{
				if (_cache.TryGetValue(locale, out var dict)) return dict;
			}
			try
			{
				// 首选：用户配置根目录
				var path = $"Localization/Locales/{locale}.json";
				var json = _persistence.ReadTextUnderConfigOrNullAsync(path).GetAwaiter().GetResult();
				if (string.IsNullOrWhiteSpace(json))
				{
					// 回退：Mod 根目录随包内置（确保开箱可用）
					var json2 = _persistence.ReadTextUnderModRootOrNullAsync($"Config/RimAI/Localization/Locales/{locale}.json").GetAwaiter().GetResult();
					json = string.IsNullOrWhiteSpace(json2) ? null : json2;
				}
				// 若仍为空，尝试自动发现同名或兼容名（例如 pt-BR / pt_BR）
				if (string.IsNullOrWhiteSpace(json))
				{
					var userFiles = _persistence.ListFilesUnderConfig("Localization/Locales", "*.json");
					var modFiles = _persistence.ListFilesUnderModRoot("Config/RimAI/Localization/Locales", "*.json");
					var all = userFiles.Concat(modFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
					var found = all.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), locale, StringComparison.OrdinalIgnoreCase))
							?? all.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), locale.Replace('-', '_'), StringComparison.OrdinalIgnoreCase))
							?? all.FirstOrDefault(f => string.Equals(Path.GetFileNameWithoutExtension(f), locale.Replace('_', '-'), StringComparison.OrdinalIgnoreCase));
					if (!string.IsNullOrWhiteSpace(found))
					{
						var fileName = Path.GetFileName(found);
						var tryUser = _persistence.ReadTextUnderConfigOrNullAsync($"Localization/Locales/{fileName}").GetAwaiter().GetResult();
						json = string.IsNullOrWhiteSpace(tryUser) ? _persistence.ReadTextUnderModRootOrNullAsync($"Config/RimAI/Localization/Locales/{fileName}").GetAwaiter().GetResult() : tryUser;
					}
				}
				if (string.IsNullOrWhiteSpace(json))
				{
					lock (_gate) { _cache[locale] = new Dictionary<string, string>(); _knownLocales.Add(locale); }
					return _cache[locale];
				}
				var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
				lock (_gate) { _cache[locale] = map; _knownLocales.Add(locale); }
				return map;
			}
			catch
			{
				lock (_gate) { _cache[locale] = new Dictionary<string, string>(); _knownLocales.Add(locale); }
				return _cache[locale];
			}
		}

		private static string NormalizeLocale(string locale)
		{
			if (string.IsNullOrWhiteSpace(locale)) return "en";
			var s = locale.Trim();
			// 拆分括号，合并为“主体 + 括号内”并去除分隔符，便于匹配
			string basePart = s;
			string parenPart = string.Empty;
			int idx = s.IndexOf('(');
			if (idx >= 0)
			{
				basePart = s.Substring(0, idx);
				int idx2 = s.IndexOf(')', idx + 1);
				parenPart = idx2 > idx ? s.Substring(idx + 1, idx2 - idx - 1) : s.Substring(idx + 1);
			}
			var tokens = (basePart + " " + parenPart)
				.ToLowerInvariant()
				.Replace('_', ' ')
				.Replace('-', ' ')
				.Replace('.', ' ')
				.Trim();
			tokens = string.Join(" ", tokens.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));

			// 葡萄牙语（巴西）优先匹配
			if ((tokens.Contains("portuguese") || tokens.Contains("portugues") || tokens.Contains("português"))
				&& (tokens.Contains("brazil") || tokens.Contains("br"))) return "pt-BR";

			// 简体中文
			if (tokens.Contains("simplified") || tokens.Contains("hans") || tokens.Contains("简体") || tokens.Contains("zh cn") || tokens.Contains("zh hans")) return "zh-Hans";
			// 繁体中文
			if (tokens.Contains("traditional") || tokens.Contains("hant") || tokens.Contains("繁体") || tokens.Contains("zh tw") || tokens.Contains("zh hant")) return "zh-Hant";

			if (tokens.Contains("english") || tokens == "en") return "en";
			if (tokens.Contains("japanese") || tokens.Contains("日本") || tokens == "ja" || tokens.Contains(" ja ")) return "ja";
			if (tokens.Contains("korean") || tokens.Contains("한국") || tokens == "ko" || tokens.Contains(" ko ")) return "ko";
			if (tokens.Contains("french") || tokens.Contains("fran") || tokens == "fr" || tokens.Contains(" fr ")) return "fr";
			if (tokens.Contains("german") || tokens.Contains("deutsch") || tokens == "de" || tokens.Contains(" de ")) return "de";
			if (tokens.Contains("spanish") || tokens.Contains("español") || tokens.Contains("espanol") || tokens == "es" || tokens.Contains(" es ")) return "es";
			if (tokens.Contains("russian") || tokens.Contains("рус") || tokens == "ru" || tokens.Contains(" ru ")) return "ru";

			// 直接返回已是标准代码的
			if (s.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase) || s.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)) return s;
			if (s.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)) return s;
			if (s.Length <= 5 && (s.Contains("-") || s.Length == 2)) return s.ToLowerInvariant();

			// 默认回退：不变返回
			return s;
		}
	}
}


