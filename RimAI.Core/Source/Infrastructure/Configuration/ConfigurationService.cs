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
            private System.DateTime _lastIndexRebuildUtc = System.DateTime.MinValue;
        private readonly object _indexGate = new object();

        public event Action<CoreConfig> OnConfigurationChanged;

        public void Reload()
        {
            // TODO: RimWorld 设置读取逻辑（P3 或更高阶段）
            var oldCfg = _current;
            var newCfg = CoreConfig.CreateDefault();
            _current = newCfg;
            OnConfigurationChanged?.Invoke(_current);

            TryRebuildToolIndexIfNeeded(oldCfg, newCfg);
        }

        public void Apply(CoreConfig snapshot)
        {
            if (snapshot == null) return;
            var oldCfg = _current;
            _current = snapshot;
            OnConfigurationChanged?.Invoke(_current);

            TryRebuildToolIndexIfNeeded(oldCfg, snapshot);

            // 触发 Prompt 模板热重载（基于文件时间戳变化）
            try
            {
                var tmpl = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Prompting.IPromptTemplateService>();
                // 访问 Get() 以触发路径与时间戳缓存更新
                tmpl?.Get();
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
                        DefaultTemplate = cfg.Orchestration?.Progress?.DefaultTemplate ?? "{{Stage}}: {{Message}}",
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

        /// <summary>
        /// 仅当 Embedding/Tools 相关字段发生变化时触发向量索引重建；并做最小去抖（≥2秒）。
        /// </summary>
        private void TryRebuildToolIndexIfNeeded(CoreConfig oldCfg, CoreConfig newCfg)
        {
            try
            {
                if (!HasEmbeddingToolsChanged(oldCfg, newCfg)) return;
                var now = System.DateTime.UtcNow;
                lock (_indexGate)
                {
                    if ((now - _lastIndexRebuildUtc).TotalSeconds < 2) return;
                    _lastIndexRebuildUtc = now;
                }
                var index = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                index?.MarkStale();
                _ = index?.EnsureBuiltAsync();
            }
            catch { /* ignore */ }
        }

        private static bool HasEmbeddingToolsChanged(CoreConfig a, CoreConfig b)
        {
            if (a == null && b == null) return false;
            if (a == null || b == null) return true;
            var ta = a.Embedding?.Tools;
            var tb = b.Embedding?.Tools;
            if (ta == null && tb == null) return false;
            if (ta == null || tb == null) return true;
            if (!string.Equals(ta.Mode, tb.Mode, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.Equals(ta.IndexPath, tb.IndexPath, System.StringComparison.OrdinalIgnoreCase)) return true;
            if (ta.AutoBuildOnStart != tb.AutoBuildOnStart) return true;
            if (ta.BlockDuringBuild != tb.BlockDuringBuild) return true;
            if (!DoubleEquals(ta.Top1Threshold, tb.Top1Threshold)) return true;
            if (!DoubleEquals(ta.LightningTop1Threshold, tb.LightningTop1Threshold)) return true;
            if (!DoubleEquals(a.Embedding?.TopK ?? 0, b.Embedding?.TopK ?? 0)) return true;
            if (!DoubleEquals(a.Embedding?.MaxContextChars ?? 0, b.Embedding?.MaxContextChars ?? 0)) return true;
            var wa = ta.ScoreWeights; var wb = tb.ScoreWeights;
            if (wa == null ^ wb == null) return true;
            if (wa != null && (!DoubleEquals(wa.Name, wb.Name) || !DoubleEquals(wa.Description, wb.Description))) return true;
            var da = ta.DynamicThresholds; var db = tb.DynamicThresholds;
            if (da == null ^ db == null) return true;
            if (da != null && (
                da.Enabled != db.Enabled ||
                !DoubleEquals(da.Smoothing, db.Smoothing) ||
                !DoubleEquals(da.MinTop1, db.MinTop1) ||
                !DoubleEquals(da.MaxTop1, db.MaxTop1))) return true;
            return false;
        }

        private static bool DoubleEquals(double x, double y)
        {
            return System.Math.Abs(x - y) < 1e-9;
        }
    }
}