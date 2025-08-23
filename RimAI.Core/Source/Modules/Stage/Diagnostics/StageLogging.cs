using Verse;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;

namespace RimAI.Core.Source.Modules.Stage.Diagnostics
{
	internal sealed class StageLogging : IStageLogging
	{
		private readonly ConfigurationService _cfg;
		public StageLogging() { _cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService; }
		private bool IsEnabled()
		{
			try { return _cfg?.GetInternal()?.Stage?.Logging?.DebugEnabled ?? true; } catch { return true; }
		}
		public void Info(string message)
		{
			if (!IsEnabled()) return;
			Log.Message("[RimAI.Core][P9.Stage] " + message);
		}

		public void Warn(string message)
		{
			if (!IsEnabled()) return;
			Log.Warning("[RimAI.Core][P9.Stage] " + message);
		}

		public void Error(string message)
		{
			if (!IsEnabled()) return;
			Log.Error("[RimAI.Core][P9.Stage] " + message);
		}
	}
}


