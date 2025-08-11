using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Stage.Acts
{
    internal sealed class ActContext
    {
        public string ConvKey { get; set; }
        public IReadOnlyList<string> Participants { get; set; }
        public int Seed { get; set; }
        public string Locale { get; set; }
        public RimAI.Core.Settings.StageConfig Options { get; set; }
        public RimAI.Core.Modules.Persona.IPersonaConversationService Persona { get; set; }
        public RimAI.Core.Services.IHistoryWriteService History { get; set; }
        public RimAI.Core.Modules.World.IParticipantIdService ParticipantId { get; set; }
        public RimAI.Core.Contracts.Eventing.IEventBus Events { get; set; }
    }

    internal sealed class ActResult
    {
        public bool Completed { get; set; }
        public string Reason { get; set; }
        public int Rounds { get; set; }
    }

    internal interface IStageAct
    {
        string Name { get; }
        bool IsEligible(ActContext ctx);
        Task<ActResult> RunAsync(ActContext ctx, CancellationToken ct = default);
    }
}


