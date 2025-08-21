using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Stage
{
	internal sealed class StageEnvironmentComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.Stage;
		public int Order => 20;
		public string Id => "stage_env";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				int centerPawnId = 0;
				var list = ctx?.Request?.ParticipantIds ?? Array.Empty<string>();
				foreach (var id in list)
				{
					if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("pawn:")) continue;
					if (int.TryParse(id.Substring(5), out var pid)) { centerPawnId = pid; break; }
				}
				if (centerPawnId <= 0) { return new ComposerOutput { SystemLines = Array.Empty<string>(), ContextBlocks = Array.Empty<ContextBlock>() }; }

				var weatherTask = world.GetWeatherStatusAsync(centerPawnId, ct);
				var beautyTask = world.GetPawnBeautyAverageAsync(centerPawnId, 3, ct);
				var terrainTask = world.GetPawnTerrainCountsAsync(centerPawnId, 3, ct);
				var colonyTask = world.GetColonySnapshotAsync(centerPawnId, ct);
				await Task.WhenAll(weatherTask, beautyTask, terrainTask, colonyTask).ConfigureAwait(false);

				var weather = weatherTask.Result;
				var beauty = beautyTask.Result;
				var terr = terrainTask.Result ?? Array.Empty<RimAI.Core.Source.Modules.World.TerrainCountItem>();
				var topTerr = string.Join("/", terr.OrderByDescending(x => x.Count).Take(2).Select(x => x.Terrain));
				var colony = colonyTask.Result;

				var args = new Dictionary<string, string>
				{
					{ "weather", weather?.Weather ?? string.Empty },
					{ "temp", weather != null ? weather.OutdoorTempC.ToString("F1") : string.Empty },
					{ "glow", weather != null ? ((int)(weather.Glow * 100)).ToString() + "%" : string.Empty },
					{ "beauty", beauty.ToString("F1") },
					{ "terrains", string.IsNullOrWhiteSpace(topTerr) ? "-" : topTerr },
					{ "colony", colony?.ColonyName ?? string.Empty },
					{ "pop", (colony?.ColonistCount ?? 0).ToString() }
				};
				string line = null;
				try { line = ctx?.F?.Invoke("stage.groupchat.env.line", args, null); } catch { line = null; }
				if (string.IsNullOrWhiteSpace(line))
				{
					line = $"天气{args["weather"]}，体感{args["temp"]}°C，光照{args["glow"]}；附近美观≈{args["beauty"]}；地貌要点：{args["terrains"]}；殖民地{args["colony"]}（{args["pop"]}人）";
				}
				lines.Add(line);
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() };
		}
	}
}


