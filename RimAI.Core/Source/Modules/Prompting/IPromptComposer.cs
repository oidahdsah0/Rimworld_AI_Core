using System.Collections.Generic;

namespace RimAI.Core.Modules.Prompting
{
    internal sealed class PromptAuditSegment
    {
        public string LabelKey { get; set; }
        public int AddedChars { get; set; }
        public bool Truncated { get; set; }
    }

    internal sealed class PromptAudit
    {
        public List<PromptAuditSegment> Segments { get; set; } = new List<PromptAuditSegment>();
        public int TotalChars { get; set; }
    }

    internal interface IPromptComposer
    {
        void Begin(string templateKey, string locale);
        void Add(string labelKey, string material);
        void AddRange(string labelKey, IEnumerable<string> materials);
        string Build(int maxChars, out PromptAudit audit);
    }
}


