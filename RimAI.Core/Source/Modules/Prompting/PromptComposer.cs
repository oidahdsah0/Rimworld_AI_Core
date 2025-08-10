using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimAI.Core.Modules.Prompting
{
    internal sealed class PromptComposer : IPromptComposer
    {
        private readonly IPromptTemplateService _templateService;
        private string _templateKey;
        private string _locale;
        private readonly List<(string LabelKey, string Text)> _segments = new();

        public PromptComposer(IPromptTemplateService templateService)
        {
            _templateService = templateService;
        }

        public void Begin(string templateKey, string locale)
        {
            _templateKey = templateKey ?? "chat";
            _locale = locale ?? _templateService.ResolveLocale();
            _segments.Clear();
        }

        public void Add(string labelKey, string material)
        {
            if (string.IsNullOrWhiteSpace(material)) return;
            _segments.Add((labelKey ?? string.Empty, material));
        }

        public void AddRange(string labelKey, IEnumerable<string> materials)
        {
            if (materials == null) return;
            foreach (var m in materials)
                Add(labelKey, m);
        }

        public string Build(int maxChars, out PromptAudit audit)
        {
            var tmpl = _templateService.Get(_locale);
            var order = (tmpl?.Templates != null && tmpl.Templates.TryGetValue(_templateKey, out var seq)) ? seq : new List<string>();
            var labelMap = tmpl?.Labels ?? new Dictionary<string, string>();

            var grouped = _segments.GroupBy(s => s.LabelKey).ToDictionary(g => g.Key, g => g.Select(x => x.Text).ToList());

            var sb = new StringBuilder(1024);
            var aud = new PromptAudit();

            foreach (var key in order)
            {
                if (!grouped.TryGetValue(key, out var list) || list.Count == 0) continue;
                var header = labelMap.TryGetValue(key, out var label) ? label : null;
                var before = sb.Length;
                bool headerHasPlaceholder = !string.IsNullOrWhiteSpace(header) && header.IndexOf("{text}", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!string.IsNullOrWhiteSpace(header) && !headerHasPlaceholder)
                {
                    sb.AppendLine(header);
                }
                if (headerHasPlaceholder)
                {
                    var text = list.FirstOrDefault() ?? string.Empty;
                    var line = header.Replace("{text}", text);
                    sb.AppendLine(line);
                }
                else
                {
                    foreach (var t in list)
                    {
                        if (!string.IsNullOrWhiteSpace(t)) sb.AppendLine(t.Trim());
                        if (sb.Length > maxChars) break;
                    }
                }
                var added = sb.Length - before;
                bool truncated = sb.Length > maxChars;
                aud.Segments.Add(new PromptAuditSegment { LabelKey = key, AddedChars = Math.Max(0, added), Truncated = truncated });
                if (sb.Length > maxChars)
                {
                    sb.Length = maxChars;
                    break;
                }
                sb.AppendLine();
            }

            aud.TotalChars = sb.Length;
            audit = aud;
            return sb.ToString();
        }
    }
}


