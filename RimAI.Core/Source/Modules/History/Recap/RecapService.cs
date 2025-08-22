using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.LLM;

namespace RimAI.Core.Source.Modules.History.Recap
{
	internal sealed class RecapService : IRecapService
	{
		private readonly ConfigurationService _cfg;
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;

		private readonly ConcurrentDictionary<string, List<RecapItem>> _recaps = new ConcurrentDictionary<string, List<RecapItem>>();
		private readonly ConcurrentDictionary<string, long> _nextRecapDueAt = new ConcurrentDictionary<string, long>();
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();

		public event Action<string, string> OnRecapUpdated;

		public RecapService(IConfigurationService cfg, ILLMService llm, IHistoryService history)
		{
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("RecapService requires ConfigurationService");
			_llm = llm;
			_history = history;
			// 订阅 History 事件，避免在 HistoryService 内部直接依赖 RecapService，消除环依赖
			try
			{
				_history.OnEntryRecorded += (convKey, _) => { System.Threading.Tasks.Task.Run(() => EnqueueGenerateIfDueAsync(convKey)); };
				_history.OnEntryEdited += (convKey, _) => { try { MarkStale(convKey, null); } catch { } };
				_history.OnEntryDeleted += (convKey, _) => { try { MarkStale(convKey, null); } catch { } };
			}
			catch { }
		}

		public async Task EnqueueGenerateIfDueAsync(string convKey, CancellationToken ct = default)
		{
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var n = Math.Max(1, _cfg.GetInternal()?.History?.SummaryEveryNRounds ?? 5);
				var mode = (_cfg.GetInternal()?.History?.Recap?.Mode ?? "Append").Equals("Replace", StringComparison.OrdinalIgnoreCase) ? RecapMode.Replace : RecapMode.Append;
				var maxChars = Math.Max(1, _cfg.GetInternal()?.History?.Recap?.MaxChars ?? 1200);
				var budgetMs = Math.Max(1, _cfg.GetInternal()?.History?.Budget?.MaxLatencyMs ?? 5000);

				// 取最新 AI TurnOrdinal
				var all = await _history.GetAllEntriesAsync(convKey, ct).ConfigureAwait(false);
				var maxTurn = all.Where(e => e.Role == EntryRole.Ai && e.TurnOrdinal.HasValue).Select(e => e.TurnOrdinal.Value).DefaultIfEmpty(0).Max();
				if (maxTurn <= 0) return; // 没有 AI 最终输出

				var dueAt = _nextRecapDueAt.AddOrUpdate(convKey, ((maxTurn + n - 1) / n) * n, (_, v) => v); // 初始化为 ceil(max/n)*n
				bool progressed = false;
				while (maxTurn >= dueAt)
				{
					var fromExclusive = dueAt - n;
					var toInclusive = dueAt;
					await GenerateWindowAsync(convKey, all, mode, maxChars, fromExclusive, toInclusive, budgetMs, ct).ConfigureAwait(false);
					progressed = true;
					var _ = _nextRecapDueAt.AddOrUpdate(convKey, dueAt + n, (_, __) => dueAt + n);
					dueAt += n;
				}
				if (progressed)
				{
					var last = _recaps.TryGetValue(convKey, out var list) && list.Count > 0 ? list[list.Count - 1] : null;
					if (last != null) OnRecapUpdated?.Invoke(convKey, last.Id);
				}
			}
			finally { gate.Release(); }
		}

		public Task RebuildStaleAsync(string convKey, CancellationToken ct = default)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list.Count == 0) return Task.CompletedTask;
			var stale = list.Where(x => x.Stale).Select(x => (x.FromTurnExclusive, x.ToTurnInclusive)).ToList();
			if (stale.Count == 0) return Task.CompletedTask;
			return Task.Run(async () =>
			{
				var all = await _history.GetAllEntriesAsync(convKey).ConfigureAwait(false);
				var mode = (_cfg.GetInternal()?.History?.Recap?.Mode ?? "Append").Equals("Replace", StringComparison.OrdinalIgnoreCase) ? RecapMode.Replace : RecapMode.Append;
				var maxChars = Math.Max(1, _cfg.GetInternal()?.History?.Recap?.MaxChars ?? 1200);
				var budgetMs = Math.Max(1, _cfg.GetInternal()?.History?.Budget?.MaxLatencyMs ?? 5000);
				foreach (var (fromExclusive, toInclusive) in stale)
				{
					await GenerateWindowAsync(convKey, all, mode, maxChars, fromExclusive, toInclusive, budgetMs, ct).ConfigureAwait(false);
				}
			});
		}

		public async Task GenerateManualAsync(string convKey, CancellationToken ct = default)
		{
			// 取消无关 OK/Request 日志；如需，可在 UI 侧点击时记录
			var mode = (_cfg.GetInternal()?.History?.Recap?.Mode ?? "Append").Equals("Replace", StringComparison.OrdinalIgnoreCase) ? RecapMode.Replace : RecapMode.Append;
			var maxChars = Math.Max(1, _cfg.GetInternal()?.History?.Recap?.MaxChars ?? 1200);
			var budgetMs = Math.Max(1, _cfg.GetInternal()?.History?.Budget?.MaxLatencyMs ?? 5000);
			var all = await _history.GetAllEntriesAsync(convKey, ct).ConfigureAwait(false);
			// 带上最近一次的前情提要文本
			var existingRecapsText = ComposeExistingRecapsText(convKey, maxBudget: 2000);
			int manualStrategyVersion = unchecked((int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF));
			int lastEntries = GetManualLastEntriesCount();
			await GenerateFromLastEntriesAsync(convKey, all, mode, maxChars, lastEntries, budgetMs, ct, existingRecapsText, manualStrategyVersion).ConfigureAwait(false);
			// 不再打印 OK 确认日志
		}

		private int GetManualLastEntriesCount()
		{
			try { return Math.Max(1, _cfg.GetInternal()?.History?.Recap?.ManualLastEntries ?? 12); } catch { return 12; }
		}

		private async Task GenerateWindowAsync(string convKey, IReadOnlyList<HistoryEntry> all, RecapMode mode, int maxChars, long fromExclusive, long toInclusive, int budgetMs, CancellationToken ct)
		{
			// 自动生成也带上最近一次非空前情提要文本，供合并到首条 user 内容
			var existingRecapsText = ComposeExistingRecapsText(convKey, maxBudget: 2000);
			await GenerateWindowAsync(convKey, all, mode, maxChars, fromExclusive, toInclusive, budgetMs, ct, existingRecapsText, strategyVersion: 1).ConfigureAwait(false);
		}

		private async Task GenerateFromLastEntriesAsync(string convKey, IReadOnlyList<HistoryEntry> all, RecapMode mode, int maxChars, int lastEntries, int budgetMs, CancellationToken ct, string existingRecapsText, int strategyVersion)
		{
			if (all == null || all.Count == 0) return;
			var ordered = all.Where(x => !x.Deleted).OrderBy(x => x.Timestamp).ToList();
			var tail = ordered.Skip(Math.Max(0, ordered.Count - lastEntries)).ToList();
			long fromExclusive = 0;
			long toInclusive = 0;
			// 取尾部中最大的 AI 回合作为 toInclusive，fromExclusive = minTurn-1（若无 AI，直接返回空）
			var aiTurns = tail.Where(e => e.Role == EntryRole.Ai && e.TurnOrdinal.HasValue).Select(e => e.TurnOrdinal.Value).ToList();
			if (aiTurns.Count == 0) return;
			toInclusive = aiTurns.Max();
			var minTurnInTail = aiTurns.Min();
			fromExclusive = Math.Max(0, minTurnInTail - 1);
			await GenerateWindowAsync(convKey, all, mode, maxChars, fromExclusive, toInclusive, budgetMs, ct, existingRecapsText, strategyVersion).ConfigureAwait(false);
		}

		private async Task GenerateWindowAsync(string convKey, IReadOnlyList<HistoryEntry> all, RecapMode mode, int maxChars, long fromExclusive, long toInclusive, int budgetMs, CancellationToken ct, string existingRecapsText, int strategyVersion)
		{
			var idemp = ComputeIdempotencyKey(convKey, mode, fromExclusive, toInclusive, strategyVersion);
			var windowItems = all.Where(e => e.Role == EntryRole.Ai && e.TurnOrdinal.HasValue && e.TurnOrdinal.Value > fromExclusive && e.TurnOrdinal.Value <= toInclusive).ToList();
			if (windowItems.Count == 0)
			{
				UpsertRecap(convKey, new RecapItem
				{
					Id = Guid.NewGuid().ToString("N"),
					ConvKey = convKey,
					Mode = mode,
					Text = string.Empty,
					MaxChars = maxChars,
					FromTurnExclusive = fromExclusive,
					ToTurnInclusive = toInclusive,
					Stale = false,
					IdempotencyKey = idemp,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
				return;
			}

			// 构建 Messages List：system + 历史多轮（user/assistant 成对，基于窗口内 AI 回合，配最近一条用户发言）
			var sys = BuildRecapSystemPrompt(maxChars, existingRecapsText);
			var messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>();
			if (!string.IsNullOrWhiteSpace(sys))
			{
				messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = sys });
			}
			// 以时间顺序整合窗口内消息为一条 User 文本（U:/A:），不再逐条 list 传递
			windowItems = windowItems.OrderBy(e => e.Timestamp).ToList();
			DateTime? prevAiTime = null;
			int userLines = 0, aiLines = 0;
			var convSb = new StringBuilder();
			foreach (var ai in windowItems)
			{
				var usersBetween = all
					.Where(x => x.Role == EntryRole.User && x.Timestamp <= ai.Timestamp && (!prevAiTime.HasValue || x.Timestamp > prevAiTime.Value))
					.OrderBy(x => x.Timestamp)
					.ToList();
				foreach (var u in usersBetween)
				{
					if (!string.IsNullOrWhiteSpace(u.Content))
					{
						if (convSb.Length > 0) convSb.AppendLine();
						convSb.Append("U: ").Append(u.Content);
						userLines++;
					}
				}
				if (!string.IsNullOrWhiteSpace(ai.Content))
				{
					if (convSb.Length > 0) convSb.AppendLine();
					convSb.Append("A: ").Append(ai.Content);
					aiLines++;
				}
				prevAiTime = ai.Timestamp;
			}
			// 将“上一条提要”置于用户内容开头并标注
			var mergedSb = new StringBuilder();
			if (!string.IsNullOrWhiteSpace(existingRecapsText))
			{
				mergedSb.AppendLine("[上次前情提要]");
				mergedSb.AppendLine(existingRecapsText.Trim());
				mergedSb.AppendLine();
			}
			mergedSb.Append(convSb.ToString());
			messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "user", Content = mergedSb.ToString() });
			try { Verse.Log.Message($"[RimAI.Core][Recap] Build payload conv={convKey} windowAI={windowItems.Count} usersAdded={userLines} aiAdded={aiLines}"); } catch { }
			// 固定打印 Payload 详情，便于对齐 LLM 输入
			try
			{
				var logSb = new StringBuilder();
				logSb.AppendLine($"[RimAI.Core][Recap] Payload conv={convKey} range={fromExclusive + 1}..{toInclusive} total={messages.Count}");
				for (int i = 0; i < messages.Count; i++)
				{
					var m = messages[i];
					logSb.AppendLine($"[{i}] {m?.Role}: {m?.Content}");
				}
				Verse.Log.Message(logSb.ToString());
			}
			catch { }

			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(budgetMs);
				var convId = $"recap:{convKey}:{fromExclusive}-{toInclusive}";
				var req = new RimAI.Framework.Contracts.UnifiedChatRequest
				{
					ConversationId = convId,
					Messages = messages,
					Stream = false
				};
				var r = await _llm.GetResponseAsync(req, cts.Token).ConfigureAwait(false);
				var text = string.Empty;
				bool success = r != null && r.IsSuccess && r.Value != null;
				if (success)
				{
					try { text = r.Value.Message?.Content ?? string.Empty; } catch { text = string.Empty; }
				}
				// 删除兜底：若为空则保留为空，不再贴原始记录
				if (text.Length > maxChars) text = text.Substring(0, maxChars);
				UpsertRecap(convKey, new RecapItem
				{
					Id = Guid.NewGuid().ToString("N"),
					ConvKey = convKey,
					Mode = mode,
					Text = text,
					MaxChars = maxChars,
					FromTurnExclusive = fromExclusive,
					ToTurnInclusive = toInclusive,
					Stale = false,
					IdempotencyKey = idemp,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
			}
		}

        // 回退策略已移除：若 LLM 返回空，则保持空文本，避免贴原始对话

//		private static string TrimTo(string s, int n) => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= n ? s : s.Substring(0, n)); // no longer used

		private static string ComputeIdempotencyKey(string convKey, RecapMode mode, long fromExclusive, long toInclusive, int strategyVersion)
		{
			var raw = $"{convKey}|{mode}|{fromExclusive}|{toInclusive}|{strategyVersion}";
			return HashString(raw);
		}

		private static string HashString(string s)
		{
			unchecked
			{
				uint hash = 2166136261;
				for (int i = 0; i < s.Length; i++)
				{
					hash ^= s[i];
					hash *= 16777619;
				}
				return hash.ToString("x8");
			}
		}

		private void UpsertRecap(string convKey, RecapItem item)
		{
			var list = _recaps.GetOrAdd(convKey, _ => new List<RecapItem>());
			string notifyId = null;
			lock (list)
			{
				var existing = list.FirstOrDefault(x => x.IdempotencyKey == item.IdempotencyKey);
				if (existing == null)
				{
					list.Add(item);
					notifyId = item.Id;
				}
				else
				{
					existing.Text = item.Text;
					existing.UpdatedAt = DateTime.UtcNow;
					existing.Stale = false;
					notifyId = existing.Id;
				}
			}
			try { OnRecapUpdated?.Invoke(convKey, notifyId); } catch { }
		}

		private string BuildRecapSystemPrompt(int maxChars, string existingRecaps)
		{
			try
			{
				var loc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
				var locale = _cfg?.GetInternal()?.General?.PromptLocaleOverride ?? loc?.GetDefaultLocale() ?? "en";
				var sb = new StringBuilder();
				var baseLine = loc?.Format(locale, "recap.system.base", new System.Collections.Generic.Dictionary<string, string> { { "max", maxChars.ToString() } }, $"You are a dialogue recap assistant. In {maxChars} chars, summarize key facts and progress as bullet-like lines; keep numbers; avoid filler.") ?? $"You are a dialogue recap assistant. In {maxChars} chars, summarize key facts and progress as bullet-like lines; keep numbers; avoid filler.";
				sb.Append(baseLine).Append('\n');
				if (!string.IsNullOrWhiteSpace(existingRecaps))
				{
					var prevLabel = loc?.Get(locale, "recap.system.prev_label", "[Previous Recap]") ?? "[Previous Recap]";
					sb.AppendLine(prevLabel);
					sb.AppendLine(existingRecaps);
				}
				return sb.ToString();
			}
			catch
			{
				var sb = new StringBuilder();
				sb.Append($"You are a dialogue recap assistant. In {maxChars} chars, summarize key facts and progress as bullet-like lines; keep numbers; avoid filler.\n");
				if (!string.IsNullOrWhiteSpace(existingRecaps))
				{
					sb.AppendLine("[Previous Recap]");
					sb.AppendLine(existingRecaps);
				}
				return sb.ToString();
			}
		}

		private string ComposeExistingRecapsText(string convKey, int maxBudget)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list == null || list.Count == 0) return null;
			RecapItem last;
			lock (list)
			{
				last = list
					.OrderBy(x => x.FromTurnExclusive)
					.ThenBy(x => x.ToTurnInclusive)
					.LastOrDefault(x => !string.IsNullOrWhiteSpace(x.Text));
			}
			if (last == null) return null;
			var s = last.Text?.Trim();
			if (string.IsNullOrEmpty(s)) return null;
			if (s.Length > maxBudget) s = s.Substring(0, maxBudget);
			return s;
		}

		public Task ForceRebuildAsync(string convKey, CancellationToken ct = default)
		{
			// 将 due 重置到最近的桶边界，等待 EnqueueGenerate 来处理
			var n = Math.Max(1, _cfg.GetInternal()?.History?.SummaryEveryNRounds ?? 5);
			var _ = _nextRecapDueAt.AddOrUpdate(convKey, n, (_, __) => n);
			return Task.CompletedTask;
		}

		public IReadOnlyList<RecapItem> GetRecaps(string convKey)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list == null) return Array.Empty<RecapItem>();
			lock (list)
			{
				return (IReadOnlyList<RecapItem>)list
					.OrderBy(x => x.FromTurnExclusive)
					.ThenBy(x => x.ToTurnInclusive)
					.ToList();
			}
		}

		public bool UpdateRecap(string convKey, string recapId, string newText)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list == null) return false;
			bool updated = false;
			lock (list)
			{
				var r = list.FirstOrDefault(x => x.Id == recapId);
				if (r == null) return false;
				r.Text = newText ?? string.Empty;
				r.UpdatedAt = DateTime.UtcNow;
				updated = true;
			}
			if (updated)
			{
				try { OnRecapUpdated?.Invoke(convKey, recapId); } catch { }
			}
			return updated;
		}

		public bool DeleteRecap(string convKey, string recapId)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list == null) return false;
			bool removed = false;
			lock (list)
			{
				var idx = list.FindIndex(x => x.Id == recapId);
				if (idx < 0) return false;
				list.RemoveAt(idx);
				removed = true;
			}
			if (removed)
			{
				try { OnRecapUpdated?.Invoke(convKey, recapId); } catch { }
			}
			return removed;
		}

		public RecapSnapshot ExportSnapshot()
		{
			var snap = new RecapSnapshot();
			foreach (var kv in _recaps)
			{
				snap.Items[kv.Key] = kv.Value.ToList();
			}
			return snap;
		}

		public void ImportSnapshot(RecapSnapshot snapshot)
		{
			_recaps.Clear();
			if (snapshot?.Items == null) return;
			foreach (var kv in snapshot.Items)
			{
				_recaps[kv.Key] = kv.Value?.ToList() ?? new List<RecapItem>();
			}
			// 读档后重算水位：ceil(maxTurn / N) * N
			var n = Math.Max(1, _cfg.GetInternal()?.History?.SummaryEveryNRounds ?? 5);
			foreach (var convKey in _recaps.Keys)
			{
				var all = _history.GetAllEntriesAsync(convKey).GetAwaiter().GetResult();
				var maxTurn = all.Where(e => e.Role == EntryRole.Ai && e.TurnOrdinal.HasValue).Select(e => e.TurnOrdinal.Value).DefaultIfEmpty(0).Max();
				var due = ((maxTurn + n - 1) / n) * n;
				_nextRecapDueAt[convKey] = Math.Max(due, n);
			}
		}

		public void MarkStale(string convKey, long? affectedTurnOrdinal = null)
		{
			if (!_recaps.TryGetValue(convKey, out var list) || list == null) return;
			lock (list)
			{
				foreach (var r in list)
				{
					if (affectedTurnOrdinal == null || (r.FromTurnExclusive < affectedTurnOrdinal && affectedTurnOrdinal <= r.ToTurnInclusive))
					{
						r.Stale = true;
					}
				}
			}
		}
	}
}



