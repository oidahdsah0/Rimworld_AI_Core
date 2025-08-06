using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts.Tooling
{
    /// <summary>
    /// 外部 Mod 可实现的工具接口。实现类被 Core 在启动时自动扫描并注册。
    /// </summary>
    public interface IRimAITool
    {
        string Name { get; }
        string Description { get; }
        ToolFunction GetSchema();
        Task<object> ExecuteAsync(Dictionary<string, object> parameters);
    }
}
