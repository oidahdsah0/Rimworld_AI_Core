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
            // P13: 将 Server 状态合入（可开关）
            try { _composers.Add(new Composers.ChatUI.ServerStatusComposer(RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>())); } catch { }
            _composers.Add(new Composers.ChatUI.UserPrefixComposer());

            // Stage Scope：群聊（环境 + 参与者摘要）
            _composers.Add(new Composers.Stage.StageEnvironmentComposer());
            _composers.Add(new Composers.Stage.StageParticipantsComposer());

            // ServerStage Scope：服务器群聊（服务器事实 + 合约约束）
            _composers.Add(new Composers.ServerStage.ServerStageServerFactsComposer());
            _composers.Add(new Composers.ServerStage.ServerStageContractComposer());
            // 复用 ChatUI 的 ColonyStatus 到 ServerStage（用 Scope 适配器包一层）
            _composers.Add(new ScopedComposerAdapter(new Composers.ChatUI.ColonyStatusComposer(), PromptScope.ServerStage, idOverride: "server_colony_status", orderOverride: 60));

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
			// Locale 选择优先级：request.Locale > 配置覆盖 PromptLocaleOverride > 本地化服务默认 > 配置 General.Locale > en
			var locale = string.IsNullOrWhiteSpace(request.Locale)
				? (!string.IsNullOrWhiteSpace(_cfg?.GetInternal()?.General?.PromptLocaleOverride)
					? _cfg.GetInternal().General.PromptLocaleOverride
					: (_loc?.GetDefaultLocale() ?? _cfg?.GetInternal()?.General?.Locale ?? "en"))
				: request.Locale;
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

			return new PromptBuildResult
			{
				SystemPrompt = sb.ToString(),
				ContextBlocks = blocks,
				UserPrefixedInput = ctx.L("ui.chat.user_prefix", "{player_title}传来的信息如下：").Replace("{player_title}", ctx.PlayerTitle ?? (ctx.L("ui.chat.player_title.value", "总督")))
			};
		}

		private IReadOnlyList<string> GetEnabledComposerIds(PromptScope scope)
		{
			try
			{
				var group = _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Composers;
				var ids = (scope == PromptScope.ChatUI ? group?.ChatUI?.Enabled : System.Array.Empty<string>()) ?? Array.Empty<string>();
				if (ids == null || ids.Length == 0) return Array.Empty<string>();
				var list = new List<string>();
				foreach (var id in ids) if (!string.IsNullOrWhiteSpace(id)) list.Add(id.Trim());
				return list;
			}
			catch { return Array.Empty<string>(); }
		}

		private int GetTopRelations()
		{
			try { return _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.TopRelations ?? 5; } catch { return 5; }
		}
		private int GetRecentEvents()
		{
			try { return _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.RecentEvents ?? 5; } catch { return 5; }
		}

		private static List<ContextBlock> BudgetBlocks(List<ContextBlock> src, int maxTotalChars)
		{
			var list = new List<ContextBlock>();
			int acc = 0;
			for (int i = 0; i < src.Count; i++)
			{
				var b = src[i];
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


