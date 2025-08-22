using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Server;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal sealed class ServerPersonaEditor
	{
		private Vector2 _scroll = Vector2.zero;
		private string _overrideText;
		private string _presetKey;

		public void Draw(Rect rect, IServerService server, string serverEntityId)
		{
			Widgets.DrawMenuSection(rect);
			var rec = server?.Get(serverEntityId);
			if (rec == null) { Widgets.Label(new Rect(rect.x + 6f, rect.y + 6f, rect.width - 12f, 24f), "No server selected"); return; }
			var view = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
			Widgets.BeginScrollView(view, ref _scroll, new Rect(0f, 0f, view.width - 16f, Mathf.Max(view.height, 420f)));
			float y = 6f;
			Widgets.Label(new Rect(0f, y, view.width - 16f, 24f), $"Entity: {rec.EntityId}  Level: {rec.Level}"); y += 28f;
			Widgets.Label(new Rect(0f, y, view.width - 16f, 24f), "Base Persona PresetKey:"); y += 24f;
			_presetKey = Widgets.TextField(new Rect(0f, y, Mathf.Min(280f, view.width - 16f), 26f), _presetKey ?? (rec.BaseServerPersonaPresetKey ?? string.Empty)); y += 32f;
			Widgets.Label(new Rect(0f, y, view.width - 16f, 24f), "Base Persona Override:"); y += 24f;
			_overrideText = Widgets.TextArea(new Rect(0f, y, Mathf.Min(480f, view.width - 16f), 100f), _overrideText ?? (rec.BaseServerPersonaOverride ?? string.Empty)); y += 108f;
			if (Widgets.ButtonText(new Rect(0f, y, 120f, 28f), "Save Base"))
			{
				try { if (_presetKey != null) server.SetBaseServerPersonaPreset(serverEntityId, _presetKey.Trim()); server.SetBaseServerPersonaOverride(serverEntityId, _overrideText ?? string.Empty); }
				catch { }
			}
			y += 36f;
			Widgets.Label(new Rect(0f, y, view.width - 16f, 24f), "Slots (index presetKey)"); y += 28f;
			var slots = server.GetServerPersonaSlots(serverEntityId);
			for (int i = 0; i < slots.Count; i++)
			{
				var s = slots[i];
				var row = new Rect(0f, y, view.width - 16f, 28f);
				Widgets.Label(new Rect(row.x, row.y + 4f, 40f, 20f), i.ToString());
				string key = s?.PresetKey ?? string.Empty;
				key = Widgets.TextField(new Rect(row.x + 50f, row.y, 180f, 26f), key);
				if (Widgets.ButtonText(new Rect(row.x + 240f, row.y, 90f, 26f), "Apply"))
				{
					try { server.SetServerPersonaSlot(serverEntityId, i, key, null); } catch { }
				}
				if (Widgets.ButtonText(new Rect(row.x + 340f, row.y, 90f, 26f), "Clear"))
				{
					try { server.ClearServerPersonaSlot(serverEntityId, i); } catch { }
				}
				y += 32f;
			}
			Widgets.EndScrollView();
		}
	}
}


