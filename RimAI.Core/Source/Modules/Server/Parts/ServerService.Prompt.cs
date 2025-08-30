using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public async Task<ServerPromptPack> BuildPromptAsync(string entityId, string locale, CancellationToken ct = default)
		{
			var s = GetOrThrow(entityId);
			var preset = await _presets.GetAsync(locale, ct).ConfigureAwait(false);
			var systemLines = new List<string>();
			// 服务器人格
			var personaLines = BuildServerPersonaLines(s, preset);
			if (personaLines.Count > 0) systemLines.AddRange(personaLines);
			// 环境变体
			var tempC = (await _world.GetAiServerSnapshotAsync(entityId, ct).ConfigureAwait(false))?.TemperatureC ?? 37;
			if (tempC < 30) { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_low)) systemLines.Add(preset.Env.temp_low); }
			else if (tempC < 70) { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_mid)) systemLines.Add(preset.Env.temp_mid); }
			else { if (!string.IsNullOrWhiteSpace(preset.Env?.temp_high)) systemLines.Add(preset.Env.temp_high); }
			// Server 基本属性
			systemLines.Add($"Server Level={s.Level}, Serial={s.SerialHex12}, BuiltAt={FormatGameTime(s.BuiltAtAbsTicks)}, Interval={s.InspectionIntervalHours}h");
			// ContextBlocks：最近一次汇总
			var blocks = new List<RimAI.Core.Source.Modules.Prompting.Models.ContextBlock>();
			if (!string.IsNullOrWhiteSpace(s.LastSummaryText))
			{
				blocks.Add(new RimAI.Core.Source.Modules.Prompting.Models.ContextBlock { Title = "最近一次巡检摘要", Text = s.LastSummaryText });
			}
			var temp = await GetRecommendedSamplingTemperatureAsync(entityId, ct).ConfigureAwait(false);
			return new ServerPromptPack { SystemLines = systemLines, ContextBlocks = blocks, SamplingTemperature = temp };
		}

		public async Task<float> GetRecommendedSamplingTemperatureAsync(string entityId, CancellationToken ct = default)
		{
			try
			{
				var s = await _world.GetAiServerSnapshotAsync(entityId, ct).ConfigureAwait(false);
				int t = s?.TemperatureC ?? 37;
				if (t < 30) return RandRange(0.9f, 1.2f);
				if (t < 70) return RandRange(1.2f, 1.5f);
				return 2.0f;
			}
			catch { return 1.2f; }
		}
	}
}
