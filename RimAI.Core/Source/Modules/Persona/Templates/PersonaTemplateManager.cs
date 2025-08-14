using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.Persistence;

namespace RimAI.Core.Source.Modules.Persona.Templates
{
	internal sealed class PersonaTemplateManager : IPersonaTemplateManager
	{
		private readonly IPersistenceService _persistence;
		private readonly ConfigurationService _cfg;

		public PersonaTemplateManager(IPersistenceService persistence, IConfigurationService cfg)
		{ _persistence = persistence; _cfg = cfg as ConfigurationService; }

		public async Task<PersonaTemplates> GetTemplatesAsync(string locale, CancellationToken ct = default)
		{
			var lc = string.IsNullOrWhiteSpace(locale) ? (_cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans") : locale;
			var master = (_cfg?.GetInternal()?.Persona?.Templates?.MasterPath ?? "Resources/prompts/persona/{locale}.persona.json").Replace("{locale}", lc);
			var overridePath = (_cfg?.GetInternal()?.Persona?.Templates?.UserOverridePath ?? "Config/RimAI/Prompts/persona/{locale}.persona.user.json").Replace("{locale}", lc);
			// master: 目前先不读资源文件，返回内置默认模板；用户覆盖：尝试从 Config 下读
			var t = GetDefaultTemplates(lc);
			try
			{
				var json = await _persistence.ReadTextUnderConfigOrNullAsync(overridePath, ct);
				if (!string.IsNullOrWhiteSpace(json))
				{
					var fromFile = JsonConvert.DeserializeObject<PersonaTemplates>(json);
					if (fromFile != null)
					{
						// 简单覆盖 prompts 字段
						if (fromFile.Prompts != null)
						{
							if (!string.IsNullOrWhiteSpace(fromFile.Prompts.jobFromName)) t.Prompts.jobFromName = fromFile.Prompts.jobFromName;
							if (!string.IsNullOrWhiteSpace(fromFile.Prompts.biographyDraft)) t.Prompts.biographyDraft = fromFile.Prompts.biographyDraft;
							if (!string.IsNullOrWhiteSpace(fromFile.Prompts.ideology)) t.Prompts.ideology = fromFile.Prompts.ideology;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Verse.Log.Warning("[RimAI.Core][P7.Persona] template.load.warn " + ex.Message);
			}
			return t;
		}

		private static PersonaTemplates GetDefaultTemplates(string locale)
		{
			var zh = locale != null && locale.StartsWith("zh");
			return new PersonaTemplates
			{
				Version = 1,
				Locale = locale,
				Prompts = new PersonaTemplates.PromptsSection
				{
					jobFromName = zh ? "你是角色设定助手。根据职务名生成一段<300字的职责描述，语言简练、具象、避免空话。输入: {jobName}; 背景: {context}" : "You are a character assistant. Generate a concise role description (<300 chars). Input: {jobName}; Context: {context}",
					biographyDraft = zh ? "你是传记撰写助手。基于信息为角色生成3-5条不超过{maxPerItem}字的传记段落，每条独立成段，避免重复与无证据推断。信息: {facts}" : "Create 3-5 biography bullets (<= {maxPerItem} chars). Facts: {facts}",
					ideology = zh ? "你是设定编辑。为角色生成四段文本：世界观/价值观/行为准则/性格特质（各≤{maxSeg}字），语言紧凑一致。信息: {facts}" : "Generate four segments: worldview/values/code-of-conduct/traits (<= {maxSeg} chars). Facts: {facts}"
				}
			};
		}
	}
}


