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
				string playerName = string.Empty;
				try { playerName = await _world.GetPlayerNameAsync(cts.Token); } catch { playerName = string.Empty; }
				var locale = _cfg?.GetInternal()?.Persona?.Locale ?? "zh-Hans";
				var seg = _cfg?.GetInternal()?.Persona?.Budget?.IdeologySegment ?? 600;
				var tpls = await _templates.GetTemplatesAsync(locale, cts.Token);
				var prompt = tpls?.Prompts?.ideology ?? (locale.StartsWith("zh") ? "你是设定编辑。为角色生成四段文本：世界观/价值观/行为准则/性格特质（各≤{maxSeg}字），语言紧凑一致。信息: {facts}" : "Generate four segments: worldview/values/code-of-conduct/traits (<= {maxSeg} chars). Facts: {facts}");
				var sys = locale.StartsWith("zh") ? "你是设定编辑。" : "You are a setting editor.";
				var user = prompt.Replace("{maxSeg}", seg.ToString()).Replace("{facts}", "player=" + playerName);
				var conv = $"persona-ideology-{entityId}";
				var r = await _llm.GetResponseAsync(conv, sys, user, cts.Token);
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

		private static string SafeCap(string text, int cap)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			var t = text.Trim();
			if (t.Length > cap) t = t.Substring(0, cap);
			return t;
		}
	}
}


