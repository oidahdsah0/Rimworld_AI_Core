using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Server
{
	// 会话修剪等内部操作
	internal sealed partial class ServerService
	{
		// 将指定巡检会话(convKey)的历史条目裁剪为不超过 MaxInspectionHistoryEntries
		private async Task PruneInspectionHistoryIfNeededAsync(string convKey, CancellationToken ct)
		{
			try
			{
				var all = await _history.GetAllEntriesRawAsync(convKey, ct).ConfigureAwait(false);
				var list = (all ?? Array.Empty<RimAI.Core.Source.Modules.History.Models.HistoryEntry>())
					.Where(e => e != null && !e.Deleted)
					.OrderBy(e => e.Timestamp)
					.ToList();
				if (list.Count <= MaxInspectionHistoryEntries) return;
				int surplus = list.Count - MaxInspectionHistoryEntries;
				for (int i = 0; i < surplus; i++)
				{
					var victim = list[i];
					if (victim == null) continue;
					try { await _history.DeleteEntryAsync(convKey, victim.Id, ct).ConfigureAwait(false); } catch { }
				}
			}
			catch { }
		}
	}
}
