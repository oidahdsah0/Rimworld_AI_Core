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
			var lines = new System.Collections.Generic.List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var s = await world.GetWeatherStatusAsync(ctx?.Request?.PawnLoadId ?? 0, ct).ConfigureAwait(false);
				if (s != null)
				{
					var title = ctx?.L?.Invoke("prompt.section.weather", "[Weather]") ?? "[Weather]";
					var tempLabel = ctx?.L?.Invoke("prompt.label.temp", "Temp") ?? "Temp";
					var glowLabel = ctx?.L?.Invoke("prompt.label.glow", "Glow") ?? "Glow";
					lines.Add($"{title} {s.Weather} | {tempLabel}: {s.OutdoorTempC:F1}°C | {glowLabel}: {s.Glow:P0}");
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}


