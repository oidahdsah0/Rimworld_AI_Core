using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Stage.Scan
{
    internal sealed class ConversationSuggestion
    {
        public IReadOnlyList<string> Participants { get; set; }
        public string Origin { get; set; } = "PawnBehavior";
        public string InitiatorId { get; set; } = string.Empty;
        public string Scenario { get; set; } = string.Empty;
        public int? Priority { get; set; }
        public int? Seed { get; set; }
    }

    internal sealed class ScanContext
    {
        public RimAI.Core.Settings.StageConfig Config { get; set; }
        public RimAI.Core.Modules.World.IParticipantIdService ParticipantId { get; set; }
    }

    internal interface IStageScan
    {
        Task<IReadOnlyList<ConversationSuggestion>> RunAsync(ScanContext ctx, CancellationToken ct = default);
    }
}


