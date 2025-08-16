using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Relations;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class RelatedConversationsComposer : IPromptComposer
	{
		private readonly IRelationsService _relations;
		private readonly IHistoryService _history;

		public RelatedConversationsComposer(IRelationsService relations, IHistoryService history)
		{
			_relations = relations;
			_history = history;
		}

		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 95;
		public string Id => "related_conversations";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var blocks = new List<ContextBlock>();
			if (ctx?.Request?.ParticipantIds == null || string.IsNullOrEmpty(ctx.Request.ConvKey))
				return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks };

			// 取与当前参与者的超集/子集会话（分页各取一页）
			var super = await _relations.ListSupersetsAsync(ctx.Request.ParticipantIds, page: 1, pageSize: 2, ct).ConfigureAwait(false);
			var sub = await _relations.ListSubsetsAsync(ctx.Request.ParticipantIds, page: 1, pageSize: 2, ct).ConfigureAwait(false);
			var related = new HashSet<string>((super?.ConvKeys ?? new List<string>()).Concat(sub?.ConvKeys ?? new List<string>()));
			related.Remove(ctx.Request.ConvKey);
			int perConv = 3;
			foreach (var ck in related.Take(2))
			{
				var all = await _history.GetAllEntriesAsync(ck, ct).ConfigureAwait(false);
				var ai = all.Where(e => e.Role == EntryRole.Ai && e.TurnOrdinal.HasValue).TakeLast(perConv).ToList();
				if (ai.Count == 0) continue;
				var text = string.Join("\n", ai.Select(e => e.Content ?? string.Empty));
				blocks.Add(new ContextBlock { Title = $"[关联对话@{ck}]", Text = text });
			}
			return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks };
		}
	}
}


