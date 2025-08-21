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

namespace RimAI.Core.Source.Modules.Stage.Acts
{
	internal sealed class InterServerGroupChatAct : IStageAct
	{
		private readonly ILLMService _llm;
		private readonly IPromptService _prompt;
		private readonly IWorldDataService _world;
		private readonly ILocalizationService _loc;

		public InterServerGroupChatAct(ILLMService llm, IPromptService prompt, IWorldDataService world, ILocalizationService loc)
		{
			_llm = llm; _prompt = prompt; _world = world; _loc = loc;
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
			var servers = ParseServersFromScenario(req?.ScenarioText);
			if (servers.Count < 2)
			{
				return new ActResult { Completed = false, Reason = "TooFewServers", FinalText = "（服务器数量不足，跳过本次群聊）" };
			}

			// 优先使用本地化模板（持久化读取），否则回退到 P11 Prompting
			string locale = req?.Locale;
			string systemText = string.Empty;
			try { if (_loc != null) systemText = _loc.Get(locale, "stage.serverchat.system", string.Empty); } catch { systemText = string.Empty; }
			if (string.IsNullOrWhiteSpace(systemText))
			{
				try
				{
					if (_prompt != null)
					{
						var built = await _prompt.BuildAsync(new PromptBuildRequest
						{
							Scope = PromptScope.Stage,
							ConvKey = conv,
							ParticipantIds = new List<string> { "agent:server_hub", "player:servers" },
							Locale = req?.Locale
						}, ct).ConfigureAwait(false);
						systemText = built?.SystemPrompt ?? string.Empty;
					}
				}
				catch { systemText = string.Empty; }
			}

			string userText;
			if (_loc != null)
			{
				var template = BuildJsonTemplate(servers.Count);
				var args = new Dictionary<string, string> { { "round", "1" }, { "count", servers.Count.ToString() }, { "template", template } };
				try { userText = _loc.Format(locale, "stage.serverchat.user", args, string.Empty); }
				catch { userText = BuildUserPrompt(servers.Count); }
			}
			else
			{
				userText = BuildUserPrompt(servers.Count);
			}
			var chatReq = new RimAI.Framework.Contracts.UnifiedChatRequest
			{
				ConversationId = conv,
				Messages = new List<RimAI.Framework.Contracts.ChatMessage>
				{
					new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=systemText },
					new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=userText }
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
			Dictionary<string, string> shaped = null;
			try { shaped = JsonConvert.DeserializeObject<Dictionary<string, string>>(json); } catch { shaped = null; }
			if (shaped == null || shaped.Count == 0)
			{
				return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）" };
			}
			var ordered = new List<(int idx, string text)>();
			foreach (var kv in shaped)
			{
				if (!int.TryParse(kv.Key, out var idx)) continue;
				if (idx < 1 || idx > servers.Count) continue;
				var text = kv.Value?.Trim();
				if (string.IsNullOrWhiteSpace(text)) continue;
				ordered.Add((idx, text));
			}
			ordered.Sort((a,b) => a.idx.CompareTo(b.idx));
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("第1轮");
			foreach (var item in ordered)
			{
				var disp = $"服务器{item.idx}";
				sb.AppendLine($"【{disp}】{item.text}");
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
	}
}


