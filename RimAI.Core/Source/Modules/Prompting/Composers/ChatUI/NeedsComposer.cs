using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class NeedsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 83;
		public string Id => "needs";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new System.Collections.Generic.List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var n = await world.GetNeedsAsync(ctx?.Request?.PawnLoadId ?? 0, ct).ConfigureAwait(false);
				if (n != null)
				{
					var isZh = (ctx?.Locale ?? "en").StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);
					var title = ctx?.L?.Invoke("prompt.section.needs", isZh ? "[需求]" : "[Needs]") ?? (isZh ? "[需求]" : "[Needs]");
					var colon = isZh ? "：" : ": ";
					var sep = isZh ? " " : " ";
					var food = (isZh ? "饮食" : "Food") + colon + n.Food.ToString("P0");
					var rest = (isZh ? "休息" : "Rest") + colon + n.Rest.ToString("P0");
					var rec = (isZh ? "娱乐" : "Recreation") + colon + n.Recreation.ToString("P0");
					var beauty = (isZh ? "美观" : "Beauty") + colon + n.Beauty.ToString("P0");
					var indoors = (isZh ? "室内" : "Indoors") + colon + n.Indoors.ToString("P0");
					var mood = (isZh ? "心情" : "Mood") + colon + n.Mood.ToString("P0");
					lines.Add($"{title} {food}{sep}{rest}{sep}{rec}{sep}{beauty}{sep}{indoors}{sep}{mood}");
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


