using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts.Tooling
{
    /// <summary>
    /// 对外只读的工具注册服务接口。
    /// </summary>
    public interface IToolRegistryService
    {
        List<ToolFunction> GetAllToolSchemas();
        Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
    }
}
