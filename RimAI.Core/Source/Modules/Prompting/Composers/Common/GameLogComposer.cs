using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;
using System.Text.RegularExpressions;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Common
{
	internal sealed class GameLogComposer : IPromptComposer
	{
		private readonly PromptScope _scope;
		private readonly int _count;
		public GameLogComposer(PromptScope scope, int count)
		{
			_scope = scope;
			_count = count;
		}

		public PromptScope Scope => _scope;
		public int Order => 93; // 活动区靠前（天气/健康等之前）
		public string Id => "game_logs";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var logs = await world.GetRecentGameLogsAsync(_count, ct).ConfigureAwait(false);
				if (logs == null || logs.Count == 0) return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() };
				var title = ctx?.L?.Invoke("prompt.section.activities", "[活动]") ?? "[活动]";
				var items = new System.Collections.Generic.List<string>();
				int idx = 1;
				foreach (var it in logs.Take(_count))
				{
					var t = (it?.GameTime ?? string.Empty).Trim();
					var text = Sanitize(it?.Text ?? string.Empty);
					if (string.IsNullOrWhiteSpace(text)) continue;
					items.Add($"{idx}. {t}：{text}");
					idx++;
				}
				if (items.Count == 0) return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() };
				var oneLine = string.Join("；", items);
				var block = new ContextBlock { Title = title, Text = oneLine };
				return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = new[] { block } };
			}
			catch { return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() }; }
		}

		private static string Sanitize(string s)
		{
			if (string.IsNullOrEmpty(s)) return string.Empty;
			// 去掉富文本 color 标签
			s = Regex.Replace(s, "</?color[^>]*>", string.Empty, RegexOptions.IgnoreCase);
			return s;
		}
	}
}


