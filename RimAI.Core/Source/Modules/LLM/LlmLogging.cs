using System;

namespace RimAI.Core.Source.Modules.LLM
{
	internal static class LlmLogging
	{
		public static string HashConversationId(string conversationId)
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
	}
}


