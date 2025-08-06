using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Tooling
{
    /// <summary>
    /// 每个可被大语言模型调用的「工具」都必须实现的接口。
    /// <para>
    /// 设计理念：
    /// 1. Name / Description 用于呈现给 LLM 的工具列表；
    /// 2. GetSchema() 返回符合 OpenAI Function Calling 规范的 <see cref="ToolFunction"/>，供 Registry 汇总；
    /// 3. ExecuteAsync() 由 <see cref="IToolRegistryService"/> 调用，执行核心逻辑并返回结果对象（可序列化为 JSON）。
    /// </para>
    /// </summary>
    public interface IRimAITool
    {
        /// <summary>
        /// 工具在 LLM 侧暴露的唯一名称（必须小写无空格）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 对工具用途的简短自然语言描述，供 LLM 理解。
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 获取工具参数定义（JSON Schema）。
        /// </summary>
        ToolFunction GetSchema();

        /// <summary>
        /// 执行工具核心逻辑。
        /// </summary>
        /// <param name="parameters">模型传入的参数字典，已按 JSON Schema 反序列化。</param>
        /// <returns>执行结果，将被序列化回传给 LLM。</returns>
        Task<object> ExecuteAsync(Dictionary<string, object> parameters);
    }
}
