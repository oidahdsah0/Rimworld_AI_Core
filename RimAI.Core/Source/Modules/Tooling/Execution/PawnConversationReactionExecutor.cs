using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class PawnConversationReactionExecutor : IToolExecutor
	{
		public string Name => "pawn_conversation_reaction";

		public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			int delta = 0;
			string title = null;
			double durationDays = 3.0; // default fallback
			try { if (args != null && args.TryGetValue("mood_delta", out var v) && v != null) delta = Convert.ToInt32(v, CultureInfo.InvariantCulture); } catch { delta = 0; }
			try { if (args != null && args.TryGetValue("mood_title", out var t) && t != null) title = Convert.ToString(t, CultureInfo.InvariantCulture); } catch { title = null; }
			try { if (args != null && args.TryGetValue("duration_days", out var d) && d != null) durationDays = Convert.ToDouble(d, CultureInfo.InvariantCulture); } catch { durationDays = 3.0; }
			if (!double.IsFinite(durationDays)) durationDays = 3.0;
			durationDays = Math.Max(1.0, Math.Min(10.0, durationDays));
			// 服务器端强制裁剪
			delta = Math.Max(-30, Math.Min(30, delta));
			title = SanitizeTitle(title);

			var loc = RimAICoreMod.TryGetService<ILocalizationService>();
			string locale = loc?.GetDefaultLocale() ?? "zh-Hans";
			if (string.IsNullOrWhiteSpace(title))
			{
				title = loc?.Get(locale, "tool.pawn_reaction.default_title", "Chat Reaction") ?? "Chat Reaction";
			}

			// 记录到历史：轻量 JSON；不推进回合
			try
			{
				var history = RimAICoreMod.TryGetService<IHistoryService>();
				string convo = null;
				try { if (args != null && args.TryGetValue("conv_key", out var ck) && ck != null) convo = Convert.ToString(ck, CultureInfo.InvariantCulture); } catch { }
				if (string.IsNullOrWhiteSpace(convo)) convo = BuildConvKeyFromContext();
				var compact = new { type = "reaction", mood_delta = delta, mood_title = title, duration_days = durationDays };
				await history.AppendRecordAsync(convo, "ChatUI", "agent:reaction", "tool_call", Newtonsoft.Json.JsonConvert.SerializeObject(compact), advanceTurn: false, ct: ct).ConfigureAwait(false);
				// try { Verse.Log.Message($"[RimAI.Core][Reaction] Persisted conv={convo} delta={delta} title={title}"); } catch { }
			}
			catch { }

			// 冷却：同一 Pawn 在冷却期内不再写入记忆
			try
			{
				var was = RimAICoreMod.TryGetService<IWorldActionService>();
				int? pawnLoadId = TryGetFirstPawnLoadId(args);
				if (was != null && pawnLoadId.HasValue && pawnLoadId.Value > 0 && delta != 0)
				{
					// 0–24 小时随机冷却：若处于冷却，直接返回；否则应用并设置新的冷却
					if (!ReactionCooldown.TryEnter(pawnLoadId.Value))
					{
						try { Verse.Log.Message($"[RimAI.Core][Reaction] Cooldown active for pawn {pawnLoadId.Value}, skip."); } catch { }
					}
					else
					{
						int durationTicks = (int)Math.Round(durationDays * 60000.0);
						_ = was.TryApplyChatReactionAsync(pawnLoadId.Value, delta, title, durationTicks, ct);
					}
				}
			}
			catch { }

			return new { ok = true, applied_delta = delta, applied_title = title };
		}

		private static string SanitizeTitle(string input)
		{
			if (string.IsNullOrWhiteSpace(input)) return string.Empty;
			var s = input.Trim();
			// 去除装饰性引号/括号
			s = s.Trim('"', '\'', '“', '”', '「', '」', '【', '】', '[', ']', '(', ')', '{', '}', '<', '>');
			// CJK: 限 7 字符；非 CJK: 限 3 词
			if (ContainsCjk(s))
			{
				if (s.Length > 7) s = s.Substring(0, 7);
				return s;
			}
			// 非 CJK：以空白分词，保留前 3 词
			var parts = Regex.Split(s, "\\s+").Where(p => !string.IsNullOrWhiteSpace(p)).Take(3);
			return string.Join(" ", parts);
		}

		private static bool ContainsCjk(string s)
		{
			if (string.IsNullOrEmpty(s)) return false;
			foreach (var ch in s)
			{
				int code = ch;
				// 常见 CJK 范围
				if ((code >= 0x4E00 && code <= 0x9FFF) || (code >= 0x3400 && code <= 0x4DBF) || (code >= 0x3040 && code <= 0x30FF) || (code >= 0xAC00 && code <= 0xD7AF))
					return true;
			}
			return false;
		}

		private static string BuildConvKeyFromContext()
		{
			// 简化：暂无上下文注入，直接使用玩家维度会话键
			try
			{
				var world = RimAICoreMod.TryGetService<IWorldDataService>();
				var playerId = "player:local"; // 仅用于记录
				return playerId;
			}
			catch { return "player:local"; }
		}

		private static int? TryGetFirstPawnLoadId(Dictionary<string, object> args)
		{
			try
			{
				if (args == null) return null;
				if (!args.TryGetValue("participant_ids", out var v) || v == null) return null;
				var enumerable = v as System.Collections.IEnumerable;
				if (enumerable == null) return null;
				foreach (var o in enumerable)
				{
					var s = Convert.ToString(o, CultureInfo.InvariantCulture);
					if (string.IsNullOrWhiteSpace(s)) continue;
					if (s.StartsWith("pawn:"))
					{
						var tail = s.Substring("pawn:".Length);
						if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) return id;
					}
				}
				return null;
			}
			catch { return null; }
		}
	}
}
