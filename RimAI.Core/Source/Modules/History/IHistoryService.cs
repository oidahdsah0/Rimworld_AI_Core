using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.History
{
	internal interface IHistoryService
	{
		// P14：统一写入入口（JSON 留存：entry/speaker/time/type/content）
		Task AppendRecordAsync(string convKey, string entry, string speaker, string type, string content, bool advanceTurn, CancellationToken ct = default);

		// 查询（分页）
		Task<HistoryThread> GetThreadAsync(string convKey, int page = 1, int pageSize = 100, CancellationToken ct = default);

		// 单条编辑 / 删除 / 撤销
		Task<bool> EditEntryAsync(string convKey, string entryId, string newContent, CancellationToken ct = default);
		Task<bool> DeleteEntryAsync(string convKey, string entryId, CancellationToken ct = default);
		Task<bool> RestoreEntryAsync(string convKey, string entryId, CancellationToken ct = default);

		// 事件（供 Debug/后台订阅）
		event Action<string, string> OnEntryRecorded;
		event Action<string, string> OnEntryEdited;
		event Action<string, string> OnEntryDeleted;

		// 内部查询/元数据（供 P8 Recap/Relations 使用）
		Task<IReadOnlyList<HistoryEntry>> GetAllEntriesAsync(string convKey, CancellationToken ct = default);
		// P14：供持久化模块使用的原始 JSON 读取（不解包）
		Task<IReadOnlyList<HistoryEntry>> GetAllEntriesRawAsync(string convKey, CancellationToken ct = default);
		bool TryGetEntry(string convKey, string entryId, out HistoryEntry entry);
		Task UpsertParticipantsAsync(string convKey, IReadOnlyList<string> participantIds, CancellationToken ct = default);
		IReadOnlyList<string> GetParticipantsOrEmpty(string convKey);
		IReadOnlyList<string> GetAllConvKeys();
	}
}


