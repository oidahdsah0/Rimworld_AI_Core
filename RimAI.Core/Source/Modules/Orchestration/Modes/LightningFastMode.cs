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
    /// LightningFast：Top1≥更高阈值时执行并传入 __fastResponse=true；否则不执行且不降级。
    /// </summary>
    internal sealed class LightningFastMode : IToolMatchMode
    {
        public string Name => "LightningFast";

        private readonly IToolRegistryService _tools;
        private readonly IToolVectorIndexService _toolIndex;
        private readonly Infrastructure.Configuration.IConfigurationService _config;
        private readonly IEventBus _bus;

        public LightningFastMode(
            IToolRegistryService tools,
            IToolVectorIndexService toolIndex,
            Infrastructure.Configuration.IConfigurationService config)
        {
            _tools = tools;
            _toolIndex = toolIndex;
            _config = config;
            _bus = CoreServices.Locator.Get<IEventBus>();
        }

        public async Task<ToolCallsResult> ExecuteAsync(string userInput, IReadOnlyList<string> participantIds, ToolOrchestrationOptions options, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var result = new ToolCallsResult { SelectedMode = Name, UsedLightningFast = true };

            if (_toolIndex == null)
            {
                result.Notes = "Index not available";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            var cfg = _config?.Current;
            double wName = cfg?.Embedding?.Tools?.ScoreWeights?.Name ?? 0.6;
            double wDesc = cfg?.Embedding?.Tools?.ScoreWeights?.Description ?? 0.4;
            double threshold = cfg?.Embedding?.Tools?.LightningTop1Threshold ?? 0.86;

            var all = _tools.GetAllToolSchemas();
            var top1 = await _toolIndex.SearchTop1Async(userInput ?? string.Empty, all, wName, wDesc);
            if (top1 == null || top1.Score < threshold)
            {
                _bus?.Publish(new OrchestrationProgressEvent { Source = Name, Stage = "ToolMatch", Message = "Top1 未达 Lightning 阈值，跳过执行" });
                result.Notes = "Top1 below lightning threshold";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            var chosen = all.FirstOrDefault(t => string.Equals(t.Name, top1.Tool, StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
            {
                result.Notes = "Selected tool schema missing";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            try
            {
                var args = new Dictionary<string, object> { { "__fastResponse", true } };
                object toolRes = await _tools.ExecuteToolAsync(chosen.Name, args);
                result.Calls.Add(new ToolCallRecord
                {
                    ToolName = chosen.Name,
                    ArgumentsJson = JsonConvert.SerializeObject(args),
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
                    ArgumentsJson = "{\"__fastResponse\":true}",
                    Succeeded = false,
                    Error = ex.Message
                });
            }

            result.DurationMs = (int)sw.ElapsedMilliseconds;
            return result;
        }
    }
}


