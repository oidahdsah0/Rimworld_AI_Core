using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Stage.History
{
    /// <summary>
    /// 舞台专属历史服务，仅负责记录 Act 的“最终输出”（结果记录器）。
    /// 与全局 <c>IHistoryWriteService</c> 解耦，严格避免逐轮对话写入。
    /// </summary>
    internal interface IStageHistoryService
    {
        /// <summary>
        /// 追加“最终输出”（FinalText）。调用方通常为舞台在 Act 结束后写入。
        /// </summary>
        Task AppendFinalAsync(string convKey, IReadOnlyList<string> participants, string speakerId, string finalText, DateTime? atUtc = null);
    }
}


