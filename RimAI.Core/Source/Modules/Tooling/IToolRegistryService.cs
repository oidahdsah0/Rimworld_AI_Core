using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Tooling
{
    /// <summary>
    /// 工具注册服务接口。
    /// 负责提供已注册工具的 Schema 清单，并在运行时安全执行工具。
    /// </summary>
    public interface IToolRegistryService
    {
        /// <summary>
        /// 获取所有已注册工具的 <see cref="ToolFunction"/> 列表，供 <c>IOrchestrationService</c> 传递给 LLM。
        /// </summary>
        List<ToolFunction> GetAllToolSchemas();

        /// <summary>
        /// 根据名称执行指定工具。
        /// </summary>
        /// <param name="toolName">工具名称（小写）。</param>
        /// <param name="parameters">参数键值对。</param>
        /// <returns>执行结果对象。</returns>
        Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
    }
}
