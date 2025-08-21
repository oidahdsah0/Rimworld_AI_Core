using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.History
{
	internal sealed class StageHistorySink
	{
		private readonly IHistoryService _history;
		private readonly ConfigurationService _cfg;

		public StageHistorySink(IHistoryService history, IConfigurationService cfg)
		{
			_history = history;
			_cfg = cfg as ConfigurationService;
		}

		public bool TryWrite(ActResult result, string actName = null, string convKey = null)
		{
			try
			{
				var stage = _cfg?.GetInternal()?.Stage ?? new CoreConfig.StageSection();
				var stageConvKey = stage.History?.StageLogConvKey ?? "agent:stage";
				var body = result?.FinalText ?? string.Empty;
				if (body.Length > (stage.History?.MaxFinalTextChars ?? 800)) body = body.Substring(0, Math.Max(0, (stage.History?.MaxFinalTextChars ?? 800)));
				var entry = $"Stage:{actName ?? ""}";
				var speaker = "agent:stage";
				_ = Task.Run(async () =>
				{
					try { await _history.AppendRecordAsync(stageConvKey, entry, speaker, "chat", body, advanceTurn: false, ct: CancellationToken.None).ConfigureAwait(false); }
					catch { }
				});
				return true;
			}
			catch { return false; }
		}
	}
}


