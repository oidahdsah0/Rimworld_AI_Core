using System;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Server;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerListSidebar
	{
		public static void Draw(Rect rect, IServerService server, ref Vector2 scroll, Action<string> onSelect, Action onBackToChat, Action onPersona, Action onInterComms, Action onHistory)
		{
			Widgets.DrawMenuSection(rect);
			var list = server?.List() ?? new System.Collections.Generic.List<RimAI.Core.Source.Modules.Persistence.Snapshots.ServerRecord>();
			var view = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f - 120f);
			Widgets.BeginScrollView(view, ref scroll, new Rect(0f, 0f, view.width - 16f, Mathf.Max(view.height, list.Count * 60f + 4f)));
			float y = 2f;
			for (int i = 0; i < list.Count; i++)
			{
				var s = list[i];
				var row = new Rect(0f, y, view.width - 20f, 56f);
				if (Widgets.ButtonText(row, $"#{Safe3(s.SerialHex12)} Lv{s.Level} {ShortPersona(s)}")) { onSelect?.Invoke(s.EntityId); }
				y += 60f;
			}
			Widgets.EndScrollView();
			var btnH = 26f; var pad = 6f; float bx = rect.x + 6f; float by = rect.yMax - pad - btnH * 4 - 6f;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.BackToChat".Translate())) { onBackToChat?.Invoke(); }
			by += btnH + pad;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.Persona".Translate())) { onPersona?.Invoke(); }
			by += btnH + pad;
			GUI.color = new Color(0.95f, 0.35f, 0.35f);
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.InterComms".Translate())) { onInterComms?.Invoke(); }
			GUI.color = Color.white;
			by += btnH + pad;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.History".Translate())) { onHistory?.Invoke(); }
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


