using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public void SetBaseServerPersonaPreset(string entityId, string presetKey)
		{
			var s = GetOrThrow(entityId);
			s.BaseServerPersonaPresetKey = presetKey;
		}

		public void SetBaseServerPersonaOverride(string entityId, string overrideText)
		{
			var s = GetOrThrow(entityId);
			s.BaseServerPersonaOverride = overrideText;
		}

		public void SetServerPersonaSlot(string entityId, int slotIndex, string presetKey, string overrideText = null)
		{
			var s = GetOrThrow(entityId);
			var cap = GetPersonaCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureServerPersonaSlots(s, cap);
			s.ServerPersonaSlots[slotIndex] = new ServerPersonaSlot { Index = slotIndex, PresetKey = presetKey, OverrideText = overrideText, Enabled = true };
		}

		public void ClearServerPersonaSlot(string entityId, int slotIndex)
		{
			var s = GetOrThrow(entityId);
			var cap = GetPersonaCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureServerPersonaSlots(s, cap);
			s.ServerPersonaSlots[slotIndex] = new ServerPersonaSlot { Index = slotIndex, PresetKey = null, OverrideText = null, Enabled = false };
		}

		public IReadOnlyList<ServerPersonaSlot> GetServerPersonaSlots(string entityId)
		{
			var s = GetOrThrow(entityId);
			EnsureServerPersonaSlots(s, GetPersonaCapacity(s.Level));
			return s.ServerPersonaSlots.OrderBy(x => x.Index).ToList();
		}

		private static List<string> BuildServerPersonaLines(ServerRecord s, ServerPromptPreset preset)
		{
			var lines = new List<string>();
			bool hasSlots = s.ServerPersonaSlots != null && s.ServerPersonaSlots.Any(x => x != null && x.Enabled && (!string.IsNullOrWhiteSpace(x.OverrideText) || !string.IsNullOrWhiteSpace(x.PresetKey)));
			if (hasSlots)
			{
				foreach (var slot in s.ServerPersonaSlots.OrderBy(x => x.Index))
				{
					if (slot == null || !slot.Enabled) continue;
					if (!string.IsNullOrWhiteSpace(slot.OverrideText)) { lines.Add(slot.OverrideText); continue; }
					if (!string.IsNullOrWhiteSpace(slot.PresetKey))
					{
						var opt = preset?.ServerPersonaOptions?.FirstOrDefault(o => string.Equals(o.key, slot.PresetKey, StringComparison.OrdinalIgnoreCase));
						if (opt != null && !string.IsNullOrWhiteSpace(opt.text)) lines.Add(opt.text);
					}
				}
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaOverride)) lines.Add(s.BaseServerPersonaOverride);
				else if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaPresetKey))
				{
					var opt = preset?.ServerPersonaOptions?.FirstOrDefault(o => string.Equals(o.key, s.BaseServerPersonaPresetKey, StringComparison.OrdinalIgnoreCase));
					if (opt != null && !string.IsNullOrWhiteSpace(opt.text)) lines.Add(opt.text);
				}
				else if (!string.IsNullOrWhiteSpace(preset?.BaseServerPersonaText)) lines.Add(preset.BaseServerPersonaText);
			}
			return lines;
		}
	}
}
