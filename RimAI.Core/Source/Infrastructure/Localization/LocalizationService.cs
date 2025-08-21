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
			_defaultLocale = locale;
			try { OnLocaleChanged?.Invoke(locale); } catch { }
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
						var tryUser = _persistence.ReadTextUnderConfigOrNullAsync($"Localization/Locales/{found}").GetAwaiter().GetResult();
						json = string.IsNullOrWhiteSpace(tryUser) ? _persistence.ReadTextUnderModRootOrNullAsync($"Config/RimAI/Localization/Locales/{found}").GetAwaiter().GetResult() : tryUser;
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
	}
}


