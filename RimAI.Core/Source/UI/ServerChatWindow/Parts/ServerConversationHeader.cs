using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Server;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerConversationHeader
	{
		public static void Draw(Rect rect, IServerService server, string serverEntityId)
		{
			var rec = string.IsNullOrWhiteSpace(serverEntityId) ? null : server?.Get(serverEntityId);
			var title = rec == null ? "RimAI.ServerChatUI.Header.None".Translate().ToString() : $"#{Safe3(rec.SerialHex12)} Lv{rec.Level} {ShortPersona(rec)}";
			Text.Font = GameFont.Medium; Widgets.Label(rect, title); Text.Font = GameFont.Small;
		}

		private static string Safe3(string serial)
		{
			if (string.IsNullOrWhiteSpace(serial)) return "---";
			return serial.Length <= 3 ? serial : serial.Substring(0, 3);
		}

		private static string ShortPersona(RimAI.Core.Source.Modules.Persistence.Snapshots.ServerRecord s)
		{
			try
			{
				if (s == null) return string.Empty;
				if (s.ServerPersonaSlots != null)
				{
					foreach (var slot in s.ServerPersonaSlots)
					{
						if (slot != null && slot.Enabled && !string.IsNullOrWhiteSpace(slot.PresetKey)) return slot.PresetKey;
					}
				}
				if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaPresetKey)) return s.BaseServerPersonaPresetKey;
				return string.IsNullOrWhiteSpace(s.BaseServerPersonaOverride) ? string.Empty : "Custom";
			}
			catch { return string.Empty; }
		}
	}
}


