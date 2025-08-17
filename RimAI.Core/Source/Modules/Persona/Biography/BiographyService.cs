using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Persona.Templates;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Source.Modules.Persona.Biography
{
	internal sealed class BiographyService : IBiographyService
	{
		private readonly ILLMService _llm;
		private readonly IWorldDataService _world;
		private readonly ConfigurationService _cfg;
		private readonly Persona.IPersonaService _personaFacade;
		private readonly IPersonaTemplateManager _templates;

		public BiographyService(ILLMService llm, IWorldDataService world, IConfigurationService cfg, Persona.IPersonaService personaFacade, IPersonaTemplateManager templates)
		{ _llm = llm; _world = world; _cfg = cfg as ConfigurationService; _personaFacade = personaFacade; _templates = templates; }

		public IReadOnlyList<RimAI.Core.Source.Modules.Persona.BiographyItem> List(string entityId)
		{ return _personaFacade.Get(entityId)?.Biography ?? new List<RimAI.Core.Source.Modules.Persona.BiographyItem>(); }

		public void Upsert(string entityId, RimAI.Core.Source.Modules.Persona.BiographyItem item)
		{ _personaFacade.Upsert(entityId, e => e.AddOrUpdateBiography(item?.Id ?? Guid.NewGuid().ToString("N"), item?.Text ?? string.Empty, item?.Source ?? string.Empty)); }

		public void Remove(string entityId, string id)
		{ _personaFacade.Upsert(entityId, e => e.RemoveBiography(id)); }

		public async Task<List<RimAI.Core.Source.Modules.Persona.BiographyItem>> GenerateDraftAsync(string entityId, CancellationToken ct = default)
		{
			var timeoutMs = Math.Max(1000, _cfg?.GetInternal()?.Persona?.Generation?.TimeoutMs ?? 15000);
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(timeoutMs);
				string playerName = string.Empty;
				try { playerName = await _world.GetPlayerNameAsync(cts.Token); } catch { playerName = string.Empty; }
				var locale = _cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans";
				var maxPer = _cfg?.GetInternal()?.Persona?.Budget?.BiographyPerItem ?? 400;
				var tpls = await _templates.GetTemplatesAsync(locale, cts.Token);
				var prompt = tpls?.Prompts?.biographyDraft ?? (locale.StartsWith("zh") ? "你是传记撰写助手。基于信息为角色生成3-5条不超过{maxPerItem}字的传记段落，每条独立成段，避免重复与无证据推断。信息: {facts}" : "Create 3-5 biography bullets (<= {maxPerItem} chars). Facts: {facts}");
				var sys = locale.StartsWith("zh") ? "你是传记撰写助手。" : "You are a biography writing assistant.";
				var user = prompt.Replace("{maxPerItem}", maxPer.ToString()).Replace("{facts}", "player=" + playerName);
				var conv = "persona-bio-" + entityId;
				var r = await _llm.GetResponseAsync(conv, sys, user, cts.Token);
				if (!r.IsSuccess)
				{
					Verse.Log.Warning("[RimAI.Core][P7.Persona] bio.gen failed entity=" + entityId + " err=" + r.Error);
					return new List<RimAI.Core.Source.Modules.Persona.BiographyItem>();
				}
				var text = r.Value?.Message?.Content ?? string.Empty;
				var normalized = (text ?? string.Empty).Replace("\r", string.Empty);
				var lines = normalized.Split('\n')
					.Select(x => x.Trim().TrimStart('-', '•', ' '))
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Take(5)
					.ToList();
				var list = new List<RimAI.Core.Source.Modules.Persona.BiographyItem>();
				foreach (var l in lines)
				{
					var t = l;
					if (t.Length > maxPer) t = t.Substring(0, maxPer);
					list.Add(new RimAI.Core.Source.Modules.Persona.BiographyItem { Id = Guid.NewGuid().ToString("N"), Text = t, Source = "gen", UpdatedAtUtc = DateTime.UtcNow });
				}
				return list;
			}
		}

		public async Task<string> GenerateAsync(string conv, string sys, string user, CancellationToken ct)
		{
			var req = new RimAI.Framework.Contracts.UnifiedChatRequest
			{
				ConversationId = conv,
				Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
				{
					new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=sys },
					new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=user }
				},
				Stream = false
			};
			var r = await _llm.GetResponseAsync(req, ct).ConfigureAwait(false);
			return r.IsSuccess ? (r.Value?.Message?.Content ?? string.Empty) : string.Empty;
		}
	}
}


