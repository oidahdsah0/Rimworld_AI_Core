using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Infrastructure.Localization;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService : IServerService
	{
		private readonly ISchedulerService _scheduler;
		private readonly IWorldDataService _world;
		private readonly IToolRegistryService _tooling;
		private readonly IServerPromptPresetManager _presets;
		private readonly IHistoryService _history;
	private readonly ILLMService _llm;
	private readonly ILocalizationService _loc;

		// 巡检历史条目上限（每台服务器独立会话）
		private const int MaxInspectionHistoryEntries = 20;

		private readonly ConcurrentDictionary<string, ServerRecord> _servers = new ConcurrentDictionary<string, ServerRecord>(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, IDisposable> _periodics = new ConcurrentDictionary<string, IDisposable>(StringComparer.OrdinalIgnoreCase);

		public ServerService(ISchedulerService scheduler, IWorldDataService world, IToolRegistryService tooling, IServerPromptPresetManager presets, IHistoryService history, ILLMService llm, ILocalizationService loc)
		{
			_scheduler = scheduler;
			_world = world;
			_tooling = tooling;
			_presets = presets;
			_history = history;
			_llm = llm;
			_loc = loc;
		}

		public ServerRecord GetOrCreate(string entityId, int level)
		{
			if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentNullException(nameof(entityId));
			if (level < 1 || level > 3) throw new ArgumentOutOfRangeException(nameof(level));
			var result = _servers.AddOrUpdate(entityId,
				id => new ServerRecord
				{
					EntityId = id,
					Level = level,
					SerialHex12 = GenerateSerial(),
					BuiltAtAbsTicks = GetTicks(),
					InspectionIntervalHours = 24,
					InspectionEnabled = true, // 默认开启巡检以提升易用性
					InspectionSlots = new List<InspectionSlot>(),
					ServerPersonaSlots = new List<ServerPersonaSlot>()
				},
				(id, existing) =>
				{
					if (existing == null)
					{
						return new ServerRecord
						{
							EntityId = id,
							Level = level,
							SerialHex12 = GenerateSerial(),
							BuiltAtAbsTicks = GetTicks(),
							InspectionIntervalHours = 24,
							InspectionEnabled = true,
							InspectionSlots = new List<InspectionSlot>(),
							ServerPersonaSlots = new List<ServerPersonaSlot>()
						};
					}
					// 同步 Level 变更，并按新等级调整槽位容量
					if (existing.Level != level)
					{
						existing.Level = level;
						EnsureInspectionSlots(existing, GetInspectionCapacity(existing.Level));
						EnsureServerPersonaSlots(existing, GetPersonaCapacity(existing.Level));
					}
					return existing;
				});
			// 确保至少初始化一个槽位（按等级容量）
			try { EnsureInspectionSlots(result, GetInspectionCapacity(result.Level)); } catch { }
			return result;
		}

		public ServerRecord Get(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return null;
			_servers.TryGetValue(entityId, out var s);
			return s;
		}

		public IReadOnlyList<ServerRecord> List() => _servers.Values.OrderBy(s => s.EntityId).ToList();

 

		private ServerRecord GetOrThrow(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentNullException(nameof(entityId));
			if (_servers.TryGetValue(entityId, out var s) && s != null) return s;
			throw new KeyNotFoundException($"server not found: {entityId}");
		}

		private static string TrimToBudget(string s, int max)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			return s.Length <= max ? s : s.Substring(0, max);
		}


	}
}


