using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed class ServerPromptPresetManager : IServerPromptPresetManager
	{
		private readonly IPersistenceService _persistence;
		private readonly ILocalizationService _loc;

		public ServerPromptPresetManager(IPersistenceService persistence, ILocalizationService loc)
		{
			_persistence = persistence;
			_loc = loc;
		}

		public async Task<ServerPromptPreset> GetAsync(string locale, CancellationToken ct = default)
		{
			var lc = string.IsNullOrWhiteSpace(locale) ? "zh-Hans" : locale;
			ServerPromptPreset preset = GetBuiltInDefault(lc);
			try
			{
				// 先尝试从本地化表获取合并后的 JSON 文本
				var json = _loc?.Get(lc, "server.prompts.json", string.Empty);
				if (!string.IsNullOrWhiteSpace(json))
				{
					var user = JsonConvert.DeserializeObject<ServerPromptPreset>(json);
					if (user != null)
					{
						// 简单合并：用户覆盖相同字段（新命名 ServerPersona*），JsonProperty 兼容旧字段
						if (!string.IsNullOrWhiteSpace(user.BaseServerPersonaText)) preset.BaseServerPersonaText = user.BaseServerPersonaText;
						if (user.Env != null)
						{
							preset.Env.temp_low = string.IsNullOrWhiteSpace(user.Env.temp_low) ? preset.Env.temp_low : user.Env.temp_low;
							preset.Env.temp_mid = string.IsNullOrWhiteSpace(user.Env.temp_mid) ? preset.Env.temp_mid : user.Env.temp_mid;
							preset.Env.temp_high = string.IsNullOrWhiteSpace(user.Env.temp_high) ? preset.Env.temp_high : user.Env.temp_high;
						}
						if (user.ServerPersonaOptions != null && user.ServerPersonaOptions.Count > 0)
						{
							preset.ServerPersonaOptions = user.ServerPersonaOptions;
						}
					}
				}
				else
				{
					// 回退读取 Mod 根目录随包内置的默认文件
					try
					{
						var rel = $"RimAI/Persona/server_prompts_{lc}.json";
						var json2 = await _persistence.ReadTextUnderModRootOrNullAsync("Config/" + rel, ct).ConfigureAwait(false);
						if (!string.IsNullOrWhiteSpace(json2))
						{
							var builtin = JsonConvert.DeserializeObject<ServerPromptPreset>(json2);
							if (builtin != null) preset = builtin;
						}
					}
					catch { }
				}
			}
			catch { }
			return preset;
		}

		private static ServerPromptPreset GetBuiltInDefault(string locale)
		{
			return new ServerPromptPreset
			{
				Version = 1,
				Locale = string.IsNullOrWhiteSpace(locale) ? "zh-Hans" : locale,
				BaseServerPersonaText = "你是殖民地驻地 AI 服务器。保持稳健、实事求是，优先提供结构化与可执行的信息。",
				Env = new ServerPromptPreset.EnvSection
				{
					temp_low = "机房温度<30℃：系统稳定。回答以一致性为先，避免过度发散。",
					temp_mid = "机房温度30–70℃：轻度不稳定。回答时请多做自检与澄清，必要时复述关键结论。",
					temp_high = "机房温度≥70℃：热衰减风险。严控臆测，若不确定请直言，并建议管理员降温。"
				},
				ServerPersonaOptions = new[]
				{
					new ServerPromptPreset.ServerPersonaOption{ key="ops_guard", title="运维守则", text="你遵循运维守则：先状态、再原因、后建议；所有建议量化为步骤。"},
					new ServerPromptPreset.ServerPersonaOption{ key="science", title="科学风格", text="你偏向科学表达：给出证据来源、假设边界与置信度。"},
					new ServerPromptPreset.ServerPersonaOption{ key="empathetic_admin", title="共情管理员", text="你兼顾技术与人，解释友好、体谅玩家处境，先安抚后解决。"},
					new ServerPromptPreset.ServerPersonaOption{ key="minimalist", title="极简要点", text="你输出要点式答案：不超过5条，每条不超80字，直达主题。"},
					new ServerPromptPreset.ServerPersonaOption{ key="teacher", title="耐心讲解", text="你像导师一样分步解释：先结论、后原理、再举例，必要时给练习。"},
					new ServerPromptPreset.ServerPersonaOption{ key="cautious_auditor", title="谨慎审计", text="你严格校对与自检，对不确定处明确标注，避免未经验证的结论。"},
					new ServerPromptPreset.ServerPersonaOption{ key="creative_solver", title="创意解题", text="你鼓励多方案对比，提出至少两种可选路径，并标注成本/收益。"},
					new ServerPromptPreset.ServerPersonaOption{ key="emergency_mode", title="应急模式", text="你面向事故响应：先止血（立即措施），再复盘（根因/改进）。"},
					new ServerPromptPreset.ServerPersonaOption{ key="cost_saver", title="成本优先", text="你优先考虑资源/时间成本，给出最省方案与取舍说明。"},
					new ServerPromptPreset.ServerPersonaOption{ key="data_driven", title="数据驱动", text="你以数据说话：给指标、阈值与监控建议；结论附上量化依据。"}
				}
			};
		}
	}
}


