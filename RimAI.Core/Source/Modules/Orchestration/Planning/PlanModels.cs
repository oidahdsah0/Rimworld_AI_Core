using System.Collections.Generic;

namespace RimAI.Core.Modules.Orchestration.Planning
{
    /// <summary>
    /// 规划黑板（最小实现）：保存本轮会话的关键信息，便于生成最终提示词。
    /// </summary>
    internal sealed class PlanBlackboard
    {
        public List<string> RagDocIds { get; } = new List<string>();
        public List<string> KeyFindings { get; } = new List<string>();
        public string ToolResultSnippet { get; set; } = string.Empty;
        public List<string> Trace { get; } = new List<string>();
    }

    /// <summary>
    /// 最终提示词结果。
    /// </summary>
    internal sealed class FinalPromptResult
    {
        public string FinalPrompt { get; set; } = string.Empty;
        public PlanBlackboard Blackboard { get; set; } = new PlanBlackboard();
    }

    internal sealed class PlanProgressUpdate
    {
        public string Source { get; set; } = "Planner";
        public string Stage { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public object Payload { get; set; }
    }

    internal interface IPlanProgressReporter
    {
        void Report(PlanProgressUpdate update);
    }

    internal sealed class EventBusPlanProgressReporter : IPlanProgressReporter
    {
        public void Report(PlanProgressUpdate update)
        {
            var bus = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Contracts.Eventing.IEventBus>();
            if (bus == null || update == null) return;
            var evt = new RimAI.Core.Contracts.Eventing.OrchestrationProgressEvent
            {
                Source = update.Source,
                Stage = update.Stage,
                Message = update.Message,
                PayloadJson = update.Payload == null ? string.Empty : Newtonsoft.Json.JsonConvert.SerializeObject(update.Payload)
            };
            bus.Publish(evt);
        }
    }
}


