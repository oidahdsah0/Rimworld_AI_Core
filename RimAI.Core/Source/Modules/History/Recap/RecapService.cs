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

		private async Task GenerateWindowAsync(string convKey, IReadOnlyList<HistoryEntry> all, RecapMode mode, int maxChars, long fromExclusive, long toInclusive, int budgetMs, CancellationToken ct)
		{
			var idemp = ComputeIdempotencyKey(convKey, mode, fromExclusive, toInclusive, 1);
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

			var sb = new StringBuilder();
			foreach (var e in windowItems)
			{
				sb.AppendLine($"[AI@{e.TurnOrdinal}] {TrimTo(e.Content, 300)}");
			}
			var input = sb.ToString();
			if (input.Length > 4000) input = input.Substring(0, 4000);

			var sys = "你是对话总结助手。";
			var user = $"请在 {maxChars} 字以内，用要点式总结以下对话关键事实与进展，避免复述无关闲聊：\n{input}\n输出要求：1) 精炼、客观；2) 保留具体数值/事实；3) 删去口头禅；4) 使用简短段落或条目。";

			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				cts.CancelAfter(budgetMs);
				var convId = $"recap:{convKey}:{fromExclusive}-{toInclusive}";
				var r = await _llm.GetResponseAsync(convId, sys, user, cts.Token).ConfigureAwait(false);
				var text = string.Empty;
				if (r != null && r.IsSuccess && r.Value != null)
				{
					try { text = r.Value.Message?.Content ?? string.Empty; } catch { text = string.Empty; }
				}
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

		private static string TrimTo(string s, int n) => string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= n ? s : s.Substring(0, n));

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
			var existing = list.FirstOrDefault(x => x.IdempotencyKey == item.IdempotencyKey);
			if (existing == null)
			{
				list.Add(item);
			}
			else
			{
				existing.Text = item.Text;
				existing.UpdatedAt = DateTime.UtcNow;
				existing.Stale = false;
			}
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
			return _recaps.TryGetValue(convKey, out var list) ? (IReadOnlyList<RecapItem>)list.OrderBy(x => x.FromTurnExclusive).ThenBy(x => x.ToTurnInclusive).ToList() : Array.Empty<RecapItem>();
		}

		public bool UpdateRecap(string convKey, string recapId, string newText)
		{
			if (!_recaps.TryGetValue(convKey, out var list)) return false;
			var r = list.FirstOrDefault(x => x.Id == recapId);
			if (r == null) return false;
			r.Text = newText ?? string.Empty;
			r.UpdatedAt = DateTime.UtcNow;
			OnRecapUpdated?.Invoke(convKey, recapId);
			return true;
		}

		public bool DeleteRecap(string convKey, string recapId)
		{
			if (!_recaps.TryGetValue(convKey, out var list)) return false;
			var idx = list.FindIndex(x => x.Id == recapId);
			if (idx < 0) return false;
			list.RemoveAt(idx);
			OnRecapUpdated?.Invoke(convKey, recapId);
			return true;
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
			if (!_recaps.TryGetValue(convKey, out var list)) return;
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


