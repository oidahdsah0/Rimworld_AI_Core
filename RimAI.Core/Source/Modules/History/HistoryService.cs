using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.History
{
	internal sealed class HistoryService : IHistoryService
	{
		private readonly ConfigurationService _cfg;
		private readonly IWorldDataService _world;

		// 主存：仅保存未删除条目；每会话串行写入
		private readonly ConcurrentDictionary<string, List<HistoryEntry>> _store = new ConcurrentDictionary<string, List<HistoryEntry>>();
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
		private readonly ConcurrentDictionary<string, long> _nextTurnOrdinal = new ConcurrentDictionary<string, long>();
		private readonly ConcurrentDictionary<string, string[]> _participantsByConvKey = new ConcurrentDictionary<string, string[]>();

		public event Action<string, string> OnEntryRecorded;
		public event Action<string, string> OnEntryEdited;
		public event Action<string, string> OnEntryDeleted;

		public HistoryService(IConfigurationService cfg, IWorldDataService world)
		{
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("HistoryService requires ConfigurationService");
			_world = world ?? throw new InvalidOperationException("HistoryService requires IWorldDataService");
		}

		public async Task AppendRecordAsync(string convKey, string entry, string speaker, string type, string content, bool advanceTurn, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(convKey)) throw new ArgumentException("convKey is empty");
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var list = _store.GetOrAdd(convKey, _ => new List<HistoryEntry>());
				string timeStr = null;
				try { timeStr = await _world.GetCurrentGameTimeStringAsync(ct).ConfigureAwait(false); } catch { timeStr = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"); }
				var payload = new HistoryJsonPayload { entry = entry ?? string.Empty, speaker = speaker ?? string.Empty, time = timeStr ?? string.Empty, type = type ?? "chat", content = content ?? string.Empty };
				var rawJson = JsonConvert.SerializeObject(payload);
				var rec = new HistoryEntry
				{
					Id = Guid.NewGuid().ToString("N"),
					Role = DeriveRoleFromSpeaker(speaker),
					Content = rawJson,
					Timestamp = DateTime.UtcNow,
					Deleted = false,
					TurnOrdinal = null
				};
				if (advanceTurn)
				{
					var next = _nextTurnOrdinal.AddOrUpdate(convKey, 1, (_, v) => v + 1);
					rec.TurnOrdinal = next;
				}
				list.Add(rec);
				OnEntryRecorded?.Invoke(convKey, rec.Id);
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
				.Select(MapForDisplay)
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
				// 覆盖 JSON 中的 content 字段
				try
				{
					var jo = JObject.Parse(e.Content ?? "{}");
					jo["content"] = newContent ?? string.Empty;
					e.Content = jo.ToString(Formatting.None);
				}
				catch
				{
					// 若不是 JSON，直接覆盖
					e.Content = newContent ?? string.Empty;
				}
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
			IReadOnlyList<HistoryEntry> result = (list ?? new List<HistoryEntry>())
				.OrderBy(e => e.Timestamp)
				.Where(e => !e.Deleted)
				.Select(MapForDisplay)
				.ToList();
			return Task.FromResult(result);
		}

		public Task<IReadOnlyList<HistoryEntry>> GetAllEntriesRawAsync(string convKey, CancellationToken ct = default)
		{
			_store.TryGetValue(convKey, out var list);
			IReadOnlyList<HistoryEntry> result = (list ?? new List<HistoryEntry>())
				.OrderBy(e => e.Timestamp)
				.ToList();
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
			var raw = list.FirstOrDefault(x => x.Id == entryId);
			if (raw == null) return false;
			entry = MapForDisplay(raw);
			return true;
		}

		private static EntryRole DeriveRoleFromSpeaker(string speaker)
		{
			if (!string.IsNullOrWhiteSpace(speaker) && speaker.StartsWith("player:")) return EntryRole.User;
			return EntryRole.Ai;
		}

		private static bool TryParsePayload(string rawJson, out HistoryJsonPayload payload)
		{
			payload = null;
			if (string.IsNullOrWhiteSpace(rawJson)) return false;
			try { payload = JsonConvert.DeserializeObject<HistoryJsonPayload>(rawJson); return payload != null; } catch { return false; }
		}

		private static HistoryEntry MapForDisplay(HistoryEntry raw)
		{
			if (raw == null) return null;
			var copy = new HistoryEntry
			{
				Id = raw.Id,
				Deleted = raw.Deleted,
				DeletedAt = raw.DeletedAt,
				Timestamp = raw.Timestamp,
				TurnOrdinal = raw.TurnOrdinal
			};
			if (TryParsePayload(raw.Content, out var p))
			{
				copy.Content = p?.content ?? string.Empty;
				copy.Role = DeriveRoleFromSpeaker(p?.speaker ?? string.Empty);
			}
			else
			{
				// 兼容意外：非 JSON 内容按 AI 文本处理
				copy.Content = raw.Content ?? string.Empty;
				copy.Role = EntryRole.Ai;
			}
			return copy;
		}

		// 供持久化在读档后直接回灌 JSON（不改变 JSON 内容，不推进回合计数）
		internal async Task ImportRawSnapshotEntryAsync(string convKey, string rawJson, long? turnOrdinal, DateTime createdAtUtc, CancellationToken ct = default)
		{
			var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
			await gate.WaitAsync(ct).ConfigureAwait(false);
			try
			{
				var list = _store.GetOrAdd(convKey, _ => new List<HistoryEntry>());
				var role = EntryRole.Ai;
				try
				{
					var jo = JObject.Parse(rawJson ?? "{}");
					var sp = jo.Value<string>("speaker") ?? string.Empty;
					role = DeriveRoleFromSpeaker(sp);
				}
				catch { }
				var e = new HistoryEntry
				{
					Id = Guid.NewGuid().ToString("N"),
					Role = role,
					Content = rawJson ?? string.Empty,
					Timestamp = createdAtUtc == default ? DateTime.UtcNow : createdAtUtc,
					Deleted = false,
					TurnOrdinal = turnOrdinal
				};
				list.Add(e);
			}
			finally { gate.Release(); }
		}

		private sealed class HistoryJsonPayload
		{
			public string entry { get; set; }
			public string speaker { get; set; }
			public string time { get; set; }
			public string type { get; set; }
			public string content { get; set; }
		}
	}
}


