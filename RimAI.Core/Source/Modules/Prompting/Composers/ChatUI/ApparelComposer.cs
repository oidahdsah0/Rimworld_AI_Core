using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class ApparelComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 82;
		public string Id => "apparel";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var list = await world.GetApparelAsync(ctx?.Request?.PawnLoadId ?? 0, 6, ct).ConfigureAwait(false);
				if (list != null && list.Count > 0)
				{
					var title = ctx?.L?.Invoke("prompt.section.apparel", "[Apparel]") ?? "[Apparel]";
					var items = list.Select(a => ctx?.F?.Invoke("prompt.format.apparel_item", new Dictionary<string, string>
					{
						{ "label", a.Label ?? string.Empty },
						{ "quality", a.Quality ?? string.Empty },
						{ "durability", a.DurabilityPercent.ToString() }
					}, $"{a.Label}({(string.IsNullOrWhiteSpace(a.Quality) ? string.Empty : a.Quality + "," )}{a.DurabilityPercent}%)") ?? $"{a.Label}({(string.IsNullOrWhiteSpace(a.Quality) ? string.Empty : a.Quality + "," )}{a.DurabilityPercent}%)");
					var sep = ctx?.L?.Invoke("prompt.punct.list_semicolon", "; ") ?? "; ";
					lines.Add(title + " " + string.Join(sep, items));
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


