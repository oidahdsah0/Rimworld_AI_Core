using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Orchestration
{
    internal interface IPromptAssemblyService
    {
        /// <summary>
        /// 基于 <see cref="PromptAssemblyInput"/> 输入构建最终的 system 提示（不包含用户发言）。
        /// </summary>
        Task<string> ComposeSystemPromptAsync(
            PromptAssemblyInput input,
            CancellationToken ct = default);
    }
}


