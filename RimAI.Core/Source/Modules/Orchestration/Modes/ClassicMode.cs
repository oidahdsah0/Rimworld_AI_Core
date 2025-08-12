using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.Embedding;

namespace RimAI.Core.Modules.Orchestration.Modes
{
    /// <summary>
    /// Classic：不使用索引与阈值，仅暴露所有工具。确定性选择第一个工具并执行。
    /// </summary>
    internal sealed class ClassicMode : IToolMatchMode
    {
        public string Name => "Classic";

        private readonly IToolRegistryService _tools;
        private readonly IEventBus _bus;

        public ClassicMode(IToolRegistryService tools)
        {
            _tools = tools;
            _bus = CoreServices.Locator.Get<IEventBus>();
        }

        public async Task<ToolCallsResult> ExecuteAsync(string userInput, IReadOnlyList<string> participantIds, ToolOrchestrationOptions options, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var result = new ToolCallsResult
            {
                SelectedMode = Name
            };

            var all = _tools.GetAllToolSchemas();
            var chosen = all.FirstOrDefault();
            if (chosen == null)
            {
                _bus?.Publish(new OrchestrationProgressEvent { Source = Name, Stage = "ToolMatch", Message = "无候选工具" });
                result.Notes = "No tool selected";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            try
            {
                object toolRes = await _tools.ExecuteToolAsync(chosen.Name, new Dictionary<string, object>());
                result.Calls.Add(new ToolCallRecord
                {
                    ToolName = chosen.Name,
                    ArgumentsJson = "{}",
                    Succeeded = true,
                    ResultType = toolRes == null ? "null" : toolRes.GetType().Name,
                    ResultJson = JsonConvert.SerializeObject(toolRes)
                });
                _bus?.Publish(new OrchestrationProgressEvent { Source = Name, Stage = "Execution", Message = $"执行完成：{chosen.Name}" });
            }
            catch (Exception ex)
            {
                result.Calls.Add(new ToolCallRecord
                {
                    ToolName = chosen.Name,
                    ArgumentsJson = "{}",
                    Succeeded = false,
                    Error = ex.Message
                });
            }

            result.DurationMs = (int)sw.ElapsedMilliseconds;
            return result;
        }
    }
}


