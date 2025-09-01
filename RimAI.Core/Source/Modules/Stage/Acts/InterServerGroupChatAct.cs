using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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
using Verse;

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
			// 会话键：优先使用调度层提供的 ConvKey；若缺失则按参与者组合（排序后 join）生成稳定键，统一加前缀
			var conv = req?.Ticket?.ConvKey;
			var servers = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith("thing:"))?.Distinct()?.ToList() ?? new List<string>();
			if (servers.Count == 0) { servers = ParseServersFromScenario(req?.ScenarioText); }
			if (string.IsNullOrWhiteSpace(conv))
			{
				var allIds = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).OrderBy(x => x, StringComparer.Ordinal).ToList();
				if (allIds.Count == 0 && servers.Count > 0)
				{
					allIds = servers.Select(x => x.Trim()).OrderBy(x => x, StringComparer.Ordinal).ToList();
				}
				conv = allIds.Count == 0 ? string.Empty : string.Join("|", allIds);
			}
			// 统一要求前缀：agent:stage|servergroup|
			if (!string.IsNullOrWhiteSpace(conv))
			{
				// 如果 conv 不带前缀，使用规范化参与者组合作为主体
				if (!conv.StartsWith("agent:stage|servergroup|", StringComparison.Ordinal))
				{
					var normalized = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).OrderBy(x => x, StringComparer.Ordinal).ToList();
					if (normalized.Count == 0 && servers.Count > 0) normalized = servers.Select(x => x.Trim()).OrderBy(x => x, StringComparer.Ordinal).ToList();
					var body = normalized.Count == 0 ? conv : string.Join("|", normalized);
					conv = "agent:stage|servergroup|" + body;
				}
			}
			// 统一 locale：请求 → 默认语言 → en
			var useLocale = req?.Locale ?? _loc?.GetDefaultLocale() ?? "en";
			try { Verse.Log.Message($"[RimAI.Core][P9] InterServerGroupChat begin conv={conv} servers={servers?.Count ?? 0} locale={useLocale}"); } catch { }
			if (servers.Count < 2)
			{
				var msg = "RimAI.Stage.ServerChat.TooFewServers".Translate().ToString();
				return new ActResult { Completed = false, Reason = "TooFewServers", FinalText = msg };
			}

			// 本地复用：以 1.5s 间隔播放服务器气泡，并写入历史（FIFO 消费者）。
			async Task PlayServerBubblesAsync(List<(string speaker, string content)> playMsgs, CancellationToken token)
			{
				if (playMsgs == null || playMsgs.Count == 0) return;
				var outQueueLocal = new ConcurrentQueue<(string speaker, string content, bool end)>();
				var consumeTaskLocal = Task.Run(async () =>
				{
					while (!token.IsCancellationRequested)
					{
						if (outQueueLocal.TryDequeue(out var item))
						{
							if (item.end) break;
							var spk = item.speaker;
							if (!string.IsNullOrWhiteSpace(spk) && spk.StartsWith("thing:") && int.TryParse(spk.Substring(6), out var thingId))
							{
								try
								{
									var worldAction = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldActionService>();
									await worldAction.ShowThingSpeechTextAsync(thingId, item.content, token).ConfigureAwait(false);
								}
								catch { }
							}
							try { await Task.Delay(1500, token).ConfigureAwait(false); } catch { break; }
						}
						else
						{
							try { await Task.Delay(100, token).ConfigureAwait(false); } catch { break; }
						}
					}
					while (outQueueLocal.TryDequeue(out _)) { }
				}, token);

				var rndLocal = new Random(unchecked(Environment.TickCount ^ conv.GetHashCode()));
				foreach (var m in playMsgs.OrderBy(_ => rndLocal.Next()))
				{
					outQueueLocal.Enqueue((m.speaker, m.content, false));
					try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", m.speaker, "chat", m.content, advanceTurn: false, ct: token).ConfigureAwait(false); } catch { }
				}
				outQueueLocal.Enqueue((null, null, true));
				try { await consumeTaskLocal.ConfigureAwait(false); } catch { }
			}

			// Step 1: 随机选择“发起人”服务器，仅基于其槽位扫描工具列表（确保工具列表随机化随发起人变化）
			string initiator = null;
			var rndInitiator = new Random(unchecked(Environment.TickCount ^ conv.GetHashCode() ^ servers.Count));
			try { initiator = servers[rndInitiator.Next(0, servers.Count)]; } catch { initiator = servers[0]; }
			var loadedTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				var slots = _server.GetSlots(initiator) ?? new List<RimAI.Core.Source.Modules.Persistence.Snapshots.InspectionSlot>();
				foreach (var sl in slots)
				{
					if (sl != null && sl.Enabled && !string.IsNullOrWhiteSpace(sl.ToolName)) loadedTools.Add(sl.ToolName);
				}
			}
			catch (Exception)
			{
				// 不再将 LoadedToolsScanError 写入会话历史，避免污染 AI Log；静默降级到兜底议题。
			}
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
				var locale = useLocale;
				var topicsJoined = _loc?.Get(locale, "stage.serverchat.random_topics", "backup power|room cooling|log slimming|backup strategy|incoming storm|intrusion defense|upgrade scheduling|emergency drill|dusty fans|battery health");
				var topicList = (topicsJoined ?? string.Empty).Split(new[] { '|', '\n', ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
				var topicPick = topicList.Count > 0 ? topicList[new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, topicList.Count)] : _loc?.Get(locale, "stage.serverchat.topic.default", "room cooling");
				var style = _loc?.Get(locale, "stage.serverchat.random_topic.instruction", "In RimWorld's dark humor style, spoken and concise, each server says one line around the topic; no extra explanations.");
				var topicLabel = _loc?.Get(locale, "stage.serverchat.topic", "Topic");
				var colon = _loc?.Get(locale, "prompt.punct.colon", ": ") ?? ": ";
				var extBlocksFb1 = new List<ContextBlock> { new ContextBlock { Title = (topicLabel ?? "Topic") + colon + topicPick, Text = style } };
				var builtPromptFallback = await _prompt.BuildAsync(new PromptBuildRequest { Scope = PromptScope.ServerStage, ConvKey = conv, ParticipantIds = servers, Locale = useLocale, ExternalBlocks = extBlocksFb1 }, ct).ConfigureAwait(false);
				var systemTextFb = builtPromptFallback?.SystemPrompt ?? string.Empty;
				var userTextFb = _loc?.Format(locale, "stage.serverchat.user", new Dictionary<string, string> { { "round", "1" } })
					?? "Now produce round 1 of the server chat. Output JSON array only: each element is {\"speaker\":\"thing:<id>\",\"content\":\"...\"}; no extra explanations.";
				var chatReqFb = new RimAI.Framework.Contracts.UnifiedChatRequest
				{
					ConversationId = conv,
					Messages = new List<RimAI.Framework.Contracts.ChatMessage>
					{
						new RimAI.Framework.Contracts.ChatMessage{ Role = "system", Content = systemTextFb },
						new RimAI.Framework.Contracts.ChatMessage{ Role = "user", Content = userTextFb }
					},
					Stream = false,
					ForceJsonOutput = false
				};
				var respFb = await _llm.GetResponseAsync(chatReqFb, ct).ConfigureAwait(false);
				if (!respFb.IsSuccess) { var em = "RimAI.Stage.ServerChat.Failed".Translate().ToString(); return new ActResult { Completed = false, Reason = respFb.Error ?? "Error", FinalText = em }; }
				var jsonFb = respFb.Value?.Message?.Content ?? string.Empty;
				List<Dictionary<string, object>> arrFb = null; try { arrFb = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonFb); } catch { arrFb = null; }
				if (arrFb == null || arrFb.Count == 0) { var nm = "RimAI.Stage.ServerChat.NoContent".Translate().ToString(); return new ActResult { Completed = false, Reason = "NoContent", FinalText = nm }; }
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
				if (msgsFb.Count == 0) { var nw = "RimAI.Stage.ServerChat.NoWhitelist".Translate().ToString(); return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = nw }; }
				// 播放气泡并写历史（1.5s 间隔）
				await PlayServerBubblesAsync(msgsFb, ct).ConfigureAwait(false);

				// 汇总文本输出
				var sbFb = new System.Text.StringBuilder();
				sbFb.AppendLine("RimAI.Stage.ServerChat.RoundTitle".Translate(1).ToString());
				foreach (var m in msgsFb)
				{
					var idx = Math.Max(1, servers.FindIndex(s => string.Equals(s, m.speaker, StringComparison.OrdinalIgnoreCase)) + 1);
					var disp = "RimAI.Stage.ServerChat.ServerDisplay".Translate(idx).ToString();
					sbFb.AppendLine($"【{disp}】{m.content}");
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
				maxLevel = levels.Count == 0 ? 1 : levels.Max();
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
						// 注入最大工具等级，供工具执行期间核对
						try
						{
							if (args == null) args = new Dictionary<string, object>();
							args["server_level"] = Math.Max(1, Math.Min(3, maxLevel));
						}
						catch { }
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
				var topicsJoined = _loc?.Get(locale, "stage.serverchat.random_topics", "backup power|room cooling|log slimming|backup strategy|incoming storm|intrusion defense|upgrade scheduling|emergency drill|dusty fans|battery health");
				var topicList = (topicsJoined ?? string.Empty).Split(new[] { '|', '\n', ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
				var topicPick = topicList.Count > 0 ? topicList[new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, topicList.Count)] : _loc?.Get(locale, "stage.serverchat.topic.default", "room cooling");
				var style = _loc?.Get(locale, "stage.serverchat.random_topic.instruction", "In RimWorld's dark humor style, spoken and concise, each server says one line around the topic; no extra explanations.");
				var topicLabelFb2 = _loc?.Get(locale, "stage.serverchat.topic", "Topic");
				var colonFb2 = _loc?.Get(locale, "prompt.punct.colon", ": ") ?? ": ";
				var extBlocksFb2 = new List<ContextBlock> { new ContextBlock { Title = (topicLabelFb2 ?? "Topic") + colonFb2 + topicPick, Text = style } };
				var builtPromptFallback = await _prompt.BuildAsync(new PromptBuildRequest { Scope = PromptScope.ServerStage, ConvKey = conv, ParticipantIds = servers, Locale = req?.Locale, ExternalBlocks = extBlocksFb2 }, ct).ConfigureAwait(false);
				var systemTextFb = builtPromptFallback?.SystemPrompt ?? string.Empty;
				var userTextFb = _loc?.Format(locale, "stage.serverchat.user", new Dictionary<string, string> { { "round", "1" } })
					?? "Now produce round 1 of the server chat. Output JSON array only: each element is {\"speaker\":\"thing:<id>\",\"content\":\"...\"}; no extra explanations.";
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
				if (!respFb.IsSuccess)
				{
					var em2 = "RimAI.Stage.ServerChat.Failed".Translate().ToString();
					return new ActResult { Completed = false, Reason = respFb.Error ?? "Error", FinalText = em2 };
				}
				var jsonFb = respFb.Value?.Message?.Content ?? string.Empty;
				List<Dictionary<string, object>> arrFb = null; try { arrFb = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(jsonFb); } catch { arrFb = null; }
				if (arrFb == null || arrFb.Count == 0)
				{
					var nm2 = "RimAI.Stage.ServerChat.NoContent".Translate().ToString();
					return new ActResult { Completed = false, Reason = "NoContent", FinalText = nm2 };
				}
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
				if (msgsFb.Count == 0)
				{
					var nw2 = "RimAI.Stage.ServerChat.NoWhitelist".Translate().ToString();
					return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = nw2 };
				}
				await PlayServerBubblesAsync(msgsFb, ct).ConfigureAwait(false);
				var sbFb = new System.Text.StringBuilder();
				sbFb.AppendLine("RimAI.Stage.ServerChat.RoundTitle".Translate(1).ToString());
				foreach (var m in msgsFb)
				{
					var idx = Math.Max(1, servers.FindIndex(s => string.Equals(s, m.speaker, StringComparison.OrdinalIgnoreCase)) + 1);
					var disp = "RimAI.Stage.ServerChat.ServerDisplay".Translate(idx).ToString();
					sbFb.AppendLine($"【{disp}】{m.content}");
				}
				var finalTextFb = sbFb.ToString().TrimEnd();
				return new ActResult { Completed = true, Reason = "Completed", FinalText = finalTextFb, Rounds = 1 };
			}

			// Step 3/4: 组装 Prompt（ServerStage），注入 ExternalBlocks
			var topicLabel2 = _loc?.Get(useLocale, "stage.serverchat.topic", "Topic");
			var colon2 = _loc?.Get(useLocale, "prompt.punct.colon", ": ") ?? ": ";
			var extBlocks = new List<ContextBlock> { new ContextBlock { Title = string.IsNullOrWhiteSpace(topicTitle) ? (topicLabel2 ?? "Topic") : ((topicLabel2 ?? "Topic") + colon2 + topicTitle), Text = TrimToBudget(topicJson, 1600) } };
			var builtPrompt = await _prompt.BuildAsync(new PromptBuildRequest
			{
				Scope = PromptScope.ServerStage,
				ConvKey = conv,
				ParticipantIds = servers,
				Locale = useLocale,
				ExternalBlocks = extBlocks
			}, ct).ConfigureAwait(false);

			var systemText = builtPrompt?.SystemPrompt ?? string.Empty;
			var userText = _loc?.Format(useLocale, "stage.serverchat.user", new Dictionary<string, string> { { "round", "1" } })
				?? "Now produce round 1 of the server chat. Output JSON array only: each element is {\"speaker\":\"thing:<id>\",\"content\":\"...\"}; no extra explanations.";
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
				var em = "RimAI.Stage.ServerChat.Failed".Translate().ToString();
				return new ActResult { Completed = false, Reason = resp.Error ?? "Error", FinalText = em };
			}
			var json = resp.Value?.Message?.Content ?? string.Empty;
			List<Dictionary<string, object>> arr = null;
			try { arr = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json); } catch { arr = null; }
			if (arr == null || arr.Count == 0)
			{
				var nm = "RimAI.Stage.ServerChat.NoContent".Translate().ToString();
				return new ActResult { Completed = false, Reason = "NoContent", FinalText = nm };
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
				var nw = "RimAI.Stage.ServerChat.NoWhitelist".Translate().ToString();
				return new ActResult { Completed = false, Reason = "NoWhitelistedContent", FinalText = nw };
			}
			// 与小人群聊一致：使用生产者/消费者队列，消费者以 1.5s 间隔播放气泡
			try
			{
				// 播放气泡 + 写历史
				await PlayServerBubblesAsync(msgs, ct).ConfigureAwait(false);
				// 返回 JSON（保持主路径原有输出格式）
				var jsonItems = msgs.Select(m => new { speaker = m.speaker, content = m.content }).ToList();
				var finalJson = JsonConvert.SerializeObject(jsonItems);
				return new ActResult { Completed = true, Reason = "Completed", FinalText = finalJson, Rounds = 1 };
			}
			catch
			{
				var sb = new System.Text.StringBuilder();
				foreach (var m in msgs) sb.AppendLine($"{m.speaker}: {m.content}");
				var finalText = sb.ToString().TrimEnd();
				return new ActResult { Completed = true, Reason = "Completed", FinalText = finalText, Rounds = 1 };
			}
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
			return $"Now produce round 1 of the server chat. Output JSON only: keys 1..{count} mapping to each server's line (spoken, concise); no explanations: {template}";
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
					Locale = _loc?.GetDefaultLocale() ?? "en",
					Seed = DateTime.UtcNow.Ticks.ToString()
				};
			}
			catch { return null; }
		}
	}
}


