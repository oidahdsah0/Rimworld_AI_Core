using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Orchestration;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class ConversationSwitching
	{
		public static ChatController SwitchToPawn(ILLMService llm, IHistoryService history, IWorldDataService world, IOrchestrationService orchestration, Modules.Prompting.IPromptService prompting, string currentPlayerId, Verse.Pawn pawn)
		{
			var participantIds = new List<string>();
			participantIds.Add($"pawn:{pawn.thingIDNumber}");
			if (!string.IsNullOrEmpty(currentPlayerId)) participantIds.Add(currentPlayerId);
			participantIds.Sort(StringComparer.Ordinal);
			var convKey = string.Join("|", participantIds);
			TryBindExistingConversation(history, ref participantIds, ref convKey);
			return new ChatController(llm, history, world, orchestration, prompting, convKey, participantIds);
		}

		public static ChatController SwitchToConvKey(ILLMService llm, IHistoryService history, IWorldDataService world, IOrchestrationService orchestration, Modules.Prompting.IPromptService prompting, string convKey)
		{
			var parts = history.GetParticipantsOrEmpty(convKey) ?? new List<string>();
			var ids = new List<string>(parts); ids.Sort(StringComparer.Ordinal);
			return new ChatController(llm, history, world, orchestration, prompting, convKey, ids);
		}

		private static void TryBindExistingConversation(IHistoryService history, ref List<string> participantIds, ref string convKey)
		{
			try
			{
				if (history == null || participantIds == null || participantIds.Count == 0) return;
				string pawnId = null;
				foreach (var id in participantIds) { if (id != null && id.StartsWith("pawn:")) { pawnId = id; break; } }
				if (string.IsNullOrEmpty(pawnId)) return;
				var all = history.GetAllConvKeys(); if (all == null || all.Count == 0) return;
				foreach (var ck in all)
				{
					var parts = history.GetParticipantsOrEmpty(ck);
					bool hasPawn = false; foreach (var p in parts) { if (string.Equals(p, pawnId, StringComparison.Ordinal)) { hasPawn = true; break; } }
					if (!hasPawn) continue;
					convKey = ck;
					participantIds = new List<string>(parts); participantIds.Sort(StringComparer.Ordinal);
					return;
				}
			}
			catch { }
		}

		public static string TryReuseExistingConvKey(IHistoryService history, System.Collections.Generic.IReadOnlyList<string> participantIds, string fallbackConvKey)
		{
			try
			{
				// 尝试重用现有对话键
				if (history == null || participantIds == null || participantIds.Count == 0) return fallbackConvKey;
				// 查找当前 pawnId
				string pawnId = null; foreach (var id in participantIds) { if (id != null && id.StartsWith("pawn:")) { pawnId = id; break; } }
				if (string.IsNullOrEmpty(pawnId)) return fallbackConvKey;
				// 查找当前对话的所有参与者
				var all = history.GetAllConvKeys(); if (all == null || all.Count == 0) return fallbackConvKey;
				foreach (var ck in all)
				{
					var parts = history.GetParticipantsOrEmpty(ck);
					bool hasPawn = false; foreach (var p in parts) { if (string.Equals(p, pawnId, System.StringComparison.Ordinal)) { hasPawn = true; break; } }
					if (hasPawn) return ck;
				}
			}
			catch { }
			return fallbackConvKey;
		}
	}
}


