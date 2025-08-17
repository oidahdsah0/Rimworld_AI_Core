using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Persona.Templates;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Source.Modules.Persona.Ideology
{
	internal sealed class IdeologyService : IIdeologyService
	{
		private readonly ILLMService _llm;
		private readonly IWorldDataService _world;
		private readonly ConfigurationService _cfg;
		private readonly Persona.IPersonaService _personaFacade;
		private readonly IPersonaTemplateManager _templates;

		public IdeologyService(ILLMService llm, IWorldDataService world, IConfigurationService cfg, Persona.IPersonaService personaFacade, IPersonaTemplateManager templates)
		{ _llm = llm; _world = world; _cfg = cfg as ConfigurationService; _personaFacade = personaFacade; _templates = templates; }

		public RimAI.Core.Source.Modules.Persona.IdeologySnapshot Get(string entityId)
		{ return _personaFacade.Get(entityId)?.Ideology ?? new RimAI.Core.Source.Modules.Persona.IdeologySnapshot(); }

		public void Set(string entityId, RimAI.Core.Source.Modules.Persona.IdeologySnapshot s)
		{ _personaFacade.Upsert(entityId, e => e.SetIdeology(s?.Worldview, s?.Values, s?.CodeOfConduct, s?.TraitsText)); }

		public async Task<RimAI.Core.Source.Modules.Persona.IdeologySnapshot> GenerateAsync(string entityId, CancellationToken ct = default)
		{
			var timeoutMs = Math.Max(1000, _cfg?.GetInternal()?.Persona?.Generation?.TimeoutMs ?? 15000);
			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(timeoutMs);
				// 使用 PromptService 统一组装（Scope=PersonaIdeology）
				var seg = _cfg?.GetInternal()?.Persona?.Budget?.IdeologySegment ?? 600;
				var prompting = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Prompting.IPromptService>();
				var req = new RimAI.Core.Source.Modules.Prompting.Models.PromptBuildRequest
				{
					Scope = RimAI.Core.Source.Modules.Prompting.Models.PromptScope.PersonaIdeology,
					ConvKey = $"persona-ideology-{entityId}",
					ParticipantIds = new System.Collections.Generic.List<string> { entityId, "player:persona" },
					PawnLoadId = TryParsePawnId(entityId),
					IsCommand = false,
					Locale = _cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans",
					UserInput = string.Empty
				};
				var prompt = await prompting.BuildAsync(req, cts.Token).ConfigureAwait(false);
				try { Verse.Log.Message("[RimAI.Core][P7] Persona Ideology Payload\nconv=" + req.ConvKey + "\n--- System ---\n" + (prompt?.SystemPrompt ?? string.Empty) + "\n--- User ---\n" + (prompt?.UserPrefixedInput ?? string.Empty)); } catch { }
				var ureq = new UnifiedChatRequest { ConversationId = req.ConvKey, Messages = new System.Collections.Generic.List<ChatMessage> { new ChatMessage{ Role="system", Content = prompt?.SystemPrompt ?? string.Empty }, new ChatMessage{ Role="user", Content = prompt?.UserPrefixedInput ?? string.Empty } }, Stream = false };
				var r = await _llm.GetResponseAsync(ureq, cts.Token).ConfigureAwait(false);
				if (!r.IsSuccess)
				{
					Verse.Log.Warning($"[RimAI.Core][P7.Persona] ideo.gen failed entity={entityId} err={r.Error}");
					return new RimAI.Core.Source.Modules.Persona.IdeologySnapshot();
				}
				var text = r.Value?.Message?.Content ?? string.Empty;
				var snapshot = new RimAI.Core.Source.Modules.Persona.IdeologySnapshot();
				// 简单按分隔符切分，UI 可编辑修正
				var parts = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length > 0) snapshot.Worldview = SafeCap(parts[0], seg);
				if (parts.Length > 1) snapshot.Values = SafeCap(parts[1], seg);
				if (parts.Length > 2) snapshot.CodeOfConduct = SafeCap(parts[2], seg);
				if (parts.Length > 3) snapshot.TraitsText = SafeCap(parts[3], seg);
				snapshot.UpdatedAtUtc = DateTime.UtcNow;
				return snapshot;
			}
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

		private static string SafeCap(string text, int cap)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			var t = text.Trim();
			if (t.Length > cap) t = t.Substring(0, cap);
			return t;
		}
	}
}


