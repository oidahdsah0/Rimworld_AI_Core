using System;
using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Orchestration
{
	internal static class OrchestrationLogging
	{
		public static string HashConv(string conversationId)
		{
			if (string.IsNullOrEmpty(conversationId)) return "-";
			unchecked
			{
				var hash = 23;
				foreach (var ch in conversationId)
				{
					hash = hash * 31 + ch;
				}
				return hash.ToString("X");
			}
		}

		public static string SummarizeScores(IReadOnlyList<(string name, double score)> list)
		{
			if (list == null || list.Count == 0) return "[]";
			var top = Math.Min(list.Count, 5);
			var parts = new List<string>(top);
			for (int i = 0; i < top; i++)
			{
				parts.Add(list[i].name + ":" + list[i].score.ToString("F2"));
			}
			return "[" + string.Join(", ", parts) + "]";
		}
	}
}


