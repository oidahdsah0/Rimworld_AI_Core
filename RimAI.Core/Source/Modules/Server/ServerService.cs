using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Infrastructure.Localization;
using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed class ServerService : IServerService
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

		public void SetBaseServerPersonaPreset(string entityId, string presetKey)
		{
			var s = GetOrThrow(entityId);
			s.BaseServerPersonaPresetKey = presetKey;
		}

		public void SetBaseServerPersonaOverride(string entityId, string overrideText)
		{
			var s = GetOrThrow(entityId);
			s.BaseServerPersonaOverride = overrideText;
		}

		public void SetServerPersonaSlot(string entityId, int slotIndex, string presetKey, string overrideText = null)
		{
			var s = GetOrThrow(entityId);
			var cap = GetPersonaCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureServerPersonaSlots(s, cap);
			s.ServerPersonaSlots[slotIndex] = new ServerPersonaSlot { Index = slotIndex, PresetKey = presetKey, OverrideText = overrideText, Enabled = true };
		}

		public void ClearServerPersonaSlot(string entityId, int slotIndex)
		{
			var s = GetOrThrow(entityId);
			var cap = GetPersonaCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureServerPersonaSlots(s, cap);
			s.ServerPersonaSlots[slotIndex] = new ServerPersonaSlot { Index = slotIndex, PresetKey = null, OverrideText = null, Enabled = false };
		}

		public IReadOnlyList<ServerPersonaSlot> GetServerPersonaSlots(string entityId)
		{
			var s = GetOrThrow(entityId);
			EnsureServerPersonaSlots(s, GetPersonaCapacity(s.Level));
			return s.ServerPersonaSlots.OrderBy(x => x.Index).ToList();
		}

		public void SetInspectionIntervalHours(string entityId, int hours)
		{
			var s = GetOrThrow(entityId);
			s.InspectionIntervalHours = Math.Max(6, hours);
			// 重启调度：首轮先经历完整冷却
			RestartScheduler(entityId, initialDelayTicks: s.InspectionIntervalHours * 2500);
		}

		public void SetInspectionEnabled(string entityId, bool enabled)
		{
			var s = GetOrThrow(entityId);
			s.InspectionEnabled = enabled;
			RestartScheduler(entityId);
		}

		public void AssignSlot(string entityId, int slotIndex, string toolName)
		{
			var s = GetOrThrow(entityId);
			var cap = GetInspectionCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureInspectionSlots(s, cap);
			// 取消全局唯一限制：允许不同服务器加载相同工具；权限校验仍在列表阶段
			s.InspectionSlots[slotIndex] = new InspectionSlot { Index = slotIndex, ToolName = toolName, Enabled = true };
			// 若之前未注册周期任务且巡检启用，则启动定时器
			try { if (s.InspectionEnabled && !_periodics.ContainsKey(entityId)) StartOneScheduler(entityId, s.InspectionIntervalHours, CancellationToken.None, initialDelayTicks: 0); } catch { }
		}

		public void RemoveSlot(string entityId, int slotIndex)
		{
			var s = GetOrThrow(entityId);
			var cap = GetInspectionCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureInspectionSlots(s, cap);
			s.InspectionSlots[slotIndex] = new InspectionSlot { Index = slotIndex, ToolName = null, Enabled = false };
		}

		public IReadOnlyList<InspectionSlot> GetSlots(string entityId)
		{
			var s = GetOrThrow(entityId);
			EnsureInspectionSlots(s, GetInspectionCapacity(s.Level));
			return s.InspectionSlots.OrderBy(x => x.Index).ToList();
		}

		public async Task RunInspectionOnceAsync(string entityId, CancellationToken ct = default)
		{
			var s = GetOrThrow(entityId);
			// 若实体已在世界被摧毁/移除，则进行自清理并返回
			try
			{
				int? id = TryParseThingId(entityId);
				if (id.HasValue)
				{
					bool exists = await _world.ExistsAiServerAsync(id.Value, ct).ConfigureAwait(false);
					if (!exists)
					{
						await RemoveAsync(entityId, clearInspectionHistory: true, ct: ct).ConfigureAwait(false);
						return;
					}
				}
			}
			catch { }
			// 全局关闭则直接返回
			try { var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService>(); if (cfg != null && !cfg.GetServerConfig().GlobalInspectionEnabled) return; } catch { }
			if (!(s.InspectionEnabled)) return;
			// 巡检时排除空槽位与无工具名的槽位
			var enabled = (s.InspectionSlots ?? new List<InspectionSlot>()).Where(x => x != null && x.Enabled && !string.IsNullOrWhiteSpace(x.ToolName)).OrderBy(x => x.Index).ToList();
			if (enabled.Count == 0)
			{
				// 限流“未配置工具”提示：12 小时内仅记一次，避免刷屏
				try
				{
					int now = GetTicks();
					int gap = 12 * 2500; // 12 小时
					var convKey = BuildServerInspectionConvKey(entityId);
					bool threadEmpty = false;
					try { var all = await _history.GetAllEntriesRawAsync(convKey, ct).ConfigureAwait(false); threadEmpty = (all == null || all.Count == 0); } catch { threadEmpty = false; }
					if (threadEmpty || !s.LastNoToolsNoticeAtAbsTicks.HasValue || now - s.LastNoToolsNoticeAtAbsTicks.Value >= gap)
					{
						try { await _history.UpsertParticipantsAsync(convKey, new List<string> { "agent:server_inspection", TryMakeInspectionParticipant(entityId) }, ct).ConfigureAwait(false); } catch { }
						var msg = _loc?.Get(_loc?.GetDefaultLocale() ?? "en", "RimAI.Server.Inspection.NoTools", "No tools configured for inspection.") ?? "No tools configured for inspection.";
						string gameTime = null; try { gameTime = await _world.GetCurrentGameTimeStringAsync(ct).ConfigureAwait(false); } catch { gameTime = null; }
						var sn = string.IsNullOrWhiteSpace(s.SerialHex12) ? "SN-UNKNOWN" : ($"SN-{s.SerialHex12}");
						var prefix = string.IsNullOrWhiteSpace(gameTime) ? ($"[{sn}] ") : ($"[{sn}] {gameTime} ");
						await _history.AppendRecordAsync(convKey, "P13.Server", entityId, "log", prefix + msg, advanceTurn: false, ct: ct).ConfigureAwait(false);
						// 写入后修剪会话历史至上限
						try { await PruneInspectionHistoryIfNeededAsync(convKey, ct).ConfigureAwait(false); } catch { }
						s.LastNoToolsNoticeAtAbsTicks = now;
					}
				}
				catch { }
				return;
			}
			// pick one slot by rotation pointer
			if (s.NextSlotPointer < 0) s.NextSlotPointer = 0;
			if (s.NextSlotPointer >= enabled.Count) s.NextSlotPointer = 0;
			var chosen = enabled[s.NextSlotPointer];
			s.NextSlotPointer = (s.NextSlotPointer + 1) % Math.Max(1, enabled.Count);

			object toolResult = null; string toolName = chosen.ToolName;
			try
			{
				var args = new Dictionary<string, object>();
				try { args["server_level"] = Math.Max(1, Math.Min(3, s.Level)); } catch { args["server_level"] = 1; }
				try { args["inspection"] = true; } catch { }
				toolResult = await _tooling.ExecuteToolAsync(toolName, args, ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException) { throw; }
			catch (Exception ex)
			{
				toolResult = new { error = ex.GetType().Name, message = ex.Message };
			}

			// Build prompt via PromptService(ServerInspection) and compose a non-stream summary
			string summary = null;
			try
			{
				var locale = _loc?.GetDefaultLocale() ?? "en";
				// 使用每台服务器独立的巡检会话键，避免混入群聊线程
				var conv = BuildServerInspectionConvKey(entityId);
				var external = new List<RimAI.Core.Source.Modules.Prompting.Models.ContextBlock>();
				try
				{
					string jsonText;
					try
					{
						if (toolResult is string strVal)
						{
							var st = strVal?.Trim() ?? string.Empty;
							// 若已是 JSON 形态，直接使用；否则包装为 JSON 字符串
							if ((st.StartsWith("{") && st.EndsWith("}")) || (st.StartsWith("[") && st.EndsWith("]"))) jsonText = st;
							else jsonText = JsonConvert.SerializeObject(new { value = strVal });
						}
						else
						{
							jsonText = JsonConvert.SerializeObject(toolResult);
						}
					}
					catch { jsonText = toolResult?.ToString() ?? string.Empty; }
					var dispName = _tooling?.GetToolDisplayNameOrNull(toolName) ?? toolName ?? "tool";
					external.Add(new RimAI.Core.Source.Modules.Prompting.Models.ContextBlock
					{
						// 标题携带工具名，方便 UserComposer 提取 app 名称
						Title = dispName,
						Text = TrimToBudget(jsonText, 1800)
					});
				}
				catch { }
				var promptReq = new RimAI.Core.Source.Modules.Prompting.Models.PromptBuildRequest
				{
					Scope = RimAI.Core.Source.Modules.Prompting.Models.PromptScope.ServerInspection,
					ConvKey = conv,
					// 与巡检会话键保持一致的参与者集合
					ParticipantIds = new List<string> { "agent:server_inspection", TryMakeInspectionParticipant(entityId) },
					PawnLoadId = null,
					IsCommand = false,
					Locale = locale,
					UserInput = null,
					ExternalBlocks = external
				};
				var prompt = await RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>().BuildAsync(promptReq, ct).ConfigureAwait(false);
				var sys = prompt?.SystemPrompt ?? string.Empty;
				var user = prompt?.UserPrefixedInput ?? string.Empty;
				// 将上下文块（含工具 JSON）拼接到 user 文本，避免非 UI 路径丢失 RAG 内容
				try
				{
					// 仅使用外部块（工具 JSON）以避免与作曲器返回的复制块造成重复
					var blocks = external;
					if (blocks != null && blocks.Count > 0)
					{
						var sb = new StringBuilder();
						for (int i = 0; i < Math.Min(3, blocks.Count); i++)
						{
							var b = blocks[i]; if (b == null) continue;
							if (!string.IsNullOrWhiteSpace(b.Title)) sb.AppendLine(b.Title);
							if (!string.IsNullOrWhiteSpace(b.Text)) sb.AppendLine(TrimToBudget(b.Text, 1800));
							sb.AppendLine();
						}
						user = string.IsNullOrWhiteSpace(user) ? sb.ToString() : (user + "\n\n" + sb.ToString());
					}
				}
				catch { }
				if (string.IsNullOrWhiteSpace(user)) user = $"tool={toolName}"; // fallback
				var messages = new List<RimAI.Framework.Contracts.ChatMessage>
				{
					new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=sys },
					new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=user }
				};
				// 为了避免框架缓存命中导致“无请求/延迟返回”，会话ID追加一次性 run 标识；历史仍按稳定的 convKey 写入
				var runConv = conv + "|run:" + GetTicks().ToString(System.Globalization.CultureInfo.InvariantCulture);
				var resp = await _llm.GetResponseAsync(new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = runConv, Messages = messages, Stream = false }, ct).ConfigureAwait(false);
				summary = resp.IsSuccess ? (resp.Value?.Message?.Content ?? string.Empty) : null;
			}
			catch { summary = null; }

			// 先生成 fallbackJson，基于“之前的摘要状态”进行去重判断并写历史，再更新快照
			string fallbackJson;
			try
			{
				if (toolResult is string strVal)
				{
					var st = strVal?.Trim() ?? string.Empty;
					fallbackJson = ((st.StartsWith("{") && st.EndsWith("}")) || (st.StartsWith("[") && st.EndsWith("]"))) ? st : JsonConvert.SerializeObject(new { value = strVal });
				}
				else fallbackJson = JsonConvert.SerializeObject(toolResult);
			}
			catch { fallbackJson = toolResult?.ToString() ?? string.Empty; }

			var prevSummary = s.LastSummaryText;
			var prevAt = s.LastSummaryAtAbsTicks;

			// Append to inspection history (per-server convKey)，并去重相邻重复（基于旧状态）
			try
			{
				var convKey = BuildServerInspectionConvKey(entityId);
				// 写入参与者元数据，便于 UI 侧通过 participants 推导出 server id
				try { await _history.UpsertParticipantsAsync(convKey, new List<string> { "agent:server_inspection", TryMakeInspectionParticipant(entityId) }, ct).ConfigureAwait(false); } catch { }
				var content = string.IsNullOrWhiteSpace(summary) ? fallbackJson : summary;
				var core = TrimToBudget(content, 1600);
				int now = GetTicks();
				bool isDuplicate = false;
				try { isDuplicate = (!string.IsNullOrWhiteSpace(prevSummary) && string.Equals(TrimToBudget(prevSummary, 1600), core, StringComparison.Ordinal)) && prevAt.HasValue && (now - prevAt.Value) <= 600; } catch { isDuplicate = false; }
				if (!isDuplicate)
				{
					string gameTime = null; try { gameTime = await _world.GetCurrentGameTimeStringAsync(ct).ConfigureAwait(false); } catch { gameTime = null; }
					var sn = string.IsNullOrWhiteSpace(s.SerialHex12) ? "SN-UNKNOWN" : ($"SN-{s.SerialHex12}");
					var prefix = string.IsNullOrWhiteSpace(gameTime) ? ($"[{sn}] ") : ($"[{sn}] {gameTime} ");
					await _history.AppendRecordAsync(convKey, "P13.Server", entityId, "log", prefix + core, advanceTurn: false, ct: ct).ConfigureAwait(false);
					// 写入后修剪会话历史至上限
					try { await PruneInspectionHistoryIfNeededAsync(convKey, ct).ConfigureAwait(false); } catch { }
				}
			}
			catch { }

			// Update snapshot & next due（最后再更新）
			// 仅将未加前缀的核心文本写入快照用于去重
			s.LastSummaryText = TrimToBudget(summary ?? fallbackJson, 1600);
			s.LastSummaryAtAbsTicks = GetTicks();
			var next = GetTicks() + Math.Max(6, s.InspectionIntervalHours) * 2500;
			foreach (var slot in s.InspectionSlots)
			{
				if (slot == null) continue;
				slot.LastRunAbsTicks = GetTicks();
				slot.NextDueAbsTicks = next;
			}
		}

		public void StartAllSchedulers(CancellationToken appRootCt)
		{
			foreach (var s in _servers.Values.ToList())
			{
				if (s?.InspectionEnabled == true)
				{
					// 启动时不强制初始冷却，保持原有体验
					StartOneScheduler(s.EntityId, s.InspectionIntervalHours, appRootCt, initialDelayTicks: 0);
				}
			}
		}

		public void RestartScheduler(string entityId, int initialDelayTicks = 0)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			if (_periodics.TryRemove(entityId, out var d)) { try { d.Dispose(); } catch { } }
			var rec = GetOrThrow(entityId);
			if (rec.InspectionEnabled)
			{
				StartOneScheduler(entityId, rec.InspectionIntervalHours, CancellationToken.None, initialDelayTicks);
			}
		}

		public async Task RemoveAsync(string entityId, bool clearInspectionHistory = true, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			// 1) 停止周期巡检任务
			if (_periodics.TryRemove(entityId, out var disp)) { try { disp.Dispose(); } catch { } }
			// 2) 构造巡检会话键
			var convKey = BuildServerInspectionConvKey(entityId);
			// 3) 清空槽位并移除记录
			if (_servers.TryRemove(entityId, out var s) && s != null)
			{
				try { s.InspectionSlots?.Clear(); } catch { }
				try { s.ServerPersonaSlots?.Clear(); } catch { }
			}
			// 4) 可选：清空巡检会话历史，避免残留继续触发 UI
			if (clearInspectionHistory)
			{
				try { await _history.ClearThreadAsync(convKey, ct).ConfigureAwait(false); } catch { }
			}

			// 5) 额外清理：删除该服务器与玩家的所有 1v1 聊天线程（包含 server:<id>/thing:<id> + player:*）
			try
			{
				int? id = TryParseThingId(entityId);
				if (id.HasValue)
				{
					var serverPid1 = $"server:{id.Value}";
					var serverPid2 = $"thing:{id.Value}";
					var allKeys = _history.GetAllConvKeys() ?? new List<string>();
					foreach (var ck in allKeys)
					{
						if (string.IsNullOrWhiteSpace(ck)) continue;
						var parts = _history.GetParticipantsOrEmpty(ck) ?? Array.Empty<string>();
						bool hasServer = false, hasPlayer = false;
						try
						{
							foreach (var p in parts)
							{
								if (string.IsNullOrWhiteSpace(p)) continue;
								if (!hasServer && (string.Equals(p, serverPid1, StringComparison.Ordinal) || string.Equals(p, serverPid2, StringComparison.Ordinal))) hasServer = true;
								if (!hasPlayer && p.StartsWith("player:", StringComparison.Ordinal)) hasPlayer = true;
								if (hasServer && hasPlayer) break;
							}
							// 参与者列表可能为空（某些历史未注册参与者），回退到从 convKey 文本解析
							if (!(hasServer && hasPlayer))
							{
								var tokens = ck.Split('|');
								foreach (var t in tokens)
								{
									var x = t?.Trim(); if (string.IsNullOrEmpty(x)) continue;
									if (!hasServer && (string.Equals(x, serverPid1, StringComparison.Ordinal) || string.Equals(x, serverPid2, StringComparison.Ordinal))) hasServer = true;
									if (!hasPlayer && x.StartsWith("player:", StringComparison.Ordinal)) hasPlayer = true;
									if (hasServer && hasPlayer) break;
								}
							}
						}
						catch { }
						if (hasServer && hasPlayer)
						{
							try { await _history.ClearThreadAsync(ck, ct).ConfigureAwait(false); } catch { }
						}
					}
				}
			}
			catch { }
		}

		public async Task<ServerPromptPack> BuildPromptAsync(string entityId, string locale, CancellationToken ct = default)
		{
			var s = GetOrThrow(entityId);
			var preset = await _presets.GetAsync(locale, ct).ConfigureAwait(false);
			var systemLines = new List<string>();
			// 服务器人格
			var personaLines = BuildServerPersonaLines(s, preset);
			if (personaLines.Count > 0) systemLines.AddRange(personaLines);
			// 环境变体
			var tempC = (await _world.GetAiServerSnapshotAsync(entityId, ct).ConfigureAwait(false))?.TemperatureC ?? 37;
			if (tempC < 30) { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_low)) systemLines.Add(preset.Env.temp_low); }
			else if (tempC < 70) { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_mid)) systemLines.Add(preset.Env.temp_mid); }
			else { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_high)) systemLines.Add(preset.Env.temp_high); }
			// Server 基本属性
			systemLines.Add($"Server Level={s.Level}, Serial={s.SerialHex12}, BuiltAt={FormatGameTime(s.BuiltAtAbsTicks)}, Interval={s.InspectionIntervalHours}h");
			// ContextBlocks：最近一次汇总
			var blocks = new List<RimAI.Core.Source.Modules.Prompting.Models.ContextBlock>();
			if (!string.IsNullOrWhiteSpace(s.LastSummaryText))
			{
				blocks.Add(new RimAI.Core.Source.Modules.Prompting.Models.ContextBlock { Title = "最近一次巡检摘要", Text = s.LastSummaryText });
			}
			var temp = await GetRecommendedSamplingTemperatureAsync(entityId, ct).ConfigureAwait(false);
			return new ServerPromptPack { SystemLines = systemLines, ContextBlocks = blocks, SamplingTemperature = temp };
		}

		public async Task<float> GetRecommendedSamplingTemperatureAsync(string entityId, CancellationToken ct = default)
		{
			try
			{
				var s = await _world.GetAiServerSnapshotAsync(entityId, ct).ConfigureAwait(false);
				int t = s?.TemperatureC ?? 37;
				if (t < 30) return RandRange(0.9f, 1.2f);
				if (t < 70) return RandRange(1.2f, 1.5f);
				return 2.0f;
			}
			catch { return 1.2f; }
		}

		public ServerState ExportSnapshot()
		{
			var state = new ServerState();
			foreach (var kv in _servers)
			{
				state.Items[kv.Key] = Clone(kv.Value);
			}
			return state;
		}

		public void ImportSnapshot(ServerState state)
		{
			_servers.Clear();
			if (state?.Items == null) return;
			foreach (var kv in state.Items)
			{
				var rec = kv.Value ?? new ServerRecord { EntityId = kv.Key, Level = 1 };
				rec.InspectionIntervalHours = Math.Max(6, rec.InspectionIntervalHours <= 0 ? 24 : rec.InspectionIntervalHours);
				_servers[kv.Key] = Clone(rec);
			}
		}

		private void StartOneScheduler(string entityId, int hours, CancellationToken appRootCt, int initialDelayTicks = 0)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			if (_periodics.ContainsKey(entityId)) return; // 幂等保护：避免重复注册
			// 全局关闭则不注册周期任务
			try { var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService>(); if (cfg != null && !cfg.GetServerConfig().GlobalInspectionEnabled) return; } catch { }
			// 若当前服务器没有任何工具已配置，跳过注册，等首次分配工具时再启动
			try
			{
				var s = Get(entityId);
				if (s != null)
				{
					var anyTool = (s.InspectionSlots ?? new List<InspectionSlot>()).Any(x => x != null && x.Enabled && !string.IsNullOrWhiteSpace(x.ToolName));
					if (!anyTool) return;
				}
			}
			catch { }
			int everyTicks = Math.Max(6, hours) * 2500;
			var name = $"server:{entityId}:inspection";
			try
			{
				var disp = _scheduler.SchedulePeriodic(name, everyTicks, async ct =>
				{
					try { await RunInspectionOnceAsync(entityId, ct).ConfigureAwait(false); }
					catch (OperationCanceledException) { }
					catch (Exception ex) { Verse.Log.Error($"[RimAI.Core][P13.Server] periodic failed: {ex.Message}"); }
				}, appRootCt, initialDelayTicks);
				_periodics[entityId] = disp;
			}
			catch { }
		}

		private static string BuildServerHubConvKey()
		{
			try
			{
				var list = new List<string> { "agent:server_hub", "player:servers" };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_hub|player:servers"; }
		}

		// 巡检专属对话键：每台服务器独立线程
		// 规范：convKey = join('|', sort({ "agent:server_inspection", "server_inspection:<thingId>" }))
		private static string BuildServerInspectionConvKey(string entityId)
		{
			try
			{
				int? id = TryParseThingId(entityId);
				var p1 = "agent:server_inspection";
				var p2 = id.HasValue ? ($"server_inspection:{id.Value}") : ($"server_inspection:{(entityId ?? "unknown")}");
				var list = new List<string> { p1, p2 };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_inspection|server_inspection:unknown"; }
		}

		private static string TryMakeInspectionParticipant(string entityId)
		{
			try
			{
				int? id = TryParseThingId(entityId);
				return id.HasValue ? ($"server_inspection:{id.Value}") : ($"server_inspection:{(entityId ?? "unknown")}");
			}
			catch { return "server_inspection:unknown"; }
		}

		private static int? TryParseThingId(string entityId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(entityId)) return null;
				var s = entityId.Trim();
				if (int.TryParse(s, out var pure)) return pure;
				var lastIdx = s.LastIndexOf(':');
				if (lastIdx >= 0 && lastIdx + 1 < s.Length)
				{
					var tail = s.Substring(lastIdx + 1);
					if (int.TryParse(tail, out var id2)) return id2;
				}
				int end = s.Length - 1;
				while (end >= 0 && !char.IsDigit(s[end])) end--;
				if (end < 0) return null;
				int start = end;
				while (start >= 0 && char.IsDigit(s[start])) start--;
				start++;
				if (start <= end)
				{
					var numStr = s.Substring(start, end - start + 1);
					if (int.TryParse(numStr, out var id3)) return id3;
				}
			}
			catch { }
			return null;
		}

		private static int GetPersonaCapacity(int level) => level switch { 1 => 1, 2 => 2, _ => 3 };
		private static int GetInspectionCapacity(int level) => level switch { 1 => 1, 2 => 3, _ => 5 };

		private static void EnsureServerPersonaSlots(ServerRecord s, int cap)
		{
			if (s.ServerPersonaSlots == null) s.ServerPersonaSlots = new List<ServerPersonaSlot>();
			while (s.ServerPersonaSlots.Count < cap) s.ServerPersonaSlots.Add(new ServerPersonaSlot { Index = s.ServerPersonaSlots.Count, Enabled = false });
			if (s.ServerPersonaSlots.Count > cap) s.ServerPersonaSlots = s.ServerPersonaSlots.Take(cap).ToList();
		}

		private static void EnsureInspectionSlots(ServerRecord s, int cap)
		{
			if (s.InspectionSlots == null) s.InspectionSlots = new List<InspectionSlot>();
			while (s.InspectionSlots.Count < cap) s.InspectionSlots.Add(new InspectionSlot { Index = s.InspectionSlots.Count, Enabled = false });
			if (s.InspectionSlots.Count > cap) s.InspectionSlots = s.InspectionSlots.Take(cap).ToList();
		}

		private static string GenerateSerial()
		{
			var rnd = new Random();
			var sb = new StringBuilder(12);
			for (int i = 0; i < 12; i++) sb.Append("0123456789ABCDEF"[rnd.Next(16)]);
			return sb.ToString();
		}

		private static int GetTicks()
		{
			try { return Verse.Find.TickManager.TicksGame; } catch { return 0; }
		}

		private static string FormatGameTime(int absTicks)
		{
			// 简化：直接返回绝对 Tick 数，避免对外部 API 的强依赖
			try { return absTicks.ToString(CultureInfo.InvariantCulture); } catch { return absTicks.ToString(); }
		}

		private static float RandRange(float a, float b)
		{
			try { return (float)(a + (new Random().NextDouble()) * (b - a)); } catch { return (a + b) / 2f; }
		}

		private static ServerRecord Clone(ServerRecord s)
		{
			return new ServerRecord
			{
				EntityId = s.EntityId,
				Level = s.Level,
				SerialHex12 = s.SerialHex12,
				BuiltAtAbsTicks = s.BuiltAtAbsTicks,
				BaseServerPersonaOverride = s.BaseServerPersonaOverride,
				BaseServerPersonaPresetKey = s.BaseServerPersonaPresetKey,
				InspectionIntervalHours = s.InspectionIntervalHours,
				InspectionEnabled = s.InspectionEnabled,
				NextSlotPointer = s.NextSlotPointer,
				InspectionSlots = (s.InspectionSlots ?? new List<InspectionSlot>()).Select(x => x == null ? null : new InspectionSlot { Index = x.Index, ToolName = x.ToolName, Enabled = x.Enabled, LastRunAbsTicks = x.LastRunAbsTicks, NextDueAbsTicks = x.NextDueAbsTicks }).ToList(),
				ServerPersonaSlots = (s.ServerPersonaSlots ?? new List<ServerPersonaSlot>()).Select(x => x == null ? null : new ServerPersonaSlot { Index = x.Index, PresetKey = x.PresetKey, OverrideText = x.OverrideText, Enabled = x.Enabled }).ToList(),
				LastSummaryText = s.LastSummaryText,
				LastSummaryAtAbsTicks = s.LastSummaryAtAbsTicks
			};
		}

		private static List<string> BuildServerPersonaLines(ServerRecord s, ServerPromptPreset preset)
		{
			var lines = new List<string>();
			bool hasSlots = s.ServerPersonaSlots != null && s.ServerPersonaSlots.Any(x => x != null && x.Enabled && (!string.IsNullOrWhiteSpace(x.OverrideText) || !string.IsNullOrWhiteSpace(x.PresetKey)));
			if (hasSlots)
			{
				foreach (var slot in s.ServerPersonaSlots.OrderBy(x => x.Index))
				{
					if (slot == null || !slot.Enabled) continue;
					if (!string.IsNullOrWhiteSpace(slot.OverrideText)) { lines.Add(slot.OverrideText); continue; }
					if (!string.IsNullOrWhiteSpace(slot.PresetKey))
					{
						var opt = preset?.ServerPersonaOptions?.FirstOrDefault(o => string.Equals(o.key, slot.PresetKey, StringComparison.OrdinalIgnoreCase));
						if (opt != null && !string.IsNullOrWhiteSpace(opt.text)) lines.Add(opt.text);
					}
				}
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaOverride)) lines.Add(s.BaseServerPersonaOverride);
				else if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaPresetKey))
				{
					var opt = preset?.ServerPersonaOptions?.FirstOrDefault(o => string.Equals(o.key, s.BaseServerPersonaPresetKey, StringComparison.OrdinalIgnoreCase));
					if (opt != null && !string.IsNullOrWhiteSpace(opt.text)) lines.Add(opt.text);
				}
				else if (!string.IsNullOrWhiteSpace(preset?.BaseServerPersonaText)) lines.Add(preset.BaseServerPersonaText);
			}
			return lines;
		}

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

		// 将指定巡检会话(convKey)的历史条目裁剪为不超过 MaxInspectionHistoryEntries
		private async Task PruneInspectionHistoryIfNeededAsync(string convKey, CancellationToken ct)
		{
			try
			{
				var all = await _history.GetAllEntriesRawAsync(convKey, ct).ConfigureAwait(false);
				var list = (all ?? Array.Empty<RimAI.Core.Source.Modules.History.Models.HistoryEntry>()).Where(e => e != null && !e.Deleted).OrderBy(e => e.Timestamp).ToList();
				if (list.Count <= MaxInspectionHistoryEntries) return;
				int surplus = list.Count - MaxInspectionHistoryEntries;
				for (int i = 0; i < surplus; i++)
				{
					var victim = list[i];
					if (victim == null) continue;
					try { await _history.DeleteEntryAsync(convKey, victim.Id, ct).ConfigureAwait(false); } catch { }
				}
			}
			catch { }
		}
	}
}


