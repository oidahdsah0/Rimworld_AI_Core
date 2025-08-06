using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts
{
    /// <summary>
    /// RimAI 核心 "大脑" —— 智能编排服务。
    ///
    /// 该接口作为 <b>唯一</b> 高层入口，将复杂的五步工作流
    /// （工具决策 → 执行 → 重新提示 → LLM 回复 → 记录历史）
    /// 封装为一个简单的方法，供 UI 或其他调用方使用。
    ///
    /// 在 v4 P5 阶段，仅支持 <b>单轮问答</b>，并以<strong>流式</strong>方式
    /// 返回模型的增量输出。后续阶段可以在接口保持兼容的前提下
    /// 通过重载或可选参数扩展多轮上下文、非流式模式等能力。
    /// </summary>
    public interface IOrchestrationService
    {
        /// <summary>
        /// 执行一次「工具辅助」查询。
        /// </summary>
        /// <param name="query">玩家或调用方的自然语言请求。</param>
        /// <param name="personaSystemPrompt">Persona 提供的系统提示词，用于影响 AI 角色。（可为空）</param>
        /// <returns>
        /// 以 <see cref="UnifiedChatChunk"/> 为单位的 <see cref="IAsyncEnumerable{T}"/> 流。
        /// 每个元素使用 <see cref="Result{T}"/> 包装：成功时 <c>Value</c> 包含增量 token；
        /// 失败时 <c>Error</c> 描述原因，调用方可在 UI 层友好提示。
        /// </returns>
        IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string query, string personaSystemPrompt = "");
    }
}