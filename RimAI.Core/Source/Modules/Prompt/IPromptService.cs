using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Prompt
{
    /// <summary>
    /// 统一提示词服务（合并组装/模板/Composer 能力）。
    /// 提供唯一入口 Compose 与本地化辅助 ResolveLocale。
    /// </summary>
    internal interface IPromptService
    {
        Task<string> ComposeSystemPromptAsync(PromptAssemblyInput input, CancellationToken ct = default);
        string ResolveLocale();
    }
}


