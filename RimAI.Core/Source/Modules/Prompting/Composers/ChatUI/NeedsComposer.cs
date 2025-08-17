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
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var n = await world.GetNeedsAsync(ctx?.Request?.PawnLoadId ?? 0, ct).ConfigureAwait(false);
				if (n != null)
				{
					var title = ctx?.L?.Invoke("prompt.section.needs", "[需求]") ?? "[需求]";
					lines.Add($"{title} 饮食:{n.Food:P0} 休息:{n.Rest:P0} 娱乐:{n.Recreation:P0} 美观:{n.Beauty:P0} 室内:{n.Indoors:P0} 心情:{n.Mood:P0}");
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


