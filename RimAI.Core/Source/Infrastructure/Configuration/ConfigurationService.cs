using System;
using RimAI.Core.Settings;
using RimAI.Core.Contracts.Settings;

namespace RimAI.Core.Infrastructure.Configuration
{
    /// <summary>
    /// P1 版本的配置服务：读取 RimWorld 的 ModSettings 将在后续阶段实现。
    /// 目前返回默认 <see cref="CoreConfig"/>，并支持 Hot Reload 事件广播。
    /// </summary>
    public sealed class ConfigurationService : IConfigurationService, RimAI.Core.Contracts.Services.IConfigurationService
    {
        private CoreConfig _current = CoreConfig.CreateDefault();
        public CoreConfig Current => _current;

        public event Action<CoreConfig> OnConfigurationChanged;

        public void Reload()
        {
            // TODO: RimWorld 设置读取逻辑（P3 或更高阶段）
            _current = CoreConfig.CreateDefault();
            OnConfigurationChanged?.Invoke(_current);

            // 配置变更后触发工具索引重建（标记过期并尝试异步构建）
            try
            {
                var index = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                index?.MarkStale();
                _ = index?.EnsureBuiltAsync();
            }
            catch { /* ignore */ }
        }

        public void Apply(CoreConfig snapshot)
        {
            if (snapshot == null) return;
            _current = snapshot;
            OnConfigurationChanged?.Invoke(_current);

            // 配置变更后触发工具索引重建
            try
            {
                var index = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                index?.MarkStale();
                _ = index?.EnsureBuiltAsync();
            }
            catch { /* ignore */ }
        }

        // Explicit interface implementation for external read-only contracts
        CoreConfigSnapshot RimAI.Core.Contracts.Services.IConfigurationService.Current => MapToSnapshot(_current);

        private static CoreConfigSnapshot MapToSnapshot(CoreConfig cfg)
        {
            if (cfg == null) return new CoreConfigSnapshot();
            return new CoreConfigSnapshot
            {
                LLM = new LLMConfigSnapshot
                {
                    Temperature = cfg.LLM?.Temperature ?? 0
                },
                Orchestration = new OrchestrationConfigSnapshot
                {
                    Strategy = cfg.Orchestration?.Strategy ?? "Classic",
                    Progress = new OrchestrationProgressConfigSnapshot
                    {
                        DefaultTemplate = cfg.Orchestration?.Progress?.DefaultTemplate ?? "[{{Source}}] {{Stage}}: {{Message}}",
                        StageTemplates = cfg.Orchestration?.Progress?.StageTemplates ?? new System.Collections.Generic.Dictionary<string, string>(),
                        PayloadPreviewChars = cfg.Orchestration?.Progress?.PayloadPreviewChars ?? 200
                    },
                    Planning = new PlanningConfigSnapshot
                    {
                        EnableLightChaining = cfg.Orchestration?.Planning?.EnableLightChaining ?? false,
                        MaxSteps = cfg.Orchestration?.Planning?.MaxSteps ?? 3,
                        AllowParallel = cfg.Orchestration?.Planning?.AllowParallel ?? false,
                        MaxParallelism = cfg.Orchestration?.Planning?.MaxParallelism ?? 2,
                        FanoutPerStage = cfg.Orchestration?.Planning?.FanoutPerStage ?? 3,
                        SatisfactionThreshold = cfg.Orchestration?.Planning?.SatisfactionThreshold ?? 0.8
                    }
                },
                Embedding = new EmbeddingConfigSnapshot
                {
                    Enabled = cfg.Embedding?.Enabled ?? true,
                    TopK = cfg.Embedding?.TopK ?? 5,
                    MaxContextChars = cfg.Embedding?.MaxContextChars ?? 2000,
                    Tools = new EmbeddingToolsConfigSnapshot
                    {
                        Mode = cfg.Embedding?.Tools?.Mode ?? "FastTop1",
                        Top1Threshold = cfg.Embedding?.Tools?.Top1Threshold ?? 0.82,
                        LightningTop1Threshold = cfg.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                        IndexPath = cfg.Embedding?.Tools?.IndexPath ?? "auto",
                        AutoBuildOnStart = cfg.Embedding?.Tools?.AutoBuildOnStart ?? true,
                        BlockDuringBuild = cfg.Embedding?.Tools?.BlockDuringBuild ?? true,
                        ScoreWeights = new EmbeddingToolsScoreWeightsSnapshot
                        {
                            Name = cfg.Embedding?.Tools?.ScoreWeights?.Name ?? 0.6,
                            Description = cfg.Embedding?.Tools?.ScoreWeights?.Description ?? 0.4
                        },
                        DynamicThresholds = new EmbeddingDynamicThresholdsSnapshot
                        {
                            Enabled = cfg.Embedding?.Tools?.DynamicThresholds?.Enabled ?? true,
                            Smoothing = cfg.Embedding?.Tools?.DynamicThresholds?.Smoothing ?? 0.2,
                            MinTop1 = cfg.Embedding?.Tools?.DynamicThresholds?.MinTop1 ?? 0.78,
                            MaxTop1 = cfg.Embedding?.Tools?.DynamicThresholds?.MaxTop1 ?? 0.90
                        }
                    }
                }
            };
        }
    }
}