using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Modules.Stage.Kernel;

namespace RimAI.Core.Modules.Stage.Triggers
{
    internal interface IStageTrigger
    {
        string Name { get; }
        string TargetActName { get; }

        void Subscribe(IEventBus bus, IStageKernel kernel); // 可选：被动事件触发
        Task OnEnableAsync(IStageKernel kernel, CancellationToken ct);
        Task OnDisableAsync(CancellationToken ct);

        // 主动触发器：执行一次扫描/判断并驱动 Stage 执行
        Task RunOnceAsync(IStageService stage, IStageKernel kernel, CancellationToken ct);
    }
}


