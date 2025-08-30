using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal interface IServerService
	{
		// 基础信息
		ServerRecord GetOrCreate(string entityId, int level);
		ServerRecord Get(string entityId);
		IReadOnlyList<ServerRecord> List();
		void SetBaseServerPersonaPreset(string entityId, string presetKey);
		void SetBaseServerPersonaOverride(string entityId, string overrideText);

		// 服务器人格槽位（按等级：Lv1=1 槽，Lv2=2 槽，Lv3=3 槽）
		void SetServerPersonaSlot(string entityId, int slotIndex, string presetKey, string overrideText = null);
		void ClearServerPersonaSlot(string entityId, int slotIndex);
		IReadOnlyList<ServerPersonaSlot> GetServerPersonaSlots(string entityId);

		// 巡检配置
		void SetInspectionIntervalHours(string entityId, int hours);
		void SetInspectionEnabled(string entityId, bool enabled);
		void AssignSlot(string entityId, int slotIndex, string toolName);
		void RemoveSlot(string entityId, int slotIndex);
		IReadOnlyList<InspectionSlot> GetSlots(string entityId);

		// 运行与调度
		Task RunInspectionOnceAsync(string entityId, CancellationToken ct = default);
		void StartAllSchedulers(CancellationToken appRootCt);
		void RestartScheduler(string entityId, int initialDelayTicks = 0);

		// 提示词与温度
		Task<ServerPromptPack> BuildPromptAsync(string entityId, string locale, CancellationToken ct = default);
		Task<float> GetRecommendedSamplingTemperatureAsync(string entityId, CancellationToken ct = default);

		// 持久化桥接（由 PersistenceManager 使用）
		ServerState ExportSnapshot();
		void ImportSnapshot(ServerState state);
	}

	internal sealed class ServerPromptPack
	{
		public IReadOnlyList<string> SystemLines { get; set; }
		public IReadOnlyList<ContextBlock> ContextBlocks { get; set; }
		public float? SamplingTemperature { get; set; }
	}
}


