using System;

namespace RimAI.Core.Source.UI.ServerChatWindow
{
	internal enum ServerTab
	{
		Chat,
		Persona,
		AiLog,
		History
	}

	internal sealed class ServerListItem
	{
		public int ThingId { get; set; }
		public int Level { get; set; }
		public string DisplayName { get; set; }
	}

	// 单一来源：服务器会话键与解析工具
	internal sealed class ServerConversationKey
	{
		public string Value { get; }
		public ServerConversationKey(string value) { Value = value ?? string.Empty; }
		public override string ToString() => Value;
	}

	internal static class ServerKeyUtil
	{
		public static string BuildForThingId(int thingId, string playerSessionId)
		{
			var pids = new System.Collections.Generic.List<string> { $"server:{thingId}", playerSessionId ?? string.Empty };
			pids.Sort(StringComparer.Ordinal);
			return string.Join("|", pids);
		}

		public static string NormalizeEntityId(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return null;
			if (entityId.StartsWith("server:", StringComparison.OrdinalIgnoreCase)) return entityId;
			if (entityId.StartsWith("thing:", StringComparison.OrdinalIgnoreCase)) return entityId;
			if (int.TryParse(entityId.Trim(), out var num)) return $"thing:{num}";
			return entityId;
		}

		public static int? TryParseThingId(string entityId)
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

		public static string BuildFallbackForEntity(string normalizedEntityId, string playerSessionId)
		{
			var pids = new System.Collections.Generic.List<string> { normalizedEntityId ?? string.Empty, playerSessionId ?? string.Empty };
			pids.Sort(StringComparer.Ordinal);
			return string.Join("|", pids);
		}
	}
}
