using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.History.Relations
{
	internal interface IRelationsService
	{
		Task<RelationResult> ListSupersetsAsync(IReadOnlyList<string> participantIds, int page, int pageSize, CancellationToken ct = default);
		Task<RelationResult> ListSubsetsAsync(IReadOnlyList<string> participantIds, int page, int pageSize, CancellationToken ct = default);
		Task<IReadOnlyList<string>> ListByParticipantAsync(string participantId, CancellationToken ct = default);
	}
}


