using UnityEngine;

namespace RimAI.Core.Source.Modules.Stage.Diagnostics
{
	internal sealed class StageLogging : IStageLogging
	{
		public void Info(string message)
		{
			Debug.Log("[RimAI.Core][P9.Stage] " + message);
		}

		public void Warn(string message)
		{
			Debug.LogWarning("[RimAI.Core][P9.Stage] " + message);
		}

		public void Error(string message)
		{
			Debug.LogError("[RimAI.Core][P9.Stage] " + message);
		}
	}
}


