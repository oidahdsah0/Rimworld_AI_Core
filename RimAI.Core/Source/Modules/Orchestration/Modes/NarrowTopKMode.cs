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
    /// NarrowTopK：使用向量索引收缩 TopK，从 TopK 中确定性选择第一个工具并执行。
    /// </summary>
    internal sealed class NarrowTopKMode : IToolMatchMode
    {
        public string Name => "NarrowTopK";

        private readonly IToolRegistryService _tools;
        private readonly IToolVectorIndexService _toolIndex;
        private readonly Infrastructure.Configuration.IConfigurationService _config;
        private readonly IEventBus _bus;

        public NarrowTopKMode(
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
            var result = new ToolCallsResult { SelectedMode = Name };

            var all = _tools.GetAllToolSchemas();
            if (_toolIndex == null)
            {
                result.Notes = "Index not available";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            var cfg = _config?.Current;
            int k = Math.Max(1, options?.TopK ?? cfg?.Embedding?.TopK ?? 5);
            double wName = cfg?.Embedding?.Tools?.ScoreWeights?.Name ?? 0.6;
            double wDesc = cfg?.Embedding?.Tools?.ScoreWeights?.Description ?? 0.4;

            var matches = await _toolIndex.SearchAsync(userInput ?? string.Empty, all, k, wName, wDesc);
            if (matches == null || matches.Count == 0)
            {
                _bus?.Publish(new OrchestrationProgressEvent { Source = Name, Stage = "ToolMatch", Message = "TopK 无命中" });
                result.Notes = "TopK empty";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            var chosenName = matches.First().Tool;
            var chosen = all.FirstOrDefault(t => string.Equals(t.Name, chosenName, StringComparison.OrdinalIgnoreCase));
            if (chosen == null)
            {
                result.Notes = "Selected tool schema missing";
                result.DurationMs = (int)sw.ElapsedMilliseconds;
                return result;
            }

            try
            {
                var args = new Dictionary<string, object>();
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


