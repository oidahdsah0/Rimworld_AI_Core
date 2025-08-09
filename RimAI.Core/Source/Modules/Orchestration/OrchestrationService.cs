using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Modules.LLM;
using RimAI.Core.Infrastructure;
using InfraConfig = RimAI.Core.Infrastructure.Configuration.IConfigurationService;
using ContractsConfig = RimAI.Core.Contracts.Services.IConfigurationService;
using System.Security.Cryptography;
// using RimAI.Core.Infrastructure.Cache; // 缓存已下沉至 Framework
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// P5 阶段 OrchestrationService 完整五步最小实现。
    /// </summary>
    internal sealed class OrchestrationService : IOrchestrationService
    {
        private readonly Dictionary<string, Strategies.IOrchestrationStrategy> _strategies;
        private readonly Strategies.IOrchestrationStrategy _defaultStrategy;
        private readonly InfraConfig _config;

        public OrchestrationService(IEnumerable<Strategies.IOrchestrationStrategy> strategies, InfraConfig config)
        {
            // 将策略列表转为名称映射；默认 Classic
            _strategies = (strategies ?? System.Linq.Enumerable.Empty<Strategies.IOrchestrationStrategy>())
                .GroupBy(s => s.Name)
                .ToDictionary(g => g.Key, g => g.First());

            _defaultStrategy = _strategies.TryGetValue("Classic", out var cls) ? cls : null;
            _config = config;
            RimAI.Core.Infrastructure.CoreServices.Logger.Info($"Loaded strategies: {string.Join(", ", _strategies.Keys)}");
        }

        public IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string query, string personaSystemPrompt = "")
        {
            var strategy = _defaultStrategy;
            try
            {
                var name = _config?.Current?.Orchestration?.Strategy ?? "Classic";
                if (!string.IsNullOrWhiteSpace(name) && _strategies.TryGetValue(name, out var s))
                    strategy = s;
            }
            catch { /* 配置读取失败时使用默认 */ }
            if (strategy == null)
            {
                return FallbackFailure($"未找到默认策略 Classic。已加载策略: {string.Join(", ", _strategies.Keys)}");
            }

            var ctx = new Strategies.OrchestrationContext
            {
                Query = query ?? string.Empty,
                PersonaSystemPrompt = personaSystemPrompt ?? string.Empty,
                Cancellation = default
            };
            return strategy.ExecuteAsync(ctx);
        }

        private static async IAsyncEnumerable<Result<UnifiedChatChunk>> FallbackFailure(string message)
        {
            await System.Threading.Tasks.Task.CompletedTask;
            yield return Result<UnifiedChatChunk>.Failure(message);
        }
    }
}
