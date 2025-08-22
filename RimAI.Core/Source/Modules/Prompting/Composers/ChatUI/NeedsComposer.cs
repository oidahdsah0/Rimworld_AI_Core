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
					var title = ctx?.L?.Invoke("prompt.section.needs", "[Needs]") ?? "[Needs]";
					var colon = ctx?.L?.Invoke("prompt.punct.colon", ": ") ?? ": ";
					var sep = ctx?.L?.Invoke("prompt.punct.sep_item", " ") ?? " ";
					var food = (ctx?.L?.Invoke("prompt.label.need.food", "Food") ?? "Food") + colon + n.Food.ToString("P0");
					var rest = (ctx?.L?.Invoke("prompt.label.need.rest", "Rest") ?? "Rest") + colon + n.Rest.ToString("P0");
					var rec = (ctx?.L?.Invoke("prompt.label.need.recreation", "Recreation") ?? "Recreation") + colon + n.Recreation.ToString("P0");
					var beauty = (ctx?.L?.Invoke("prompt.label.need.beauty", "Beauty") ?? "Beauty") + colon + n.Beauty.ToString("P0");
					var indoors = (ctx?.L?.Invoke("prompt.label.need.indoors", "Indoors") ?? "Indoors") + colon + n.Indoors.ToString("P0");
					var mood = (ctx?.L?.Invoke("prompt.label.need.mood", "Mood") ?? "Mood") + colon + n.Mood.ToString("P0");
					lines.Add($"{title} {food}{sep}{rest}{sep}{rec}{sep}{beauty}{sep}{indoors}{sep}{mood}");
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


