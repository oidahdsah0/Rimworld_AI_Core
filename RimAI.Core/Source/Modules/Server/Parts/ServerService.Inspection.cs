using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
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
	}
}
