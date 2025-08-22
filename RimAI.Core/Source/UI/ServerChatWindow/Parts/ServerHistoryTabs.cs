using System;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.UI.ChatWindow;
using RimAI.Core.Source.UI.ChatWindow.Parts;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal sealed class ServerHistoryTabs
	{
		private HistoryManagerTabView _inner;
		public void Draw(Rect inRect, string convKey)
		{
			if (_inner == null) _inner = new HistoryManagerTabView();
			var history = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IHistoryService>();
			var recap = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IRecapService>();
			_inner.Draw(inRect, new ChatConversationState { ConvKey = convKey, ParticipantIds = history.GetParticipantsOrEmpty(convKey) }, history, recap, ck => { });
		}
		public void ClearCache() { try { _inner?.ClearCache(); } catch { } }
	}
}


