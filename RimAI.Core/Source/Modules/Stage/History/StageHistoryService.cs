using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Modules.World;

namespace RimAI.Core.Modules.Stage.History
{
    /// <summary>
    /// 舞台历史服务：仅承接“最终输出”写入。逐轮发言由 Act 自行写入全局历史，不经过此服务。
    /// </summary>
    internal sealed class StageHistoryService : IStageHistoryService
    {
        private readonly RimAI.Core.Services.IHistoryWriteService _historyWrite;
        private readonly IParticipantIdService _pid;

        public StageHistoryService(RimAI.Core.Services.IHistoryWriteService historyWrite, IParticipantIdService pid)
        {
            _historyWrite = historyWrite;
            _pid = pid;
        }

        public async Task AppendFinalAsync(string convKey, IReadOnlyList<string> participants, string speakerId, string finalText, DateTime? atUtc = null)
        {
            var convId = await EnsureConversationAsync(convKey, participants);
            var now = atUtc ?? DateTime.UtcNow;
            await _historyWrite.AppendEntryAsync(convId, new ConversationEntry(speakerId ?? _pid.GetPlayerId(), finalText ?? string.Empty, now));
        }

        private async Task<string> EnsureConversationAsync(string convKey, IReadOnlyList<string> participants)
        {
            var idsByKey = await _historyWrite.FindByConvKeyAsync(convKey);
            var convId = idsByKey?.LastOrDefault();
            if (string.IsNullOrWhiteSpace(convId))
            {
                convId = _historyWrite.CreateConversation(participants);
            }
            return convId;
        }
    }
}


