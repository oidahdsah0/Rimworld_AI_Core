using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts; // ToolDefinition

namespace RimAI.Core.Contracts.Tools
{
    /// <summary>
    /// RimAI 工具接口，供 LLM 通过 function calling 使用。
    /// </summary>
    public interface IRimAITool
    {
        string Name { get; }
        string Description { get; }

        /// <summary>
        /// 返回该工具的 JSON Schema 定义，包装成 ToolDefinition 供 LLM 参考。
        /// </summary>
        ToolDefinition GetSchema();

        /// <summary>
        /// 执行工具核心逻辑。
        /// </summary>
        /// <param name="parameters">由模型解析出的参数。</param>
        Task<object> ExecuteAsync(Dictionary<string, object> parameters);
    }
}