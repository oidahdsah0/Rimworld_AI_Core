using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts.Services
{
    public interface IToolRegistryService
    {
        /// <summary>
        /// 获取所有已注册工具的 Schema。
        /// </summary>
        List<ToolDefinition> GetAllToolSchemas();

        /// <summary>
        /// 执行指定工具。
        /// </summary>
        Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
    }
}