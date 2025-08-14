using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Persona.Templates;

namespace RimAI.Core.Source.Modules.Persona.Job
{
	internal sealed class PersonaJobService : IPersonaJobService
	{
		private readonly ILLMService _llm;
		private readonly IWorldDataService _world;
		private readonly ConfigurationService _cfg;
		private readonly Persona.IPersonaService _personaFacade;
		private readonly IPersonaTemplateManager _templates;


		public PersonaJobService(ILLMService llm, IWorldDataService world, IConfigurationService cfg, Persona.IPersonaService personaFacade, IPersonaTemplateManager templates)
		{ _llm = llm; _world = world; _cfg = cfg as ConfigurationService; _personaFacade = personaFacade; _templates = templates; }

		public RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot Get(string entityId)
		{
			return _personaFacade.Get(entityId)?.Job ?? new RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot();
		}

		public void Set(string entityId, string name, string description)
		{
			_personaFacade.Upsert(entityId, e => e.SetJob(name, description));
		}

		public async Task<string> GenerateDescriptionFromNameAsync(string entityId, string jobName, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(jobName)) return string.Empty;
			var timeoutMs = Math.Max(1000, _cfg?.GetInternal()?.Persona?.Generation?.TimeoutMs ?? 15000);
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			string playerName = string.Empty;
			try { playerName = await _world.GetPlayerNameAsync(cts.Token); } catch { playerName = string.Empty; }
			var locale = _cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans";
			var t = await _templates.GetTemplatesAsync(locale, cts.Token);
			var sys = (t?.Prompts?.jobFromName ?? string.Empty).Length > 0 && t.Prompts.jobFromName.Contains("You are") ? "System" : (locale.StartsWith("zh") ? "你是角色设定助手。" : "You are a character design assistant.");
			var prompt = (t?.Prompts?.jobFromName ?? (locale.StartsWith("zh") ? "你是角色设定助手。根据职务名生成一段<300字的职责描述，语言简练、具象、避免空话。输入: {jobName}; 背景: {context}" : "You are a character assistant. Generate a concise role description (<300 chars). Input: {jobName}; Context: {context}"));
			var user = prompt.Replace("{jobName}", jobName ?? string.Empty).Replace("{context}", "player=" + playerName);
			var conv = $"persona-job-{entityId}";
			var r = await _llm.GetResponseAsync(conv, sys, user, cts.Token);
			if (!r.IsSuccess)
			{
				Verse.Log.Warning($"[RimAI.Core][P7.Persona] job.gen failed entity={entityId} err={r.Error}");
				return string.Empty;
			}
			var text = r.Value?.Message?.Content ?? string.Empty;
			var cap = _cfg?.GetInternal()?.Persona?.Budget?.Job ?? 600;
			if (text.Length > cap) text = text.Substring(0, cap);
			return text.Trim();
		}
	}
}


