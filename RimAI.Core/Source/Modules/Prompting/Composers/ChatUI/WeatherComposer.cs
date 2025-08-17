using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class WeatherComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 80;
		public string Id => "weather";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var s = await world.GetWeatherStatusAsync(ctx?.Request?.PawnLoadId ?? 0, ct).ConfigureAwait(false);
				if (s != null)
				{
					var title = ctx?.L?.Invoke("prompt.section.weather", "[天气]") ?? "[天气]";
					lines.Add($"{title} {s.Weather} | 温度: {s.OutdoorTempC:F1}°C | 光照: {s.Glow:P0}");
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


