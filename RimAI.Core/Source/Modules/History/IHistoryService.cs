using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.History
{
	internal interface IHistoryService
	{
		// 写入（终端完成一次交互后调用）
		Task AppendPairAsync(string convKey, string userText, string aiFinalText, CancellationToken ct = default);
		Task AppendUserAsync(string convKey, string userText, CancellationToken ct = default);
		Task AppendAiFinalAsync(string convKey, string aiFinalText, CancellationToken ct = default);

		// 新增：写入 AI 过程说明（不推进回合，不参与 Recap 的 Turn 分桶）
		Task AppendAiNoteAsync(string convKey, string aiNoteText, CancellationToken ct = default);

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
		bool TryGetEntry(string convKey, string entryId, out HistoryEntry entry);
		Task UpsertParticipantsAsync(string convKey, IReadOnlyList<string> participantIds, CancellationToken ct = default);
		IReadOnlyList<string> GetParticipantsOrEmpty(string convKey);
		IReadOnlyList<string> GetAllConvKeys();
	}
}


