using System.Diagnostics;

namespace RimAI.Core.Source.Modules.Tooling
{
	internal static class ToolLogging
	{
		public static void Info(string message) => Debug.WriteLine($"[RimAI.Core][P4.Tool] {message}");
		public static void Warn(string message) => Debug.WriteLine($"[RimAI.Core][P4.Tool][WARN] {message}");
		public static void Error(string message, System.Exception ex = null) => Debug.WriteLine($"[RimAI.Core][P4.Tool][ERR] {message} {(ex!=null?ex.ToString():string.Empty)}");
	}
}


