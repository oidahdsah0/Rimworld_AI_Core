using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	// 辅助/工具方法（保持无副作用）
	internal sealed partial class ServerService
	{
		private static string BuildServerHubConvKey()
		{
			try
			{
				var list = new List<string> { "agent:server_hub", "player:servers" };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_hub|player:servers"; }
		}

		// 巡检专属对话键：每台服务器独立线程
		// 规范：convKey = join('|', sort({ "agent:server_inspection", "server_inspection:<thingId>" }))
		private static string BuildServerInspectionConvKey(string entityId)
		{
			try
			{
				int? id = TryParseThingId(entityId);
				var p1 = "agent:server_inspection";
				var p2 = id.HasValue ? ($"server_inspection:{id.Value}") : ($"server_inspection:{(entityId ?? "unknown")}");
				var list = new List<string> { p1, p2 };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_inspection|server_inspection:unknown"; }
		}

		private static string TryMakeInspectionParticipant(string entityId)
		{
			try
			{
				int? id = TryParseThingId(entityId);
				return id.HasValue ? ($"server_inspection:{id.Value}") : ($"server_inspection:{(entityId ?? "unknown")}");
			}
			catch { return "server_inspection:unknown"; }
		}

		private static int? TryParseThingId(string entityId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(entityId)) return null;
				var s = entityId.Trim();
				if (int.TryParse(s, out var pure)) return pure;
				var lastIdx = s.LastIndexOf(':');
				if (lastIdx >= 0 && lastIdx + 1 < s.Length)
				{
					var tail = s.Substring(lastIdx + 1);
					if (int.TryParse(tail, out var id2)) return id2;
				}
				int end = s.Length - 1;
				while (end >= 0 && !char.IsDigit(s[end])) end--;
				if (end < 0) return null;
				int start = end;
				while (start >= 0 && char.IsDigit(s[start])) start--;
				start++;
				if (start <= end)
				{
					var numStr = s.Substring(start, end - start + 1);
					if (int.TryParse(numStr, out var id3)) return id3;
				}
			}
			catch { }
			return null;
		}

		private static int GetPersonaCapacity(int level) => level switch { 1 => 1, 2 => 2, _ => 3 };
		private static int GetInspectionCapacity(int level) => level switch { 1 => 1, 2 => 3, _ => 5 };

		private static void EnsureServerPersonaSlots(ServerRecord s, int cap)
		{
			if (s.ServerPersonaSlots == null) s.ServerPersonaSlots = new List<ServerPersonaSlot>();
			while (s.ServerPersonaSlots.Count < cap) s.ServerPersonaSlots.Add(new ServerPersonaSlot { Index = s.ServerPersonaSlots.Count, Enabled = false });
			if (s.ServerPersonaSlots.Count > cap) s.ServerPersonaSlots = s.ServerPersonaSlots.Take(cap).ToList();
		}

		private static void EnsureInspectionSlots(ServerRecord s, int cap)
		{
			if (s.InspectionSlots == null) s.InspectionSlots = new List<InspectionSlot>();
			while (s.InspectionSlots.Count < cap) s.InspectionSlots.Add(new InspectionSlot { Index = s.InspectionSlots.Count, Enabled = false });
			if (s.InspectionSlots.Count > cap) s.InspectionSlots = s.InspectionSlots.Take(cap).ToList();
		}

		private static string GenerateSerial()
		{
			var rnd = new Random();
			var sb = new System.Text.StringBuilder(12);
			for (int i = 0; i < 12; i++) sb.Append("0123456789ABCDEF"[rnd.Next(16)]);
			return sb.ToString();
		}

		private static int GetTicks()
		{
			try { return Verse.Find.TickManager.TicksGame; } catch { return 0; }
		}

		private static string FormatGameTime(int absTicks)
		{
			try { return absTicks.ToString(System.Globalization.CultureInfo.InvariantCulture); } catch { return absTicks.ToString(); }
		}

		private static float RandRange(float a, float b)
		{
			try { return (float)(a + (new Random().NextDouble()) * (b - a)); } catch { return (a + b) / 2f; }
		}
	}
}
