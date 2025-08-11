using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Stage
{
    /// <summary>
    /// 舞台服务（P11-M1 骨架）。
    /// 负责会话归一化、并发治理（锁/合流/幂等/冷却）与入口约束校验。
    /// M1 返回占位结果；M2 接入 Persona 与历史写入。
    /// </summary>
    internal interface IStageService
    {
        IAsyncEnumerable<Result<UnifiedChatChunk>> StartAsync(StageRequest request, CancellationToken ct = default);
        Task RunScanOnceAsync(CancellationToken ct = default);
    }

    internal sealed class StageRequest
    {
        public IReadOnlyList<string> Participants { get; set; }
        public string Mode { get; set; } = "Chat"; // Chat|Command
        public bool Stream { get; set; } = false;
        public string Origin { get; set; } = "Other"; // PlayerUI|PawnBehavior|AIServer|EventAggregator|Other
        public string InitiatorId { get; set; } = string.Empty;
        public string UserInputOrScenario { get; set; } = string.Empty;
        public string SourceId { get; set; } = string.Empty; // 触发源（用于合流主触发选择）
        public string IdempotencyKey { get; set; } = null;
        public int? Priority { get; set; } = null; // 数值越大优先级越高
        public int? Seed { get; set; } = null;
        public string Locale { get; set; } = null;
        public string TargetParticipantId { get; set; } = null;
    }
}


