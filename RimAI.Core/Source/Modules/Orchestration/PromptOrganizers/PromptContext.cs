using System.Collections.Generic;

namespace RimAI.Core.Modules.Orchestration.PromptOrganizers
{
    internal sealed class PromptContext
    {
        public RimAI.Core.Modules.Orchestration.PromptMode Mode { get; set; }
        public IReadOnlyList<string> ParticipantIds { get; set; }
        public string ConvKey { get; set; }
        public string CurrentSpeakerId { get; set; }
        public string PersonaName { get; set; }
        public string PawnId { get; set; }
        public string Locale { get; set; }
        public string ScenarioText { get; set; }
        public object ToolCallResults { get; set; }

        public PromptIncludeFlags IncludeFlags { get; set; }
        public Dictionary<string, int> SegmentBudgets { get; set; }
        public int? MaxPromptChars { get; set; }
    }

    [System.Flags]
    internal enum PromptIncludeFlags
    {
        None = 0,
        Persona = 1 << 0,
        Beliefs = 1 << 1,
        Recap = 1 << 2,
        History = 1 << 3,
        World = 1 << 4,
        StageHistory = 1 << 5,
        Tool = 1 << 6,
        Extras = 1 << 7,
        All = Persona | Beliefs | Recap | History | World | StageHistory | Tool | Extras
    }
}


