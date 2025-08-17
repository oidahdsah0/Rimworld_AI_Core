using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class NeedStatesComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 84;
		public string Id => "need_states";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var list = await world.GetMoodThoughtOffsetsAsync(ctx?.Request?.PawnLoadId ?? 0, 10, ct).ConfigureAwait(false);
				if (list != null && list.Count > 0)
				{
					var title = ctx?.L?.Invoke("prompt.section.need_states", "[需求状态]") ?? "[需求状态]";
					var items = list.Select(t => $"{t.Label}({(t.MoodOffset > 0 ? "+" : string.Empty)}{t.MoodOffset})");
					lines.Add(title + " " + string.Join("；", items));
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


