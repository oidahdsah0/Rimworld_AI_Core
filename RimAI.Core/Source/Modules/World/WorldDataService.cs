/*
 * WorldDataService (WDS) — V5 入口与总组装（Orchestrator / Facade）
 *
 * 角色与约束：
 * - 作为对外唯一入口，聚合并转发世界数据查询；自身不承载具体业务实现。
 * - 全部具体逻辑下沉至 World/Parts/*（单一职责的 Part 类）；WDS 仅委托调用。
 * - 任何 Verse/RimWorld API 访问必须经由 ISchedulerService 在游戏主线程执行，并受配置的超时保护。
 * - 公共方法保持“薄”：表达式体或最小样板；每个方法一行注释描述用途；不得引入状态、缓存或跨线程对象。
 * - 新增功能流程：先新增 Part，再在 WDS 暴露对应委托方法，保持 API 稳定与一致性。
 *
 * 维护提示：如发现实现细节滑入本类，请及时下沉到合适的 Part；本类应始终保持可读、可审计的路由清单。
 */

using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.World
{
	internal sealed class WorldDataService : IWorldDataService
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;
		// Parts
		private readonly Parts.FoodInventoryPart _foodPart;
		private readonly Parts.MedicineInventoryPart _medPart;
		private readonly Parts.ThreatScanPart _threatPart;
		private readonly Parts.ColonyPart _colonyPart;
		private readonly Parts.AIServerPart _aiServerPart;
		private readonly Parts.PawnHealthPart _pawnHealthPart;
		private readonly Parts.PawnIdentityPart _pawnIdentityPart;
		private readonly Parts.PawnSocialPart _pawnSocialPart;
		private readonly Parts.PawnStatusPart _pawnStatusPart;
		private readonly Parts.EnvironmentPart _envPart;
		private readonly Parts.MetaPart _metaPart;
		private readonly Parts.WeatherPart _weatherPart;
	private readonly Parts.ResourceOverviewPart _resourcePart;
	private readonly Parts.PowerStatusPart _powerPart;

		public WorldDataService(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("WorldDataService requires ConfigurationService");
			_foodPart = new Parts.FoodInventoryPart(_scheduler, _cfg);
			_medPart = new Parts.MedicineInventoryPart(_scheduler, _cfg);
			_threatPart = new Parts.ThreatScanPart(_scheduler, _cfg);
			_colonyPart = new Parts.ColonyPart(_scheduler, _cfg);
			_aiServerPart = new Parts.AIServerPart(_scheduler, _cfg);
			_pawnHealthPart = new Parts.PawnHealthPart(_scheduler, _cfg);
			_pawnIdentityPart = new Parts.PawnIdentityPart(_scheduler, _cfg);
			_pawnSocialPart = new Parts.PawnSocialPart(_scheduler, _cfg);
			_pawnStatusPart = new Parts.PawnStatusPart(_scheduler, _cfg);
			_envPart = new Parts.EnvironmentPart(_scheduler, _cfg);
			_metaPart = new Parts.MetaPart(_scheduler, _cfg);
			_weatherPart = new Parts.WeatherPart(_scheduler, _cfg);
			_resourcePart = new Parts.ResourceOverviewPart(_scheduler, _cfg);
			_powerPart = new Parts.PowerStatusPart(_scheduler, _cfg);
		}

		// 获取玩家名称（派系/殖民地拥有者）
		public Task<string> GetPlayerNameAsync(CancellationToken ct = default) => _colonyPart.GetPlayerNameAsync(ct);

		// 获取当前绝对天数（按 60k tick/天）
		public Task<int> GetCurrentDayNumberAsync(CancellationToken ct = default) => _colonyPart.GetCurrentDayNumberAsync(ct);

		// 获取所有在世自由殖民者的 thingIDNumber 列表
		public Task<System.Collections.Generic.IReadOnlyList<int>> GetAllColonistLoadIdsAsync(CancellationToken ct = default) => _colonyPart.GetAllColonistLoadIdsAsync(ct);

		// 获取通电的 AI 服务器（Lv1-3，不含终端）的 thingID 列表
		public Task<System.Collections.Generic.IReadOnlyList<int>> GetPoweredAiServerThingIdsAsync(CancellationToken ct = default) => _aiServerPart.GetPoweredAiServerThingIdsAsync(ct);

		// 获取指定 AI 服务器的状态快照
		public Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct = default) => _aiServerPart.GetAiServerSnapshotAsync(serverId, ct);

		// 获取指定 AI 服务器的等级
		public Task<int> GetAiServerLevelAsync(int thingId, CancellationToken ct = default) => _aiServerPart.GetAiServerLevelAsync(thingId, ct);

		// 获取殖民者健康状态快照（生命、伤口、疾病等）
		public Task<PawnHealthSnapshot> GetPawnHealthSnapshotAsync(int pawnLoadId, CancellationToken ct = default) => _pawnHealthPart.GetPawnHealthSnapshotAsync(pawnLoadId, ct);

		// 获取殖民者身份摘要（姓名/头衔/年龄/性别等）
		public Task<PawnPromptSnapshot> GetPawnPromptSnapshotAsync(int pawnLoadId, CancellationToken ct = default) => _pawnIdentityPart.GetPawnPromptSnapshotAsync(pawnLoadId, ct);

		// 获取殖民者社交摘要（关系与近期社交事件）
		public Task<PawnSocialSnapshot> GetPawnSocialSnapshotAsync(int pawnLoadId, int topRelations, int recentSocialEvents, CancellationToken ct = default) => _pawnSocialPart.GetPawnSocialSnapshotAsync(pawnLoadId, topRelations, recentSocialEvents, ct);

		// removed GetPawnEnvironmentMatrixAsync: replaced by split APIs below

		// 计算以殖民者为中心的美观均值（指定半径）
		public Task<float> GetPawnBeautyAverageAsync(int pawnLoadId, int radius, CancellationToken ct = default) => _envPart.GetPawnBeautyAverageAsync(pawnLoadId, radius, ct);

		// 统计以殖民者为中心的地形分布（指定半径）
		public Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetPawnTerrainCountsAsync(int pawnLoadId, int radius, CancellationToken ct = default) => _envPart.GetPawnTerrainCountsAsync(pawnLoadId, radius, ct);

		// 获取最近的游戏日志条目
		public Task<System.Collections.Generic.IReadOnlyList<GameLogItem>> GetRecentGameLogsAsync(int maxCount, CancellationToken ct = default) => _metaPart.GetRecentGameLogsAsync(maxCount, ct);

		// 获取当前游戏时间字符串
		public Task<string> GetCurrentGameTimeStringAsync(CancellationToken ct = default) => _metaPart.GetCurrentGameTimeStringAsync(ct);

		// 计算指定坐标为中心的美观均值（半径）
		public Task<float> GetBeautyAverageAsync(int centerX, int centerZ, int radius, CancellationToken ct = default) => _envPart.GetBeautyAverageAsync(centerX, centerZ, radius, ct);

		// 统计指定坐标为中心的地形分布（半径）
		public Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetTerrainCountsAsync(int centerX, int centerZ, int radius, CancellationToken ct = default) => _envPart.GetTerrainCountsAsync(centerX, centerZ, radius, ct);

		// 获取殖民地概况快照（名称、人数、清单等）
		public Task<ColonySnapshot> GetColonySnapshotAsync(int? pawnLoadId, CancellationToken ct = default) => _colonyPart.GetColonySnapshotAsync(pawnLoadId, ct);


		// 获取殖民者所在位置的天气状态
		public Task<WeatherStatus> GetWeatherStatusAsync(int pawnLoadId, CancellationToken ct = default) => _weatherPart.GetWeatherStatusAsync(pawnLoadId, ct);

		// 获取殖民者当前工作的标签/描述
		public Task<string> GetCurrentJobLabelAsync(int pawnLoadId, CancellationToken ct = default) => _pawnStatusPart.GetCurrentJobLabelAsync(pawnLoadId, ct);

		// 获取殖民者所穿戴的服装清单（限制数量）
		public Task<System.Collections.Generic.IReadOnlyList<ApparelItem>> GetApparelAsync(int pawnLoadId, int maxApparel, CancellationToken ct = default) => _pawnStatusPart.GetApparelAsync(pawnLoadId, maxApparel, ct);

		// 获取殖民者需求快照（饥饿、休息等）
		public Task<NeedsSnapshot> GetNeedsAsync(int pawnLoadId, CancellationToken ct = default) => _pawnStatusPart.GetNeedsAsync(pawnLoadId, ct);

		// 获取殖民者情绪思潮及加成/减益（限制数量）
		public Task<System.Collections.Generic.IReadOnlyList<ThoughtItem>> GetMoodThoughtOffsetsAsync(int pawnLoadId, int maxThoughts, CancellationToken ct = default) => _pawnStatusPart.GetMoodThoughtOffsetsAsync(pawnLoadId, maxThoughts, ct);

		// 获取食物库存快照（营养、保质等）
		public Task<FoodInventorySnapshot> GetFoodInventoryAsync(CancellationToken ct = default) => _foodPart.GetAsync(ct);

		// 获取药品库存快照（效能、数量等）
		public Task<MedicineInventorySnapshot> GetMedicineInventoryAsync(CancellationToken ct = default) => _medPart.GetAsync(ct);

		// 获取威胁概况（敌对数量、威胁点、火灾/危险等级等）
		public Task<ThreatSnapshot> GetThreatSnapshotAsync(CancellationToken ct = default) => _threatPart.GetAsync(ct);


		// 获取资源概览（CountAsResource 的物品清单 + 日耗与天数估算）
		public Task<ResourceOverviewSnapshot> GetResourceOverviewAsync(CancellationToken ct = default) => _resourcePart.GetAsync(ct);

		// 获取电力概览（总发电/用电/净值 + 电池存量与天数）
		public Task<PowerStatusSnapshot> GetPowerStatusAsync(CancellationToken ct = default) => _powerPart.GetAsync(ct);

	}

	internal sealed class WorldDataException : Exception
	{
		public WorldDataException(string message) : base(message) { }
		public WorldDataException(string message, Exception inner) : base(message, inner) { }
	}
}


