using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;

namespace RimAI.Core.Source.Modules.History
{
	internal sealed class HistoryService : IHistoryService
	{
		private readonly ConfigurationService _cfg;

		// 主存：仅保存未删除条目；每会话串行写入
		private readonly ConcurrentDictionary<string, List<HistoryEntry>> _store = new ConcurrentDictionary<string, List<HistoryEntry>>();
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
		private readonly ConcurrentDictionary<string, long> _nextTurnOrdinal = new ConcurrentDictionary<string, long>();
		private readonly ConcurrentDictionary<string, string[]> _participantsByConvKey = new ConcurrentDictionary<string, string[]>();

		public event Action<string, string> OnEntryRecorded;
		public event Action<string, string> OnEntryEdited;
		public event Action<string, string> OnEntryDeleted;

		public HistoryService(IConfigurationService cfg)
		{
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("HistoryService requires ConfigurationService");
		}

		public async Task AppendPairAsync(string convKey, string userText, string aiFinalText, CancellationToken ct = default)
		{
			await AppendUserAsync(convKey, userText, ct).ConfigureAwait(false);
			await AppendAiFinalAsync(convKey, aiFinalText, ct).ConfigureAwait(false);
		}

		public Task AppendUserAsync(string convKey, string userText, CancellationToken ct = default)
			=> AppendInternalAsync(convKey, EntryRole.User, userText, advanceTurn: false, ct);

		public Task AppendAiFinalAsync(string convKey, string aiFinalText, CancellationToken ct = default)
			=> AppendInternalAsync(convKey, EntryRole.Ai, aiFinalText, advanceTurn: true, ct);

		private async Task AppendInternalAsync(string convKey, EntryRole role, string content, bool advanceTurn, CancellationToken ct)
		{
			if (string.IsNullOrWhiteSpace(convKey)) throw new ArgumentException("convKey is empty");
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var list = _store.GetOrAdd(convKey, _ => new List<HistoryEntry>());
				var entry = new HistoryEntry
				{
					Id = Guid.NewGuid().ToString("N"),
					Role = role,
					Content = content ?? string.Empty,
					Timestamp = DateTime.UtcNow,
					Deleted = false,
					TurnOrdinal = null
				};
				if (advanceTurn)
				{
					var next = _nextTurnOrdinal.AddOrUpdate(convKey, 1, (_, v) => v + 1);
					entry.TurnOrdinal = next;
				}
				list.Add(entry);
				OnEntryRecorded?.Invoke(convKey, entry.Id);
			}
			finally
			{
				gate.Release();
			}
		}

		public async Task<HistoryThread> GetThreadAsync(string convKey, int page = 1, int pageSize = 100, CancellationToken ct = default)
		{
			if (page <= 0) page = 1;
			if (pageSize <= 0) pageSize = 100;
			_store.TryGetValue(convKey, out var list);
			var items = list ?? new List<HistoryEntry>();
			var total = items.Count;
			var pageItems = items
				.OrderBy(e => e.Timestamp)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToList();
			return await Task.FromResult(new HistoryThread
			{
				ConvKey = convKey,
				Entries = pageItems,
				Page = page,
				PageSize = pageSize,
				TotalEntries = total
			});
		}

		public async Task<bool> EditEntryAsync(string convKey, string entryId, string newContent, CancellationToken ct = default)
		{
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (!_store.TryGetValue(convKey, out var list)) return false;
				var e = list.FirstOrDefault(x => x.Id == entryId);
				if (e == null) return false;
				e.Content = newContent ?? string.Empty;
				OnEntryEdited?.Invoke(convKey, entryId);
				return true;
			}
			finally { gate.Release(); }
		}

		public async Task<bool> DeleteEntryAsync(string convKey, string entryId, CancellationToken ct = default)
		{
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (!_store.TryGetValue(convKey, out var list)) return false;
				var e = list.FirstOrDefault(x => x.Id == entryId);
				if (e == null) return false;
				e.Deleted = true;
				e.DeletedAt = DateTime.UtcNow;
				OnEntryDeleted?.Invoke(convKey, entryId);
				return true;
			}
			finally { gate.Release(); }
		}

		public async Task<bool> RestoreEntryAsync(string convKey, string entryId, CancellationToken ct = default)
		{
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				if (!_store.TryGetValue(convKey, out var list)) return false;
				var e = list.FirstOrDefault(x => x.Id == entryId);
				if (e == null) return false;
				var undoSec = Math.Max(0, _cfg.GetInternal()?.History?.UndoWindowSeconds ?? 3);
				if (e.Deleted && e.DeletedAt.HasValue && (DateTime.UtcNow - e.DeletedAt.Value).TotalSeconds > undoSec)
				{
					return false; // 超出撤销窗口
				}
				e.Deleted = false;
				e.DeletedAt = null;
				return true;
			}
			finally { gate.Release(); }
		}

		public Task<IReadOnlyList<HistoryEntry>> GetAllEntriesAsync(string convKey, CancellationToken ct = default)
		{
			_store.TryGetValue(convKey, out var list);
			IReadOnlyList<HistoryEntry> result = (list ?? new List<HistoryEntry>()).OrderBy(e => e.Timestamp).ToList();
			return Task.FromResult(result);
		}

		public Task UpsertParticipantsAsync(string convKey, IReadOnlyList<string> participantIds, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(convKey)) throw new ArgumentException("convKey is empty");
			if (participantIds == null || participantIds.Count == 0) return Task.CompletedTask;
			var normalized = participantIds.Distinct().OrderBy(x => x).ToArray();
			_participantsByConvKey.AddOrUpdate(convKey, normalized, (_, __) => normalized);
			return Task.CompletedTask;
		}

		public IReadOnlyList<string> GetParticipantsOrEmpty(string convKey)
		{
			if (string.IsNullOrWhiteSpace(convKey)) return Array.Empty<string>();
			return _participantsByConvKey.TryGetValue(convKey, out var arr) ? (IReadOnlyList<string>)arr : Array.Empty<string>();
		}

		public IReadOnlyList<string> GetAllConvKeys()
		{
			return _store.Keys.ToList();
		}

		public bool TryGetEntry(string convKey, string entryId, out HistoryEntry entry)
		{
			entry = null;
			if (!_store.TryGetValue(convKey, out var list)) return false;
			entry = list.FirstOrDefault(x => x.Id == entryId);
			return entry != null;
		}
	}
}


