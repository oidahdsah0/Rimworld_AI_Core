using System.Collections.Generic;
using System.Threading;
using RimAI.Framework.Contracts;
using System.Threading.Tasks;

#nullable enable

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 核心大脑：负责智能编排整条 Function-Calling 工作流。
    /// </summary>
    public interface IOrchestrationService
    {
        /// <summary>
        /// 执行一次工具辅助查询流程，并以流式方式返回 AI 回复。
        /// </summary>
        /// <param name="userInput">玩家原始输入</param>
        /// <param name="personaSystemPrompt">Persona 的系统提示</param>
        /// <param name="participants">可选：对话参与者唯一 ID，用于历史聚合</param>
        /// <param name="ct">取消令牌</param>
        IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(
            string userInput,
            string personaSystemPrompt,
            IEnumerable<string>? participants = null,
            CancellationToken ct = default);
    }
}
