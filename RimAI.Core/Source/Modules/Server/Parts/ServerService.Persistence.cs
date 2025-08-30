using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public ServerState ExportSnapshot()
		{
			var state = new ServerState();
			foreach (var kv in _servers)
			{
				state.Items[kv.Key] = Clone(kv.Value);
			}
			return state;
		}

		public void ImportSnapshot(ServerState state)
		{
			_servers.Clear();
			if (state?.Items == null) return;
			foreach (var kv in state.Items)
			{
				var rec = kv.Value ?? new ServerRecord { EntityId = kv.Key, Level = 1 };
				rec.InspectionIntervalHours = Math.Max(6, rec.InspectionIntervalHours <= 0 ? 24 : rec.InspectionIntervalHours);
				_servers[kv.Key] = Clone(rec);
			}
		}

		private static ServerRecord Clone(ServerRecord s)
		{
			return new ServerRecord
			{
				EntityId = s.EntityId,
				Level = s.Level,
				SerialHex12 = s.SerialHex12,
				BuiltAtAbsTicks = s.BuiltAtAbsTicks,
				BaseServerPersonaOverride = s.BaseServerPersonaOverride,
				BaseServerPersonaPresetKey = s.BaseServerPersonaPresetKey,
				InspectionIntervalHours = s.InspectionIntervalHours,
				InspectionEnabled = s.InspectionEnabled,
				NextSlotPointer = s.NextSlotPointer,
				InspectionSlots = (s.InspectionSlots ?? new List<InspectionSlot>()).Select(x => x == null ? null : new InspectionSlot { Index = x.Index, ToolName = x.ToolName, Enabled = x.Enabled, LastRunAbsTicks = x.LastRunAbsTicks, NextDueAbsTicks = x.NextDueAbsTicks }).ToList(),
				ServerPersonaSlots = (s.ServerPersonaSlots ?? new List<ServerPersonaSlot>()).Select(x => x == null ? null : new ServerPersonaSlot { Index = x.Index, PresetKey = x.PresetKey, OverrideText = x.OverrideText, Enabled = x.Enabled }).ToList(),
				LastSummaryText = s.LastSummaryText,
				LastSummaryAtAbsTicks = s.LastSummaryAtAbsTicks
			};
		}
	}
}
