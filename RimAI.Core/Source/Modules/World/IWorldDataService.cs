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
		Task<float> GetBeautyAverageAsync(int centerX, int centerZ, int radius, CancellationToken ct = default);
		Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetTerrainCountsAsync(int centerX, int centerZ, int radius, CancellationToken ct = default);
		Task<ColonySnapshot> GetColonySnapshotAsync(int? pawnLoadId, CancellationToken ct = default);
		Task<WeatherStatus> GetWeatherStatusAsync(int pawnLoadId, CancellationToken ct = default);
		Task<string> GetCurrentJobLabelAsync(int pawnLoadId, CancellationToken ct = default);
		Task<System.Collections.Generic.IReadOnlyList<ApparelItem>> GetApparelAsync(int pawnLoadId, int maxApparel, CancellationToken ct = default);
		Task<NeedsSnapshot> GetNeedsAsync(int pawnLoadId, CancellationToken ct = default);
		Task<System.Collections.Generic.IReadOnlyList<ThoughtItem>> GetMoodThoughtOffsetsAsync(int pawnLoadId, int maxThoughts, CancellationToken ct = default);
		Task<float> GetPawnBeautyAverageAsync(int pawnLoadId, int radius, CancellationToken ct = default);
		Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetPawnTerrainCountsAsync(int pawnLoadId, int radius, CancellationToken ct = default);
	}
}


