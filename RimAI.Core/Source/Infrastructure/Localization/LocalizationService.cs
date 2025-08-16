using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence;

namespace RimAI.Core.Source.Infrastructure.Localization
{
	internal sealed class LocalizationService : ILocalizationService
	{
		private readonly IPersistenceService _persistence;
		private readonly object _gate = new object();
		private readonly Dictionary<string, Dictionary<string, string>> _cache = new Dictionary<string, Dictionary<string, string>>();
		private string _defaultLocale = "zh-Hans";

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

		public string Get(string locale, string key, string fallback = "")
		{
			var dict = LoadLocale(locale);
			if (dict != null && key != null && dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)) return v;
			return fallback;
		}

		public string Format(string locale, string key, IDictionary<string, string> args, string fallback = "")
		{
			var tpl = Get(locale, key, fallback);
			if (string.IsNullOrEmpty(tpl)) return fallback ?? string.Empty;
			if (args == null || args.Count == 0) return tpl;
			foreach (var kv in args)
			{
				tpl = tpl.Replace("{" + kv.Key + "}", kv.Value ?? string.Empty);
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
				var path = $"Localization/Locales/{locale}.json";
				var json = _persistence.ReadTextUnderConfigOrNullAsync(path).GetAwaiter().GetResult();
				if (string.IsNullOrWhiteSpace(json))
				{
					lock (_gate) { _cache[locale] = new Dictionary<string, string>(); }
					return _cache[locale];
				}
				var map = JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
				lock (_gate) { _cache[locale] = map; }
				return map;
			}
			catch
			{
				lock (_gate) { _cache[locale] = new Dictionary<string, string>(); }
				return _cache[locale];
			}
		}
	}
}


