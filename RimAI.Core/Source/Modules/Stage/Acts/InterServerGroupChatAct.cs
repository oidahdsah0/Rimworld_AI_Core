using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.Modules.Tooling;
using Newtonsoft.Json.Linq;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
	internal sealed class InterServerGroupChatAct : IStageAct, IAutoStageIntentProvider
	{
		private readonly ILLMService _llm;
		private readonly IPromptService _prompt;
		private readonly IWorldDataService _world;
		private readonly ILocalizationService _loc;
        private readonly IHistoryService _history;
        private readonly IServerService _server;
        private readonly IToolRegistryService _tooling;

		public InterServerGroupChatAct(ILLMService llm, IPromptService prompt, IWorldDataService world, ILocalizationService loc, IHistoryService history, IServerService server, IToolRegistryService tooling)
		{
			_llm = llm; _prompt = prompt; _world = world; _loc = loc; _history = history; _server = server; _tooling = tooling;
		}

		public string Name => "InterServerGroupChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		public bool IsEligible(StageExecutionRequest req)
		{
			return req?.Ticket != null && !string.IsNullOrWhiteSpace(req.Ticket.ConvKey);
		}

		public async Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct)
		{
			var conv = req?.Ticket?.ConvKey ?? ("agent:stage|" + DateTime.UtcNow.Ticks);
			var servers = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("thing:"))?.Distinct()?.ToList() ?? new List<string>();
			if (servers.Count == 0) { servers = ParseServersFromScenario(req?.ScenarioText); }
			if (servers.Count < 2)
			{
				return new ActResult { Completed = false, Reason = "TooFewServers", FinalText = "（服务器数量不足，跳过本次群聊）" };
			}

			// Step 1: 汇总“已加载工具 List”（从服务器巡检槽）
			var loadedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (var sid in servers)
				{
					var slots = _server.GetSlots(sid) ?? new List<RimAI.Core.Source.Modules.Persistence.Snapshots.InspectionSlot>();
					foreach (var sl in slots)
					{
						if (sl != null && sl.Enabled && !string.IsNullOrWhiteSpace(sl.ToolName)) loadedTools.Add(sl.ToolName);
					}
				}
			}
			catch (Exception ex) { /* best effort; just log via history */ try { await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"loadedToolsScanError:{ex.GetType().Name}", false, ct).ConfigureAwait(false); } catch { } }
			// 若无工具，继续走后续兜底流程（不再直接返回），以随机议题开启群聊

			// 过滤为当前已注册工具集合
			try
			{
				var registered = _tooling.GetRegisteredToolNames() ?? Array.Empty<string>();
				loadedTools.IntersectWith(registered);
			}
			catch (Exception ex) { try { await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"toolRegistryError:{ex.GetType().Name}", false, ct).ConfigureAwait(false); } catch { } }
			if (loadedTools.Count == 0)
			{
				// Fallback：无工具可用时，生成一个随机关联话题（本地化），并以黑色幽默风格讨论
				var locale = req?.Locale;
				var topicsJoined = _loc?.Get(locale, "stage.serverchat.random_topics", "供电故障备援|机房散热|日志瘦身|备份策略|风暴来袭|入侵防护|升级调度|应急演练|风扇积灰|电池健康");
				var topicList = (topicsJoined ?? string.Empty).Split(new[] { '|', '\n', ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
				var topicPick = topicList.Count > 0 ? topicList[new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, topicList.Count)] : "机房散热";
				var style = _loc?.Get(locale, "stage.serverchat.random_topic.instruction", "请以 RimWorld 的黑色幽默风格、口语化、简短地围绕该话题各说一句；不输出解释文本。");
				var extBlocksFb1 = new List<ContextBlock> { new ContextBlock { Title = "议题：" + topicPick, Text = style } };
				var builtPromptFallback = await _prompt.BuildAsync(new PromptBuildRequest { Scope = PromptScope.ServerStage, ConvKey = conv, ParticipantIds = servers, Locale = req?.Locale, ExternalBlocks = extBlocksFb1 }, ct).ConfigureAwait(false);
				var systemTextFb = builtPromptFallback?.SystemPrompt ?? string.Empty;
				var userTextFb = "现在，生成第1轮服务器群聊。";
				var chatReqFb = new RimAI.Framework.Contracts.UnifiedChatRequest
				{
					ConversationId = conv,
					Messages = new List<RimAI.Framework.Contracts.ChatMessage>
					{
						new RimAI.Framework.Contracts.ChatMessage{ Role = "system", Content = systemTextFb },
						new RimAI.Framework.Contracts.ChatMessage{ Role = "user", Content = userTextFb }
					},
					Stream = false,
					ForceJsonOutput = true
				};
				var respFb = await _llm.GetResponseAsync(chatReqFb, ct).ConfigureAwait(false);
				if (!respFb.IsSuccess) { return new ActResult { Completed = false, Reason = respFb.Error ?? "Error", FinalText = "（服务器群聊失败或超时）" }; }
				var jsonFb = respFb.Value?.Message?.Content ?? string.Empty;
				List<Dictionary<string, object>> arrFb = null; try { arrFb = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonFb); } catch { arrFb = null; }
				if (arrFb == null || arrFb.Count == 0) { return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）" }; }
				var msgsFb = new List<(string speaker, string content)>();
				foreach (var item in arrFb)
				{
					if (item == null) continue;
					object spkObj; object txtObj;
					var hasSpk = item.TryGetValue("speaker", out spkObj);
					var hasTxt = item.TryGetValue("content", out txtObj);
					var spk = hasSpk ? spkObj?.ToString() : null;
					var txt = hasTxt ? txtObj?.ToString() : null;
					if (string.IsNullOrWhiteSpace(spk) || string.IsNullOrWhiteSpace(txt)) continue;
					if (!servers.Contains(spk)) continue;
					msgsFb.Add((spk.Trim(), txt.Trim()));
				}
				if (msgsFb.Count == 0) { return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = "（无白名单内有效输出）" }; }
				var sbFb = new System.Text.StringBuilder();
				sbFb.AppendLine("第1轮");
				foreach (var m in msgsFb)
				{
					var idx = Math.Max(1, servers.FindIndex(s => string.Equals(s, m.speaker, StringComparison.OrdinalIgnoreCase)) + 1);
					var disp = $"服务器{idx}";
					sbFb.AppendLine($"【{disp}】{m.content}");
					try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", m.speaker, "chat", m.content, advanceTurn: false, ct: ct).ConfigureAwait(false); } catch { }
				}
				var finalTextFb = sbFb.ToString().TrimEnd();
				return new ActResult { Completed = true, Reason = "Completed", FinalText = finalTextFb, Rounds = 1 };
			}

			// 估计最大工具等级（取所有服务器的最小等级）
			int maxLevel = 1;
			try
			{
				var levels = new List<int>();
				foreach (var sid in servers)
				{
					var sstr = sid.StartsWith("thing:") ? sid.Substring(6) : sid;
					if (int.TryParse(sstr, out var thingId))
					{
						var lv = await _world.GetAiServerLevelAsync(thingId, ct).ConfigureAwait(false);
						levels.Add(Math.Max(1, Math.Min(3, lv)));
					}
				}
				maxLevel = levels.Count == 0 ? 1 : levels.Min();
			}
			catch { maxLevel = 1; }

			// Step 2: 随机选择一个工具并执行（尽力填充所需参数）
			var topicTitle = string.Empty;
			string topicJson = null;
			try
			{
				var rnd = new Random(unchecked(Environment.TickCount ^ conv.GetHashCode()));
				var candidates = loadedTools.ToList();
				for (int i = candidates.Count - 1; i > 0; i--) { int j = rnd.Next(0, i + 1); var tmp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = tmp; }

				Dictionary<string, object> BuildArgsFor(string toolName)
				{
					try
					{
						var classic = _tooling.GetClassicToolCallSchema(new ToolQueryOptions { MaxToolLevel = maxLevel });
						var list = classic?.ToolsJson ?? new List<string>();
						foreach (var j in list)
						{
							if (string.IsNullOrWhiteSpace(j)) continue;
							var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(j);
							var fn = obj?["function"] as Newtonsoft.Json.Linq.JObject;
							var name = fn?[(string)"name"]?.ToString();
							if (!string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase)) continue;
							var parameters = fn?["parameters"] as Newtonsoft.Json.Linq.JObject;
							if (parameters == null) return new Dictionary<string, object>();
							var props = parameters["properties"] as Newtonsoft.Json.Linq.JObject;
							var required = (parameters["required"] as Newtonsoft.Json.Linq.JArray)?.Select(x => x.ToString()).ToList() ?? new List<string>();
							var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
							var rnd2 = new Random(unchecked(Environment.TickCount ^ toolName.GetHashCode()));
							foreach (var reqName in required)
							{
								var p = props?[reqName] as Newtonsoft.Json.Linq.JObject;
								if (p == null) { return null; }
								var enumArr = p["enum"] as Newtonsoft.Json.Linq.JArray;
								if (enumArr != null && enumArr.Count > 0)
								{
									args[reqName] = enumArr[rnd2.Next(0, enumArr.Count)]?.ToString();
									continue;
								}
								var def = p["default"];
								if (def != null)
								{
									args[reqName] = def.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? (int)def : (def.Type == Newtonsoft.Json.Linq.JTokenType.Float ? (double)def : def.ToString());
									continue;
								}
								return null;
							}
							return args;
						}
						// 若未找到匹配的工具定义，返回空字典（表示无需参数）
						return new Dictionary<string, object>();
					}
					catch { return new Dictionary<string, object>(); }
				}

				foreach (var tool in candidates)
				{
					Dictionary<string, object> args = null;
					try { args = BuildArgsFor(tool); } catch { args = null; }
					if (args == null) continue;
					try
					{
						var result = await _tooling.ExecuteToolAsync(tool, args, ct).ConfigureAwait(false);
						topicJson = JsonConvert.SerializeObject(result);
						topicTitle = _tooling.GetToolDisplayNameOrNull(tool) ?? tool;
						break;
					}
					catch { }
				}
			}
			catch (Exception ex) { try { await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"toolPickError:{ex.GetType().Name}", false, ct).ConfigureAwait(false); } catch { } }
			if (string.IsNullOrWhiteSpace(topicJson))
			{
				// 工具执行未产出时，也回退到随机关联话题
				var locale = req?.Locale;
				var topicsJoined = _loc?.Get(locale, "stage.serverchat.random_topics", "供电故障备援|机房散热|日志瘦身|备份策略|风暴来袭|入侵防护|升级调度|应急演练|风扇积灰|电池健康");
				var topicList = (topicsJoined ?? string.Empty).Split(new[] { '|', '\n', ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
				var topicPick = topicList.Count > 0 ? topicList[new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, topicList.Count)] : "机房散热";
				var style = _loc?.Get(locale, "stage.serverchat.random_topic.instruction", "请以 RimWorld 的黑色幽默风格、口语化、简短地围绕该话题各说一句；不输出解释文本。");
				var extBlocksFb2 = new List<ContextBlock> { new ContextBlock { Title = "议题：" + topicPick, Text = style } };
				var builtPromptFallback = await _prompt.BuildAsync(new PromptBuildRequest { Scope = PromptScope.ServerStage, ConvKey = conv, ParticipantIds = servers, Locale = req?.Locale, ExternalBlocks = extBlocksFb2 }, ct).ConfigureAwait(false);
				var systemTextFb = builtPromptFallback?.SystemPrompt ?? string.Empty;
				var userTextFb = "现在，生成第1轮服务器群聊。";
				var chatReqFb = new RimAI.Framework.Contracts.UnifiedChatRequest
				{
					ConversationId = conv,
					Messages = new List<RimAI.Framework.Contracts.ChatMessage>
					{
						new RimAI.Framework.Contracts.ChatMessage{ Role = "system", Content = systemTextFb },
						new RimAI.Framework.Contracts.ChatMessage{ Role = "user", Content = userTextFb }
					},
					Stream = false,
					ForceJsonOutput = true
				};
				var respFb = await _llm.GetResponseAsync(chatReqFb, ct).ConfigureAwait(false);
				if (!respFb.IsSuccess) { return new ActResult { Completed = false, Reason = respFb.Error ?? "Error", FinalText = "（服务器群聊失败或超时）" }; }
				var jsonFb = respFb.Value?.Message?.Content ?? string.Empty;
				List<Dictionary<string, object>> arrFb = null; try { arrFb = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonFb); } catch { arrFb = null; }
				if (arrFb == null || arrFb.Count == 0) { return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）" }; }
				var msgsFb = new List<(string speaker, string content)>();
				foreach (var item in arrFb)
				{
					if (item == null) continue;
					object spkObj; object txtObj;
					var hasSpk = item.TryGetValue("speaker", out spkObj);
					var hasTxt = item.TryGetValue("content", out txtObj);
					var spk = hasSpk ? spkObj?.ToString() : null;
					var txt = hasTxt ? txtObj?.ToString() : null;
					if (string.IsNullOrWhiteSpace(spk) || string.IsNullOrWhiteSpace(txt)) continue;
					if (!servers.Contains(spk)) continue;
					msgsFb.Add((spk.Trim(), txt.Trim()));
				}
				if (msgsFb.Count == 0) { return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = "（无白名单内有效输出）" }; }
				var sbFb = new System.Text.StringBuilder();
				sbFb.AppendLine("第1轮");
				foreach (var m in msgsFb)
				{
					var idx = Math.Max(1, servers.FindIndex(s => string.Equals(s, m.speaker, StringComparison.OrdinalIgnoreCase)) + 1);
					var disp = $"服务器{idx}";
					sbFb.AppendLine($"【{disp}】{m.content}");
					try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", m.speaker, "chat", m.content, advanceTurn: false, ct: ct).ConfigureAwait(false); } catch { }
				}
				var finalTextFb = sbFb.ToString().TrimEnd();
				return new ActResult { Completed = true, Reason = "Completed", FinalText = finalTextFb, Rounds = 1 };
			}

			// Step 3/4: 组装 Prompt（ServerStage），注入 ExternalBlocks
			var extBlocks = new List<ContextBlock> { new ContextBlock { Title = string.IsNullOrWhiteSpace(topicTitle) ? "议题" : ("议题：" + topicTitle), Text = TrimToBudget(topicJson, 1600) } };
			var builtPrompt = await _prompt.BuildAsync(new PromptBuildRequest
			{
				Scope = PromptScope.ServerStage,
				ConvKey = conv,
				ParticipantIds = servers,
				Locale = req?.Locale,
				ExternalBlocks = extBlocks
			}, ct).ConfigureAwait(false);

			var systemText = builtPrompt?.SystemPrompt ?? string.Empty;
			var userText = "现在，生成第1轮服务器群聊。";
			var chatReq = new RimAI.Framework.Contracts.UnifiedChatRequest
			{
				ConversationId = conv,
				Messages = new List<RimAI.Framework.Contracts.ChatMessage>
				{
					new RimAI.Framework.Contracts.ChatMessage{ Role = "system", Content = systemText },
					new RimAI.Framework.Contracts.ChatMessage{ Role = "user", Content = userText }
				},
				Stream = false,
				ForceJsonOutput = true
			};
			var resp = await _llm.GetResponseAsync(chatReq, ct).ConfigureAwait(false);
			if (!resp.IsSuccess)
			{
				return new ActResult { Completed = false, Reason = resp.Error ?? "Error", FinalText = "（服务器群聊失败或超时）" };
			}
			var json = resp.Value?.Message?.Content ?? string.Empty;
			List<Dictionary<string, object>> arr = null;
			try { arr = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json); } catch { arr = null; }
			if (arr == null || arr.Count == 0)
			{
				return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）" };
			}
			var msgs = new List<(string speaker, string content)>();
			foreach (var item in arr)
			{
				if (item == null) continue;
				object spkObj; object txtObj;
				var hasSpk = item.TryGetValue("speaker", out spkObj);
				var hasTxt = item.TryGetValue("content", out txtObj);
				var spk = hasSpk ? spkObj?.ToString() : null;
				var txt = hasTxt ? txtObj?.ToString() : null;
				if (string.IsNullOrWhiteSpace(spk) || string.IsNullOrWhiteSpace(txt)) continue;
				if (!servers.Contains(spk)) continue; // 白名单
				msgs.Add((spk.Trim(), txt.Trim()));
			}
			if (msgs.Count == 0)
			{
				return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = "（无白名单内有效输出）" };
			}

			var sb = new System.Text.StringBuilder();
			sb.AppendLine("第1轮");
			foreach (var m in msgs)
			{
				var idx = Math.Max(1, servers.FindIndex(s => string.Equals(s, m.speaker, StringComparison.OrdinalIgnoreCase)) + 1);
				var disp = $"服务器{idx}";
				sb.AppendLine($"【{disp}】{m.content}");
				try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", m.speaker, "chat", m.content, advanceTurn: false, ct: ct).ConfigureAwait(false); } catch { }
			}
			var finalText = sb.ToString().TrimEnd();
			return new ActResult { Completed = true, Reason = "Completed", FinalText = finalText, Rounds = 1 };
		}

		private static List<string> ParseServersFromScenario(string scenario)
		{
			var list = new List<string>();
			if (string.IsNullOrWhiteSpace(scenario)) return list;
			try
			{
				// 允许形如："servers=thing:123,thing:456" 的片段
				var parts = scenario.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var p in parts)
				{
					var s = p.Trim();
					if (!s.StartsWith("servers=")) continue;
					s = s.Substring("servers=".Length);
					var ids = s.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					foreach (var id in ids)
					{
						if (string.IsNullOrWhiteSpace(id)) continue;
						list.Add(id.Trim());
					}
				}
			}
			catch { }
			return list;
		}

		private static string BuildUserPrompt(int count)
		{
			var template = BuildJsonTemplate(count);
			return $"现在，生成第1轮服务器群聊。请严格输出 JSON，键为 1..{count}，值为对应服务器的台词（简短口语化），不得包含解释文本：{template}";
		}

		private static string BuildJsonTemplate(int count)
		{
			var mapKeys = new List<string>();
			for (int i = 0; i < count; i++) mapKeys.Add($"\"{i + 1}\":\"...\"");
			return "{" + string.Join(",", mapKeys) + "}";
		}

		private static string TrimToBudget(string s, int max)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			return s.Length <= max ? s : s.Substring(0, max);
		}

		public async Task<StageIntent> TryBuildAutoIntentAsync(CancellationToken ct)
		{
			try
			{
				var poweredIds = await _world.GetPoweredAiServerThingIdsAsync(ct).ConfigureAwait(false);
				var count = poweredIds?.Count ?? 0;
				if (count < 2) return null;
				var rnd = new Random(unchecked(Environment.TickCount ^ count));
				int kMax = Math.Min(5, count);
				int k = Math.Max(2, Math.Min(kMax, 2 + rnd.Next(0, Math.Max(1, kMax - 2 + 1))));
				var pick = poweredIds.ToList();
				for (int i = pick.Count - 1; i > 0; i--)
				{
					int j = rnd.Next(0, i + 1);
					var tmp = pick[i]; pick[i] = pick[j]; pick[j] = tmp;
				}
				var chosen = pick.Take(k).Select(id => $"thing:{id}").ToList();
				var scenario = $"servers={string.Join(",", chosen)}";
				return new StageIntent
				{
					ActName = Name,
					ParticipantIds = new[] { "agent:server_hub", "player:servers" },
					Origin = "Global",
					ScenarioText = scenario,
					Locale = "zh-Hans",
					Seed = DateTime.UtcNow.Ticks.ToString()
				};
			}
			catch { return null; }
		}
	}
}


