using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.History.Relations
{
	internal sealed class RelationsService : IRelationsService
	{
		private readonly IHistoryService _history;

		public RelationsService(IHistoryService history)
		{
			_history = history;
		}

		public Task<RelationResult> ListSupersetsAsync(IReadOnlyList<string> participantIds, int page, int pageSize, CancellationToken ct = default)
		{
			var baseSet = new HashSet<string>(participantIds ?? Array.Empty<string>());
			var candidates = new List<(string convKey, int diff, DateTime lastUpdate)>();
			foreach (var ck in _history.GetAllConvKeys())
			{
				var parts = _history.GetParticipantsOrEmpty(ck);
				if (parts.Count == 0) continue;
				var set = new HashSet<string>(parts);
				if (set.IsSupersetOf(baseSet) && set.Count > baseSet.Count)
				{
					var last = GetLastTimestamp(ck);
					candidates.Add((ck, set.Count - baseSet.Count, last));
				}
			}
			var ordered = candidates.OrderBy(x => x.diff).ThenByDescending(x => x.lastUpdate).Select(x => x.convKey).ToList();
			return Task.FromResult(Page(ordered, page, pageSize));
		}

		public Task<RelationResult> ListSubsetsAsync(IReadOnlyList<string> participantIds, int page, int pageSize, CancellationToken ct = default)
		{
			var baseSet = new HashSet<string>(participantIds ?? Array.Empty<string>());
			var candidates = new List<(string convKey, int diff, DateTime lastUpdate)>();
			foreach (var ck in _history.GetAllConvKeys())
			{
				var parts = _history.GetParticipantsOrEmpty(ck);
				if (parts.Count == 0) continue;
				var set = new HashSet<string>(parts);
				if (baseSet.IsSupersetOf(set) && set.Count < baseSet.Count)
				{
					var last = GetLastTimestamp(ck);
					candidates.Add((ck, baseSet.Count - set.Count, last));
				}
			}
			var ordered = candidates.OrderBy(x => x.diff).ThenByDescending(x => x.lastUpdate).Select(x => x.convKey).ToList();
			return Task.FromResult(Page(ordered, page, pageSize));
		}

		public Task<IReadOnlyList<string>> ListByParticipantAsync(string participantId, CancellationToken ct = default)
		{
			var result = new List<string>();
			foreach (var ck in _history.GetAllConvKeys())
			{
				var parts = _history.GetParticipantsOrEmpty(ck);
				if (parts.Contains(participantId)) result.Add(ck);
			}
			return Task.FromResult((IReadOnlyList<string>)result);
		}

		private DateTime GetLastTimestamp(string convKey)
		{
			var list = _history.GetAllEntriesAsync(convKey).GetAwaiter().GetResult();
			return list.Count == 0 ? DateTime.MinValue : list.Max(x => x.Timestamp);
		}

		private static RelationResult Page(IReadOnlyList<string> list, int page, int pageSize)
		{
			if (page <= 0) page = 1; if (pageSize <= 0) pageSize = 50;
			var total = list.Count;
			var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
			return new RelationResult { ConvKeys = items, Page = page, PageSize = pageSize, Total = total };
		}
	}
}


