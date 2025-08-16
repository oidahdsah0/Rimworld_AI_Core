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
            _composers.Add(new Composers.ChatUI.PawnIdentityComposer());
            _composers.Add(new Composers.ChatUI.PawnBackstoryComposer());
            _composers.Add(new Composers.ChatUI.PawnTraitsComposer());
            _composers.Add(new Composers.ChatUI.PawnSkillsComposer());
            _composers.Add(new Composers.ChatUI.PersonaJobComposer());
            _composers.Add(new Composers.ChatUI.PersonaBiographyComposer());
            _composers.Add(new Composers.ChatUI.PersonaIdeologyComposer());
            _composers.Add(new Composers.ChatUI.PersonaFixedPromptComposer());
            _composers.Add(new Composers.ChatUI.PawnSocialRelationsComposer());
            _composers.Add(new Composers.ChatUI.HistoryRecapComposer());
            _composers.Add(new Composers.ChatUI.RelatedConversationsComposer(_relations, _history));
            _composers.Add(new Composers.ChatUI.PawnSocialHistoryComposer());
		}

		public async Task<PromptBuildResult> BuildAsync(PromptBuildRequest request, CancellationToken ct = default)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));
			var locale = string.IsNullOrWhiteSpace(request.Locale) ? (_cfg?.GetInternal()?.General?.Locale ?? "zh-Hans") : request.Locale;
			var entityId = request.PawnLoadId.HasValue ? ($"pawn:{request.PawnLoadId.Value}") : null;

			// 预取快照（按需）
			var pawnPromptTask = request.PawnLoadId.HasValue ? _world.GetPawnPromptSnapshotAsync(request.PawnLoadId.Value, ct) : Task.FromResult<RimAI.Core.Source.Modules.World.PawnPromptSnapshot>(null);
			var pawnSocialTask = request.PawnLoadId.HasValue ? _world.GetPawnSocialSnapshotAsync(request.PawnLoadId.Value, GetTopRelations(), GetRecentEvents(), ct) : Task.FromResult<RimAI.Core.Source.Modules.World.PawnSocialSnapshot>(null);
			var recapsTask = string.IsNullOrEmpty(request.ConvKey) ? Task.FromResult((IReadOnlyList<RecapItem>)Array.Empty<RecapItem>()) : Task.Run(() => (IReadOnlyList<RecapItem>)_recap.GetRecaps(request.ConvKey), ct);
			var personaSnap = entityId == null ? null : _persona.Get(entityId);

			await Task.WhenAll(pawnPromptTask, pawnSocialTask, recapsTask).ConfigureAwait(false);

			var ctx = new PromptBuildContext
			{
				Request = request,
				Locale = locale,
				EntityId = entityId,
				PawnPrompt = pawnPromptTask.Result,
				PawnSocial = pawnSocialTask.Result,
				Persona = personaSnap,
				Recaps = recapsTask.Result,
				L = (key, fb) => GetString(locale, key, fb),
				F = (key, args, fb) => { try { return _loc?.Format(locale, key, args, fb) ?? fb; } catch { return fb; } }
			};

			var enabled = GetEnabledComposerIds(request.Scope);
			var ordered = _composers.Where(c => c.Scope == request.Scope && (enabled.Count == 0 || enabled.Contains(c.Id))).OrderBy(c => c.Order).ToList();
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

			var baseSys = GetString(locale, "ui.chat.system.base", "你是一个Rimworld边缘世界的新领地殖民者，你的总督正在通过随身通信终端与你取得联系。");
			var sb = new StringBuilder();
			sb.Append(baseSys);
			if (sysLines.Count > 0)
			{
				sb.AppendLine();
				sb.AppendLine();
				sb.Append(string.Join(Environment.NewLine, sysLines));
			}
			var userPrefix = GetString(locale, "ui.chat.user_prefix", "总督传来的信息如下：");
			var result = new PromptBuildResult
			{
				SystemPrompt = TrimToBudget(sb.ToString(), GetMaxSystemPromptChars()),
				ContextBlocks = TrimBlocks(blocks, GetBlocksBudgetChars()),
				UserPrefixedInput = string.IsNullOrWhiteSpace(request.UserInput) ? string.Empty : (userPrefix + " " + request.UserInput)
			};
			return result;
		}

		private int GetMaxSystemPromptChars() => Math.Max(200, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.MaxSystemPromptChars ?? 1600);
		private int GetBlocksBudgetChars() => Math.Max(400, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.MaxBlocksChars ?? 2400);
		private int GetTopRelations() => Math.Max(0, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.TopRelations ?? 5);
		private int GetRecentEvents() => Math.Max(0, _cfg?.GetInternal()?.UI?.ChatWindow?.Prompts?.Social?.RecentEvents ?? 5);
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


