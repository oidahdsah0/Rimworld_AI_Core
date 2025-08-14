using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.History.Recap
{
	internal interface IRecapService
	{
		Task EnqueueGenerateIfDueAsync(string convKey, CancellationToken ct = default);
		Task ForceRebuildAsync(string convKey, CancellationToken ct = default);
		Task RebuildStaleAsync(string convKey, CancellationToken ct = default);

		IReadOnlyList<RecapItem> GetRecaps(string convKey);
		bool UpdateRecap(string convKey, string recapId, string newText);
		bool DeleteRecap(string convKey, string recapId);

		// 快照（持久化对接由 PersistenceManager 统一处理，内部提供导出/导入以便集成）
		RecapSnapshot ExportSnapshot();
		void ImportSnapshot(RecapSnapshot snapshot);
		void MarkStale(string convKey, long? affectedTurnOrdinal = null);

		event Action<string, string> OnRecapUpdated;
	}

	internal sealed class RecapSnapshot
	{
		public Dictionary<string, List<RecapItem>> Items { get; set; } = new Dictionary<string, List<RecapItem>>();
	}
}


