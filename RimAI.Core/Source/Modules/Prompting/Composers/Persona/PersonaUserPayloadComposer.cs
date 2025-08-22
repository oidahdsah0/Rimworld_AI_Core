using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Relations;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Persona
{
	internal sealed class PersonaUserPayloadComposer : IPromptComposer, IProvidesUserPayload
	{
		private readonly PromptScope _scope;
		private readonly IRelationsService _relations;
		private readonly IHistoryService _history;
		public PersonaUserPayloadComposer(PromptScope scope)
		{
			_scope = scope;
			try { _relations = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IRelationsService>(); } catch { _relations = null; }
			try { _history = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IHistoryService>(); } catch { _history = null; }
		}

		public PromptScope Scope => _scope;
		public int Order => 9999; // 放到最后，仅提供User
		public string Id => _scope == PromptScope.PersonaBiography ? "persona_biography_user" : "persona_ideology_user";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			// 不生成 SystemLines/Blocks
			return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() });
		}

		PromptScope IProvidesUserPayload.Scope => _scope;

		public Task<string> BuildUserPayloadAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var sb = new StringBuilder();

			// 复用 ChatUI 各 composer 的输出行，拼为单段文本
			var tmpComposers = new List<IPromptComposer>();
			// 通用基础信息
			tmpComposers.Add(new ChatUI.PawnIdentityComposer());
			tmpComposers.Add(new ChatUI.PawnBackstoryComposer());
			tmpComposers.Add(new ChatUI.PawnTraitsComposer());
			tmpComposers.Add(new ChatUI.PawnSkillsComposer());
			tmpComposers.Add(new ChatUI.PawnBeliefComposer());
			tmpComposers.Add(new ChatUI.PersonaJobComposer());
			tmpComposers.Add(new ChatUI.HealthAverageComposer());
			tmpComposers.Add(new ChatUI.HediffComposer());
			// 需求：
			// - 在 PersonaBiography 的 user 段放入“世界观四段”（排除传记）
			// - 在 PersonaIdeology 的 user 段放入“个人传记”（排除世界观四段）
			if (_scope == PromptScope.PersonaBiography)
			{
				// 仅加入世界观四段
				tmpComposers.Add(new ChatUI.PersonaIdeologyComposer());
			}
			else if (_scope == PromptScope.PersonaIdeology)
			{
				// 仅加入个人传记
				tmpComposers.Add(new ChatUI.PersonaBiographyComposer());
			}
			// 其他上下文
			tmpComposers.Add(new ChatUI.PawnSocialRelationsComposer());
			// 关联对话（同参与者的相关会话摘录）
			if (_relations != null && _history != null) tmpComposers.Add(new ChatUI.RelatedConversationsComposer(_relations, _history));
			tmpComposers.Add(new ChatUI.HistoryRecapComposer());
			tmpComposers.Add(new ChatUI.PawnSocialHistoryComposer());
			// 世界信息（按 ChatUI 详尽程度保留）
			tmpComposers.Add(new ChatUI.WeatherComposer());
			tmpComposers.Add(new ChatUI.ColonyStatusComposer());
			tmpComposers.Add(new Common.GameLogComposer(_scope, 30));
			tmpComposers.Add(new ChatUI.EnvBeautyComposer());
			tmpComposers.Add(new ChatUI.EnvTerrainComposer());
			tmpComposers.Add(new ChatUI.CurrentJobComposer());
			tmpComposers.Add(new ChatUI.ApparelComposer());
			tmpComposers.Add(new ChatUI.NeedsComposer());
			tmpComposers.Add(new ChatUI.NeedStatesComposer());
			// 固定提示词视作个人设定信息
			tmpComposers.Add(new ChatUI.PersonaFixedPromptComposer());

			foreach (var c in tmpComposers.OrderBy(c => c.Order))
			{
				if (ct.IsCancellationRequested) break;
				var outp = c.ComposeAsync(ctx, ct).GetAwaiter().GetResult();
				// 这些 ChatUI composer 都是把可读行放在 SystemLines 中
				if (outp?.SystemLines != null)
				{
					foreach (var line in outp.SystemLines)
					{
						if (!string.IsNullOrWhiteSpace(line))
						{
							if (sb.Length > 0) sb.AppendLine();
							sb.Append(line);
						}
					}
				}
				// 同时合并 ContextBlocks（Activities 等），进入 user 段
				if (outp?.ContextBlocks != null)
				{
					foreach (var b in outp.ContextBlocks)
					{
						var title = b?.Title ?? string.Empty;
						var text = b?.Text ?? string.Empty;
						if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text)) continue;
						bool singleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
						if (sb.Length > 0) sb.AppendLine();
						if (!string.IsNullOrWhiteSpace(title) && singleLine)
						{
							sb.Append(title + " " + text);
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
							sb.Append(text ?? string.Empty);
						}
					}
				}
			}

			// 合入与玩家的最近 10 条聊天记录（若存在）
			try
			{
				var recentChat = BuildRecentChatWithPlayerAsync(ctx, maxLines: 10, ct).GetAwaiter().GetResult();
				if (!string.IsNullOrWhiteSpace(recentChat))
				{
					if (sb.Length > 0) sb.AppendLine();
					var title = ctx?.L?.Invoke("prompt.section.recent_chat", "[Recent Chat]") ?? "[Recent Chat]";
					sb.AppendLine(title);
					sb.Append(recentChat);
				}
			}
			catch { }

			return Task.FromResult(sb.ToString());
		}

		private async Task<string> BuildRecentChatWithPlayerAsync(PromptBuildContext ctx, int maxLines, CancellationToken ct)
		{
			try
			{
				var entityId = ctx?.EntityId;
				if (string.IsNullOrWhiteSpace(entityId) || _relations == null || _history == null) return string.Empty;
				var convs = await _relations.ListByParticipantAsync(entityId, ct).ConfigureAwait(false);
				if (convs == null || convs.Count == 0) return string.Empty;
				string pick = null;
				System.DateTime lastTs = System.DateTime.MinValue;
				foreach (var ck in convs)
				{
					if (string.IsNullOrWhiteSpace(ck) || ck.IndexOf("player:") < 0) continue;
					var all = await _history.GetAllEntriesAsync(ck, ct).ConfigureAwait(false);
					var t = all?.Where(e => e != null && e.Timestamp != default).Select(e => e.Timestamp).DefaultIfEmpty(System.DateTime.MinValue).Max() ?? System.DateTime.MinValue;
					if (t > lastTs) { lastTs = t; pick = ck; }
				}
				if (string.IsNullOrWhiteSpace(pick)) return string.Empty;
				var list2 = await _history.GetAllEntriesAsync(pick, ct).ConfigureAwait(false);
				if (list2 == null || list2.Count == 0) return string.Empty;
				var lines = new List<string>();
				foreach (var e in list2.Where(x => x.Role == EntryRole.User || x.Role == EntryRole.Ai).OrderBy(x => x.Timestamp).TakeLast(maxLines))
				{
					var prefix = e.Role == EntryRole.User ? "U: " : "A: ";
					var content = e.Content ?? string.Empty;
					if (!string.IsNullOrWhiteSpace(content)) lines.Add(prefix + content);
				}
				return string.Join("\n", lines);
			}
			catch { return string.Empty; }
		}
	}
}


