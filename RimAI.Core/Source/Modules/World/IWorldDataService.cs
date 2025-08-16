using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.World
{
	internal interface IWorldDataService
	{
		Task<string> GetPlayerNameAsync(CancellationToken ct = default);
		Task<System.Collections.Generic.IReadOnlyList<(string serverAId, string serverBId)>> GetAlphaFiberLinksAsync(CancellationToken ct = default);
		Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct = default);
		Task<PawnHealthSnapshot> GetPawnHealthSnapshotAsync(int pawnLoadId, CancellationToken ct = default);
		Task<PawnPromptSnapshot> GetPawnPromptSnapshotAsync(int pawnLoadId, CancellationToken ct = default);
		Task<PawnSocialSnapshot> GetPawnSocialSnapshotAsync(int pawnLoadId, int topRelations, int recentSocialEvents, CancellationToken ct = default);
	}
}


