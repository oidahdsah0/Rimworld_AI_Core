using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class EnvBeautyComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 94;
		public string Id => "env_beauty";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				if (ctx?.Request?.PawnLoadId != null)
				{
					var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
					var avg = await world.GetPawnBeautyAverageAsync(ctx.Request.PawnLoadId.Value, 9, ct).ConfigureAwait(false);
					var title = ctx?.L?.Invoke("prompt.section.env_beauty", "[Env-Beauty Nearby]") ?? "[Env-Beauty Nearby]";
					lines.Add(title + " " + avg.ToString("F1"));
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


