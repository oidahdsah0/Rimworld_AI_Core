using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;

namespace RimAI.Core.Modules.Prompting
{
    internal sealed class PromptTemplateService : IPromptTemplateService
    {
        private readonly IConfigurationService _config;
        private PromptTemplate _cached;
        private string _cachedLocale;
        private DateTime _lastLoadUtc;
        private string _lastMasterPath;
        private string _lastUserPath;
        private DateTime _lastMasterWrite;
        private DateTime _lastUserWrite;

        public PromptTemplateService(IConfigurationService config)
        {
            _config = config;
        }

        public string ResolveLocale()
        {
            try
            {
                var p = _config?.Current?.Prompt ?? new PromptConfig();
                if (p.UseGameLanguage)
                {
                    // 读取 RimWorld 当前语言（安全回退）
                    var lang = Verse.LanguageDatabase.activeLanguage?.folderName ?? p.Locale ?? "zh-Hans";
                    return NormalizeLocale(lang);
                }
                return NormalizeLocale(p.Locale ?? "zh-Hans");
            }
            catch { return "zh-Hans"; }
        }

        public PromptTemplate Get(string desiredLocale = null)
        {
            var locale = NormalizeLocale(desiredLocale ?? ResolveLocale());
            var p = _config?.Current?.Prompt ?? new PromptConfig();
            string masterPath = (p.MasterPath ?? "Resources/prompts/{locale}.json").Replace("{locale}", locale);
            string userPath = (p.UserOverridePath ?? "Config/RimAI/Prompts/{locale}.user.json").Replace("{locale}", locale);

            bool needReload = true;
            if (_cached != null && string.Equals(_cachedLocale, locale, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var masterWrite = File.Exists(masterPath) ? File.GetLastWriteTimeUtc(masterPath) : DateTime.MinValue;
                    var userWrite = File.Exists(userPath) ? File.GetLastWriteTimeUtc(userPath) : DateTime.MinValue;
                    needReload = masterPath != _lastMasterPath || userPath != _lastUserPath || masterWrite != _lastMasterWrite || userWrite != _lastUserWrite;
                }
                catch { needReload = true; }
            }
            if (!needReload)
                return _cached;

            p = _config?.Current?.Prompt ?? new PromptConfig();
            var master = LoadJson(masterPath) ?? new PromptTemplate { Locale = locale };
            var user = LoadJson(userPath) ?? new PromptTemplate { Locale = locale };

            var merged = Merge(master, user);
            _cached = merged; _cachedLocale = locale; _lastLoadUtc = DateTime.UtcNow;
            _lastMasterPath = masterPath; _lastUserPath = userPath;
            try
            {
                _lastMasterWrite = File.Exists(masterPath) ? File.GetLastWriteTimeUtc(masterPath) : DateTime.MinValue;
                _lastUserWrite = File.Exists(userPath) ? File.GetLastWriteTimeUtc(userPath) : DateTime.MinValue;
            }
            catch { _lastMasterWrite = DateTime.MinValue; _lastUserWrite = DateTime.MinValue; }
            return merged;
        }

        private static PromptTemplate LoadJson(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return null;
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<PromptTemplate>(json);
            }
            catch (Exception ex)
            {
                CoreServices.Logger.Warn($"[PromptTemplate] load failed: {ex.Message}");
                return null;
            }
        }

        private static PromptTemplate Merge(PromptTemplate master, PromptTemplate user)
        {
            var pt = new PromptTemplate
            {
                Version = Math.Max(master?.Version ?? 1, user?.Version ?? 1),
                Locale = user?.Locale ?? master?.Locale ?? "zh-Hans",
                Templates = new Dictionary<string, List<string>>(),
                Labels = new Dictionary<string, string>()
            };

            // templates: 以 user 覆盖 master
            if (master?.Templates != null)
            {
                foreach (var kv in master.Templates)
                    pt.Templates[kv.Key] = kv.Value?.ToList() ?? new List<string>();
            }
            if (user?.Templates != null)
            {
                foreach (var kv in user.Templates)
                    pt.Templates[kv.Key] = kv.Value?.ToList() ?? new List<string>();
            }

            // labels: 以 user 覆盖 master
            if (master?.Labels != null)
            {
                foreach (var kv in master.Labels)
                    pt.Labels[kv.Key] = kv.Value ?? string.Empty;
            }
            if (user?.Labels != null)
            {
                foreach (var kv in user.Labels)
                    pt.Labels[kv.Key] = kv.Value ?? string.Empty;
            }

            return pt;
        }

        private static string NormalizeLocale(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale)) return "zh-Hans";
            // 将 RimWorld 语言文件夹名简易映射为 BCP-47，常见如 ChineseSimplified → zh-Hans
            if (string.Equals(locale, "ChineseSimplified", StringComparison.OrdinalIgnoreCase)) return "zh-Hans";
            if (string.Equals(locale, "ChineseTraditional", StringComparison.OrdinalIgnoreCase)) return "zh-Hant";
            return locale;
        }
    }
}


