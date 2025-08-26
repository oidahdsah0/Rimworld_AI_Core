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

		// Colony assistant extras
		Task<FoodInventorySnapshot> GetFoodInventoryAsync(CancellationToken ct = default);
		Task<MedicineInventorySnapshot> GetMedicineInventoryAsync(CancellationToken ct = default);
		Task<ThreatSnapshot> GetThreatSnapshotAsync(CancellationToken ct = default);
		Task<PowerStatusSnapshot> GetPowerStatusAsync(CancellationToken ct = default);

		// New: world resource overview (counts from ResourceCounter + simple daily-use estimates)
		Task<ResourceOverviewSnapshot> GetResourceOverviewAsync(CancellationToken ct = default);

		// New: weather analysis snapshot (time + weather + temp trend + advisories)
		Task<WeatherAnalysisSnapshot> GetWeatherAnalysisAsync(CancellationToken ct = default);

		// New: storage saturation snapshot (per stockpile/container usage)
		Task<StorageSaturationSnapshot> GetStorageSaturationAsync(CancellationToken ct = default);

		// New: research options listing (current/available/locked + colony capability)
		Task<ResearchOptionsSnapshot> GetResearchOptionsAsync(CancellationToken ct = default);

		// New: construction backlog (blueprints/frames and missing materials)
		Task<ConstructionBacklogSnapshot> GetConstructionBacklogAsync(CancellationToken ct = default);

		// New: security posture (turrets, traps, coverage and gaps)
		Task<SecurityPostureSnapshot> GetSecurityPostureAsync(CancellationToken ct = default);

		// New: mood risk overview (distribution and top negative causes)
		Task<MoodRiskSnapshot> GetMoodRiskOverviewAsync(CancellationToken ct = default);

		// New: medical overview (colony health check)
		Task<MedicalOverviewSnapshot> GetMedicalOverviewAsync(CancellationToken ct = default);

		// New: wildlife opportunities (group by species)
		Task<WildlifeOpportunitiesSnapshot> GetWildlifeOpportunitiesAsync(CancellationToken ct = default);

		// New: trade readiness (silver, beacons/comms, goods)
		Task<TradeReadinessSnapshot> GetTradeReadinessAsync(CancellationToken ct = default);

		// New: animal management snapshot
		Task<AnimalManagementSnapshot> GetAnimalManagementAsync(CancellationToken ct = default);

		// New: prison overview (count, recruitables, risks)
		Task<PrisonOverviewSnapshot> GetPrisonOverviewAsync(CancellationToken ct = default);

		// New: alert digest (aggregate & sort)
		Task<AlertDigestSnapshot> GetAlertDigestAsync(CancellationToken ct = default);

		// New: raid readiness (wealth/threat points/size estimates)
		Task<RaidReadinessSnapshot> GetRaidReadinessAsync(CancellationToken ct = default);
	}
}


