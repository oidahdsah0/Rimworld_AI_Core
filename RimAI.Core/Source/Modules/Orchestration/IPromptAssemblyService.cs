using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 提示词组装服务（内部接口）。
    /// 将固定提示词、人物传记段落、前情提要与相关历史片段进行裁剪与拼接，生成 system 提示。
    /// </summary>
    internal enum PromptAssemblyMode { Chat, Command }

    internal interface IPromptAssemblyService
    {
        Task<string> BuildSystemPromptAsync(
            IReadOnlyCollection<string> participantIds,
            PromptAssemblyMode mode,
            string userInput,
            string locale = null,
            CancellationToken ct = default);
    }
}


