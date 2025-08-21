using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.World
{
	internal interface IWorldDataService
	{
		Task<string> GetPlayerNameAsync(CancellationToken ct = default);
        Task<int> GetCurrentDayNumberAsync(CancellationToken ct = default);
        Task<System.Collections.Generic.IReadOnlyList<int>> GetAllColonistLoadIdsAsync(CancellationToken ct = default);
		Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct = default);
		// 新增：返回通电的 AI 服务器（lv1/2/3，不含终端）的 thingID 列表
		Task<System.Collections.Generic.IReadOnlyList<int>> GetPoweredAiServerThingIdsAsync(CancellationToken ct = default);
		// 新增：返回指定服务器 thingId 的等级（1..3；未知返回1）
		Task<int> GetAiServerLevelAsync(int thingId, CancellationToken ct = default);
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
		Task<System.Collections.Generic.IReadOnlyList<GameLogItem>> GetRecentGameLogsAsync(int maxCount, CancellationToken ct = default);

		// P14：新增世界时间字符串获取（优先使用游戏内时间/经纬度；失败回退 UTC）
		Task<string> GetCurrentGameTimeStringAsync(CancellationToken ct = default);
	}
}


