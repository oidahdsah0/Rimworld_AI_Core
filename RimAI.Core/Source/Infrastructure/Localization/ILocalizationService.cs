using System;
using System.Collections.Generic;

namespace RimAI.Core.Source.Infrastructure.Localization
{
	internal interface ILocalizationService
	{
		string Get(string locale, string key, string fallback = "");
		string Format(string locale, string key, IDictionary<string, string> args, string fallback = "");
		event Action<string> OnLocaleChanged;
		string GetDefaultLocale();
		void SetDefaultLocale(string locale);
		IEnumerable<string> GetAvailableLocales();
	}
}


