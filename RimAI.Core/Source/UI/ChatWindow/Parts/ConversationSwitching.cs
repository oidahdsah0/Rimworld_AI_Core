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
				// 仅当现有会话的参与者集合“与目标完全一致”时才重用（严格 1:1：pawn+player），排除任何 stage/server/thing/多参与者会话
				var wanted = new System.Collections.Generic.List<string>(); foreach (var id in participantIds) { if (!string.IsNullOrWhiteSpace(id)) wanted.Add(id); }
				wanted.Sort(System.StringComparer.Ordinal);
				if (wanted.Count != 2) return fallbackConvKey; // 仅支持 1:1
				if (!(wanted[0].StartsWith("pawn:") && wanted[1].StartsWith("player:"))) return fallbackConvKey;
				var all = history.GetAllConvKeys(); if (all == null || all.Count == 0) return fallbackConvKey;
				foreach (var ck in all)
				{
					if (ck != null && ck.StartsWith("agent:stage|", System.StringComparison.Ordinal)) continue; // 排除 Stage 会话
					var parts = history.GetParticipantsOrEmpty(ck) ?? new System.Collections.Generic.List<string>();
					var list = new System.Collections.Generic.List<string>(); foreach (var p in parts) { if (!string.IsNullOrWhiteSpace(p)) list.Add(p); }
					list.Sort(System.StringComparer.Ordinal);
					if (list.Count != 2) continue;
					if (list[0].StartsWith("agent:") || list[0].StartsWith("thing:") || list[0].StartsWith("server:")) continue;
					if (list[1].StartsWith("agent:") || list[1].StartsWith("thing:") || list[1].StartsWith("server:")) continue;
					if (string.Equals(list[0], wanted[0], System.StringComparison.Ordinal) && string.Equals(list[1], wanted[1], System.StringComparison.Ordinal)) return ck;
				}
			}
			catch { }
			return fallbackConvKey;
		}
	}
}


