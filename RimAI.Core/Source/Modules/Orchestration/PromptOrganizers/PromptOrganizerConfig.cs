using System.Collections.Generic;

namespace RimAI.Core.Modules.Orchestration.PromptOrganizers
{
    internal sealed class PromptOrganizerConfig
    {
        public HashSet<string> EnabledSegments { get; set; } = new HashSet<string>();
        public Dictionary<string, int> Priorities { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Budgets { get; set; } = new Dictionary<string, int>();
        public int? MaxPromptChars { get; set; }
        public List<string> WorldSnapshotFields { get; set; } = new List<string>();
        public List<string> ToolResultWhitelist { get; set; } = new List<string>();
    }
}


