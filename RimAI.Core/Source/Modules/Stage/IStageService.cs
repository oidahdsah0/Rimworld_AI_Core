using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Stage
{
    /// <summary>
    /// 舞台服务（P11.5 薄层）。
    /// 仅负责 Act 的注册/启停、仲裁提交、Debug 路由与运行中查询。
    /// </summary>
    internal interface IStageService
    {
        // Debug/脚本直呼某 Act 执行
        IAsyncEnumerable<Result<UnifiedChatChunk>> StartAsync(StageExecutionRequest request, CancellationToken ct = default);

        // 仲裁入口：Act 通过 SubmitIntent 申请执行权限
        StageDecision SubmitIntent(StageIntent intent);

        // Act 注册与启停
        void RegisterAct(Acts.IStageAct act);
        void UnregisterAct(string name);
        void EnableAct(string name);
        void DisableAct(string name);
        IReadOnlyList<string> ListActs();

        // 触发器注册与统一开关
        void RegisterTrigger(Triggers.IStageTrigger trigger);
        void UnregisterTrigger(string name);
        void EnableTrigger(string name);
        void DisableTrigger(string name);
        IReadOnlyList<string> ListTriggers();

        // 查询运行中票据/占用
        IReadOnlyList<RunningActInfo> QueryRunning();

        // 触发器模式下不再提供扫描入口
    }

    // 仲裁数据模型（精简）
    internal sealed class StageIntent
    {
        public string ActName { get; set; }
        public IReadOnlyList<string> Participants { get; set; }
        public string ConvKey { get; set; }
        public string Origin { get; set; } = "Other";
        public string Scenario { get; set; }
        public int? Priority { get; set; }
        public int? Seed { get; set; }
        public string Locale { get; set; }
    }

    internal sealed class StageDecision
    {
        public string Outcome { get; set; } // Approve|Reject|Defer
        public string Reason { get; set; }
        public Kernel.StageTicket Ticket { get; set; }
    }

    internal sealed class StageExecutionRequest
    {
        public string ActName { get; set; }
        public IReadOnlyList<string> Participants { get; set; }
        public string ConvKey { get; set; }
        public string UserInputOrScenario { get; set; }
        public string Locale { get; set; }
        public int? Seed { get; set; }
        public string TargetParticipantId { get; set; }
    }

    internal sealed class RunningActInfo
    {
        public string ActName { get; set; }
        public string ConvKey { get; set; }
        public IReadOnlyList<string> Participants { get; set; }
        public DateTime SinceUtc { get; set; }
        public DateTime LeaseExpiresUtc { get; set; }
    }
}


