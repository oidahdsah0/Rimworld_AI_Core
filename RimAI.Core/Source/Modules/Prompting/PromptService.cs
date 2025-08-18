using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Modules.History.Relations;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.Modules.Prompting.Composers.Adapters;

namespace RimAI.Core.Source.Modules.Prompting
{
	internal sealed class PromptService : IPromptService
	{
		private readonly IConfigurationService _cfgPublic;
		private readonly ConfigurationService _cfg;
		private readonly IWorldDataService _world;
		private readonly IPersonaService _persona;
		private readonly IHistoryService _history;
		private readonly IRecapService _recap;
		private readonly List<IPromptComposer> _composers;
        private readonly IRelationsService _relations;
        private readonly ILocalizationService _loc;

		public PromptService(
			IConfigurationService cfg,
			IWorldDataService world,
			IPersonaService persona,
			IHistoryService history,
			IRecapService recap,
            IRelationsService relations,
            ILocalizationService localization
		)
		{
			_cfgPublic = cfg;
			_cfg = cfg as ConfigurationService;
			_world = world;
			_persona = persona;
			_history = history;
			_recap = recap;
            _relations = relations;
            _loc = localization;
            _composers = new List<IPromptComposer>();
            // 内置 ChatUI 作曲器（最小集），可后续按配置裁剪与扩展
            _composers.Add(new Composers.ChatUI.SystemBaseComposer());
            _composers.Add(new Composers.ChatUI.PlayerTitleComposer());
            _composers.Add(new Composers.ChatUI.PawnIdentityComposer());
            _composers.Add(new Composers.ChatUI.PawnBackstoryComposer());
            _composers.Add(new Composers.ChatUI.PawnBeliefComposer());
            _composers.Add(new Composers.ChatUI.PawnTraitsComposer());
            _composers.Add(new Composers.ChatUI.PawnSkillsComposer());
            _composers.Add(new Composers.ChatUI.WeatherComposer());
            _composers.Add(new Composers.ChatUI.CurrentJobComposer());
            _composers.Add(new Composers.ChatUI.ApparelComposer());
            _composers.Add(new Composers.ChatUI.NeedsComposer());
            _composers.Add(new Composers.ChatUI.NeedStatesComposer());
            _composers.Add(new Composers.ChatUI.ColonyStatusComposer());
            _composers.Add(new Composers.ChatUI.HealthAverageComposer());
            _composers.Add(new Composers.ChatUI.HediffComposer());
            _composers.Add(new Composers.ChatUI.PersonaJobComposer());
            _composers.Add(new Composers.ChatUI.PersonaBiographyComposer());
            _composers.Add(new Composers.ChatUI.PersonaIdeologyComposer());
            _composers.Add(new Composers.ChatUI.PersonaFixedPromptComposer());
            _composers.Add(new Composers.ChatUI.PawnSocialRelationsComposer());
            _composers.Add(new Composers.ChatUI.HistoryRecapComposer());
            _composers.Add(new Composers.ChatUI.RelatedConversationsComposer(_relations, _history));
            _composers.Add(new Composers.ChatUI.PawnSocialHistoryComposer());
            _composers.Add(new Composers.Common.GameLogComposer(PromptScope.ChatUI, 30));
            _composers.Add(new Composers.ChatUI.EnvBeautyComposer());
            _composers.Add(new Composers.ChatUI.EnvTerrainComposer());
            _composers.Add(new Composers.ChatUI.UserPrefixComposer());

            // PersonaBiography Scope：仅系统提示 + 单段User
            var scopeBio = PromptScope.PersonaBiography;
            _composers.Add(new Composers.Persona.PersonaBiographySystemComposer());
            _composers.Add(new Composers.Persona.PersonaUserPayloadComposer(scopeBio));
            _composers.Add(new Composers.Common.GameLogComposer(PromptScope.PersonaBiography, 30));

            // PersonaIdeology Scope：仅系统提示 + 单段User
            var scopeIdeo = PromptScope.PersonaIdeology;
            _composers.Add(new Composers.Persona.PersonaIdeologySystemComposer());
            _composers.Add(new Composers.Persona.PersonaUserPayloadComposer(scopeIdeo));
            _composers.Add(new Composers.Common.GameLogComposer(PromptScope.PersonaIdeology, 30));
		}

		public async Task<PromptBuildResult> BuildAsync(PromptBuildRequest request, CancellationToken ct = default)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			var locale = string.IsNullOrWhiteSpace(request.Locale) ? (_cfg?.GetInternal()?.General?.Locale ?? "zh-Hans") : request.Locale;
			var entityId = request.PawnLoadId.HasValue ? ($"pawn:{request.PawnLoadId.Value}") : null;

			// 预取快照（按需）
			var pawnPromptTask = request.PawnLoadId.HasValue ? _world.GetPawnPromptSnapshotAsync(request.PawnLoadId.Value, ct) : Task.FromResult<RimAI.Core.Source.Modules.World.PawnPromptSnapshot>(null);
			var pawnHealthTask = request.PawnLoadId.HasValue ? _world.GetPawnHealthSnapshotAsync(request.PawnLoadId.Value, ct) : Task.FromResult<RimAI.Core.Source.Modules.World.PawnHealthSnapshot>(null);
			var pawnSocialTask = request.PawnLoadId.HasValue ? _world.GetPawnSocialSnapshotAsync(request.PawnLoadId.Value, GetTopRelations(), GetRecentEvents(), ct) : Task.FromResult<RimAI.Core.Source.Modules.World.PawnSocialSnapshot>(null);
			var recapsTask = string.IsNullOrEmpty(request.ConvKey) ? Task.FromResult((IReadOnlyList<RecapItem>)Array.Empty<RecapItem>()) : Task.Run(() => (IReadOnlyList<RecapItem>)_recap.GetRecaps(request.ConvKey), ct);
			var threadTask = string.IsNullOrEmpty(request.ConvKey) ? Task.FromResult((IReadOnlyList<HistoryEntry>)Array.Empty<HistoryEntry>()) : _history.GetAllEntriesAsync(request.ConvKey, ct);
			var personaSnap = entityId == null ? null : _persona.Get(entityId);

			await Task.WhenAll(pawnPromptTask, pawnSocialTask, pawnHealthTask, recapsTask, threadTask).ConfigureAwait(false);

			var ctx = new PromptBuildContext
			{
				Request = request,
				Locale = locale,
				EntityId = entityId,
				PawnPrompt = pawnPromptTask.Result,
				PawnSocial = pawnSocialTask.Result,
				Persona = personaSnap,
				PawnHealth = pawnHealthTask.Result,
				Recaps = recapsTask.Result,
				RecentThread = threadTask.Result,
				EnvMatrix = null,
				PlayerTitle = string.IsNullOrWhiteSpace(_cfg?.GetPlayerTitleOrDefault()) 
					? (_loc?.Get(locale, "ui.chat.player_title.value", "总督") ?? "总督")
					: _cfg.GetPlayerTitleOrDefault(),
				L = (key, fb) => GetString(locale, key, fb),
				F = (key, args, fb) => { try { return _loc?.Format(locale, key, args, fb) ?? fb; } catch { return fb; } }
			};

			var enabled = GetEnabledComposerIds(request.Scope);
			var ordered = _composers
				.Where(c => c.Scope == request.Scope && ((enabled.Count == 0 || enabled.Contains(c.Id)) || c.Id == "system_base" || (c is IProvidesUserPrefix)))
				.OrderBy(c => c.Order)
				.ToList();
			var sysLines = new List<string>();
			var blocks = new List<ContextBlock>();
			foreach (var comp in ordered)
			{
				try
				{
					var outp = await comp.ComposeAsync(ctx, ct).ConfigureAwait(false);
					if (outp?.SystemLines != null) sysLines.AddRange(outp.SystemLines.Where(s => !string.IsNullOrWhiteSpace(s)));
					if (outp?.ContextBlocks != null) blocks.AddRange(outp.ContextBlocks.Where(b => b != null && (!string.IsNullOrWhiteSpace(b.Title) || !string.IsNullOrWhiteSpace(b.Text))));
				}
				catch (OperationCanceledException) { throw; }
				catch (Exception)
				{
					// 保守：单个作曲器失败不影响整体
				}
			}

			var sb = new StringBuilder();
			if (request.Scope == PromptScope.ChatUI && blocks != null && blocks.Count > 0)
			{
				// 将 Activities（ContextBlocks）也并入 System 段，满足“ChatUI 下必须进入 System Prompt”的要求
				foreach (var b in blocks)
				{
					var title = b?.Title ?? string.Empty;
					var text = b?.Text ?? string.Empty;
					if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text)) continue;
					bool textIsSingleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
					if (textIsSingleLine)
					{
						sysLines.Add(string.IsNullOrWhiteSpace(title) ? text : (title + " " + text));
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(title)) sysLines.Add(title);
						if (!string.IsNullOrWhiteSpace(text)) sysLines.Add(text);
					}
				}
			}
			// 合并外部传入的 RAG 块（如编排工具结果），并统一做预算裁剪
			if (request.ExternalBlocks != null && request.ExternalBlocks.Count > 0)
			{
				foreach (var b in request.ExternalBlocks)
				{
					if (b == null) continue;
					if (!string.IsNullOrWhiteSpace(b.Title) || !string.IsNullOrWhiteSpace(b.Text))
					{
						blocks.Add(b);
					}
				}
			}

			if (sysLines.Count > 0)
			{
				sb.Append(string.Join(Environment.NewLine, sysLines));
			}
			string userPrefix = null;
			// 从提供者作曲器获取用户前缀
			foreach (var comp in ordered)
			{
				if (comp is IProvidesUserPrefix up && up.Scope == request.Scope)
				{
					var pfx = up.GetUserPrefix(ctx) ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(pfx)) { userPrefix = pfx; break; }
				}
			}
			if (string.IsNullOrWhiteSpace(userPrefix))
			{
				var playerTitle = _cfg?.GetPlayerTitleOrDefault() ?? "总督";
				try { userPrefix = _loc?.Format(locale, "ui.chat.user_prefix", new Dictionary<string, string> { { "player_title", playerTitle } }, string.Empty) ?? string.Empty; }
				catch { userPrefix = GetString(locale, "ui.chat.user_prefix", string.Empty); }
			}

			// Persona Scope：覆盖为单段 User 文本
			string personaUser = null;
			if (request.Scope == PromptScope.PersonaBiography || request.Scope == PromptScope.PersonaIdeology)
			{
				foreach (var comp in ordered)
				{
					if (comp is IProvidesUserPayload up2 && up2.Scope == request.Scope)
					{
						try { personaUser = await up2.BuildUserPayloadAsync(ctx, ct).ConfigureAwait(false); }
						catch { personaUser = null; }
						if (!string.IsNullOrWhiteSpace(personaUser)) break;
					}
				}
			}

			var result = new PromptBuildResult
			{
				SystemPrompt = TrimToBudget(sb.ToString(), GetMaxSystemPromptChars()),
				ContextBlocks = TrimBlocks(blocks, GetBlocksBudgetChars()),
				UserPrefixedInput = (request.Scope == PromptScope.PersonaBiography || request.Scope == PromptScope.PersonaIdeology)
					? (personaUser ?? string.Empty)
					: (string.IsNullOrWhiteSpace(request.UserInput)
						? string.Empty
						: (string.IsNullOrWhiteSpace(userPrefix) ? request.UserInput : (userPrefix + " " + request.UserInput)))
			};
			return result;
		}

		private int GetMaxSystemPromptChars() => Math.Max(200, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.MaxSystemPromptChars ?? 1600);
		private int GetBlocksBudgetChars() => Math.Max(400, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.MaxBlocksChars ?? 2400);
		private int GetTopRelations() => Math.Max(0, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.TopRelations ?? 5);
		private int GetRecentEvents() => Math.Max(0, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.RecentEvents ?? 5);
		private int GetEnvRadius() => Math.Max(1, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Env?.Radius ?? 9);

		/* removed EnvMatrix enrichment */
		private HashSet<string> GetEnabledComposerIds(PromptScope scope)
		{
			try
			{
				var arr = scope == PromptScope.ChatUI ? _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Composers?.ChatUI?.Enabled : null;
				return arr == null ? new HashSet<string>() : new HashSet<string>(arr);
			}
			catch { return new HashSet<string>(); }
		}

		private static string TrimToBudget(string s, int max)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			return s.Length <= max ? s : s.Substring(0, max);
		}

		private static IReadOnlyList<ContextBlock> TrimBlocks(List<ContextBlock> blocks, int maxTotalChars)
		{
			if (blocks == null || blocks.Count == 0) return Array.Empty<ContextBlock>();
			int acc = 0;
			var list = new List<ContextBlock>(blocks.Count);
			foreach (var b in blocks)
			{
				var text = b.Text ?? string.Empty;
				var title = b.Title ?? string.Empty;
				var need = title.Length + 1 + text.Length;
				if (acc + need <= maxTotalChars)
				{
					list.Add(b);
					acc += need;
				}
				else
				{
					var allow = Math.Max(0, maxTotalChars - acc - title.Length - 1);
					if (allow <= 0) break;
					list.Add(new ContextBlock { Title = title, Text = text.Substring(0, Math.Min(text.Length, allow)) });
					break;
				}
			}
			return list;
		}

		private string GetString(string locale, string key, string fallback)
		{
			try { return _loc?.Get(locale, key, fallback) ?? fallback; } catch { return fallback; }
		}
	}
}


