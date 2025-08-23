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
				// 使用 PromptService 统一组装（Scope=PersonaBiography）
				var prompting = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>();
				var req = new RimAI.Core.Source.Modules.Prompting.Models.PromptBuildRequest
				{
					Scope = RimAI.Core.Source.Modules.Prompting.Models.PromptScope.PersonaBiography,
					ConvKey = "persona-bio-" + entityId,
					ParticipantIds = new System.Collections.Generic.List<string> { entityId, "player:persona" },
					PawnLoadId = TryParsePawnId(entityId),
					IsCommand = false,
					Locale = _cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans",
					UserInput = string.Empty
				};
				var prompt = await prompting.BuildAsync(req, cts.Token).ConfigureAwait(false);
				var messages = new System.Collections.Generic.List<ChatMessage>();
				messages.Add(new ChatMessage { Role = "system", Content = prompt?.SystemPrompt ?? string.Empty });
				if (!string.IsNullOrWhiteSpace(prompt?.UserPrefixedInput))
				{
					messages.Add(new ChatMessage { Role = "user", Content = prompt.UserPrefixedInput });
				}
				// 记录实际将要发送的 Payload，确保与请求严格一致
				try 
				{ 
					string sysOut = string.Empty; string userOut = string.Empty; 
					foreach (var m in messages) { if (m?.Role == "system") sysOut = m.Content ?? string.Empty; else if (m?.Role == "user") userOut = m.Content ?? string.Empty; }
					Verse.Log.Message("[RimAI.Core][P7] Persona Biography Payload\nconv=" + req.ConvKey + "\n--- System ---\n" + sysOut + "\n--- User ---\n" + userOut);
				} 
				catch { }
				var ureq = new UnifiedChatRequest { ConversationId = req.ConvKey, Messages = messages, Stream = false };
				var r = await _llm.GetResponseAsync(ureq, cts.Token).ConfigureAwait(false);
				if (!r.IsSuccess)
				{
					Verse.Log.Warning("[RimAI.Core][P7.Persona] bio.gen failed entity=" + entityId + " err=" + r.Error);
					return new List<RimAI.Core.Source.Modules.Persona.BiographyItem>();
				}
				var text = r.Value?.Message?.Content ?? string.Empty;
				var normalized = (text ?? string.Empty).Replace("\r", string.Empty);
				var maxPer = _cfg?.GetInternal()?.Persona?.Budget?.BiographyPerItem ?? 400;
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
			var ureq = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = conv, Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage> { new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=sys }, new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=user } }, Stream = false };
			var r = await _llm.GetResponseAsync(ureq, ct).ConfigureAwait(false);
			return r.IsSuccess ? (r.Value?.Message?.Content ?? string.Empty) : string.Empty;
		}

		private static int? TryParsePawnId(string entityId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(entityId)) return null;
				if (entityId.StartsWith("pawn:"))
				{
					if (int.TryParse(entityId.Substring(5), out var id)) return id;
				}
				return null;
			}
			catch { return null; }
		}
	}
}


