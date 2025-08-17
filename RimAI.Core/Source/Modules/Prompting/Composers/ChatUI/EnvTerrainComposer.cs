using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class EnvTerrainComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 95;
		public string Id => "env_terrain";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				if (ctx?.Request?.PawnLoadId != null)
				{
					var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
					var list = await world.GetPawnTerrainCountsAsync(ctx.Request.PawnLoadId.Value, 9, ct).ConfigureAwait(false);
					var title = ctx?.L?.Invoke("prompt.section.env_terrain", "[环境-周围地貌]") ?? "[环境-周围地貌]";
					var parts = (list ?? new List<RimAI.Core.Source.Modules.World.TerrainCountItem>()).OrderByDescending(x => x?.Count ?? 0).Take(6).Select(x => $"{x.Terrain}-{x.Count}");
					var text = string.Join(";", parts);
					if (!string.IsNullOrEmpty(text)) text += ";";
					lines.Add(title + text);
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


