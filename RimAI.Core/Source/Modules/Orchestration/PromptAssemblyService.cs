using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Modules.World;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 提示词组装服务的最小占位实现（M1）。
    /// 暂时返回空字符串；后续阶段将注入固定提示/人物传记段落/前情提要/历史片段。
    /// </summary>
    internal sealed class PromptAssemblyService : IPromptAssemblyService
    {
        private readonly IParticipantIdService _pid;

        public PromptAssemblyService(IParticipantIdService pid)
        {
            _pid = pid;
        }

        public Task<string> BuildSystemPromptAsync(IReadOnlyCollection<string> participantIds, CancellationToken ct = default)
        {
            // M1：占位返回空，确保编排可调用但不改变行为
            return Task.FromResult(string.Empty);
        }
    }
}


