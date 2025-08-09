using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Modules.Embedding;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings
{
    /// <summary>
    /// RimAI Core 设置面板（仅负责绘制与应用配置）。
    /// </summary>
    internal sealed class CoreSettingsPanel
    {
        private CoreConfig _draft;
        private string _newStageKey = string.Empty;
        private string _newStageValue = string.Empty;

        public CoreSettingsPanel()
        {
            try
            {
                var cfg = CoreServices.Locator.Get<IConfigurationService>();
                _draft = cfg?.Current ?? CoreConfig.CreateDefault();
            }
            catch
            {
                _draft = CoreConfig.CreateDefault();
            }
        }

        public void Draw(Rect inRect)
        {
            if (_draft == null) _draft = CoreConfig.CreateDefault();
            var list = new Listing_Standard();
            list.Begin(inRect);

            LabelWithTip(list,
                "工具调用模式（唯一选择）:",
                "- LightningFast: 仅当Top1高置信且工具支持快速文本直出，跳过二次LLM总结\n- FastTop1: Top1≥阈值则仅暴露该工具\n- NarrowTopK: 收缩TopK后由LLM自行选择\n- Classic: 暴露全部工具\n- Auto: 优先FastTop1，不足则NarrowTopK，再降级Classic");
            var modes = new[] { "LightningFast", "FastTop1", "NarrowTopK", "Classic", "Auto" };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                bool selected = string.Equals(_draft.Embedding.Tools.Mode, mode, System.StringComparison.OrdinalIgnoreCase);
                if (list.RadioButton(mode, selected))
                {
                    // 更新草稿（CoreConfig/子配置均为 init-only，不可原位修改）
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = _draft.Embedding.Enabled,
                            TopK = _draft.Embedding.TopK,
                            
                            MaxContextChars = _draft.Embedding.MaxContextChars,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = mode,
                                Top1Threshold = _draft.Embedding.Tools.Top1Threshold,
                                LightningTop1Threshold = _draft.Embedding.Tools.LightningTop1Threshold,
                                IndexPath = _draft.Embedding.Tools.IndexPath,
                                AutoBuildOnStart = _draft.Embedding.Tools.AutoBuildOnStart,
                                BlockDuringBuild = _draft.Embedding.Tools.BlockDuringBuild,
                                ScoreWeights = _draft.Embedding.Tools.ScoreWeights,
                                DynamicThresholds = _draft.Embedding.Tools.DynamicThresholds
                            }
                        }
                    };
                }
            }

            list.GapLine();

            // 工具匹配参数
            LabelWithTip(list, "工具匹配参数:", "用于控制工具检索与匹配的关键参数");

            // TopK（1~20）
            LabelWithTip(list, "TopK:", "NarrowTopK 模式下给 LLM 的候选工具个数，越大选择自由度越高，但提示词更长。");
            int curTopK = _draft.Embedding.TopK;
            float topKf = list.Slider(curTopK, 1f, 20f);
            int newTopK = Mathf.Clamp(Mathf.RoundToInt(topKf), 1, 20);
            list.Label($"TopK: {newTopK}");
            if (newTopK != curTopK)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft.Embedding.Enabled,
                        TopK = newTopK,
                        
                        MaxContextChars = _draft.Embedding.MaxContextChars,
                        Tools = _draft.Embedding.Tools
                    }
                };
            }

            // 阈值（Top1 / LightningTop1）
            var toolsCfg = _draft.Embedding.Tools;
            LabelWithTip(list, "Top1 阈值:", "FastTop1 模式的最低命中置信度（余弦相似度）。低于该值将降级到 NarrowTopK。");
            float curTop1 = (float)toolsCfg.Top1Threshold;
            float newTop1 = list.Slider(curTop1, 0.50f, 0.95f);
            list.Label($"Top1 阈值: {newTop1:F2}");

            LabelWithTip(list, "Lightning Top1 阈值:", "闪电直出模式的更高置信阈值。仅当Top1≥此值且工具支持快速文本直出时才无需二次LLM总结。");
            float curLight = (float)toolsCfg.LightningTop1Threshold;
            float newLight = list.Slider(curLight, 0.60f, 0.99f);
            newLight = Mathf.Max(newLight, newTop1 + 0.01f);
            list.Label($"Lightning Top1 阈值: {newLight:F2}");

            if (!Mathf.Approximately(newTop1, curTop1) || !Mathf.Approximately(newLight, curLight))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft.Embedding.Enabled,
                        TopK = _draft.Embedding.TopK,
                        
                        MaxContextChars = _draft.Embedding.MaxContextChars,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg.Mode,
                            Top1Threshold = newTop1,
                            LightningTop1Threshold = newLight,
                            IndexPath = toolsCfg.IndexPath,
                            AutoBuildOnStart = toolsCfg.AutoBuildOnStart,
                            BlockDuringBuild = toolsCfg.BlockDuringBuild,
                            ScoreWeights = toolsCfg.ScoreWeights,
                            DynamicThresholds = toolsCfg.DynamicThresholds
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 权重（Name / Description）：使用单滑条控制 Name，Description = 1 - Name
            LabelWithTip(list, "分数权重:", "用于合并 name/description 两条向量分数的权重。向右增加对工具名的偏置，向左增加对描述的偏置。");
            double curWName = toolsCfg.ScoreWeights?.Name ?? 0.6;
            float wNameF = list.Slider((float)curWName, 0f, 1f);
            float wDescF = 1f - wNameF;
            list.Label($"权重 Name: {wNameF:F2} / Description: {wDescF:F2}");
            if (!Mathf.Approximately(wNameF, (float)curWName))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft.Embedding.Enabled,
                        TopK = _draft.Embedding.TopK,
                        
                        MaxContextChars = _draft.Embedding.MaxContextChars,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg.Mode,
                            Top1Threshold = toolsCfg.Top1Threshold,
                            LightningTop1Threshold = toolsCfg.LightningTop1Threshold,
                            IndexPath = toolsCfg.IndexPath,
                            AutoBuildOnStart = toolsCfg.AutoBuildOnStart,
                            BlockDuringBuild = toolsCfg.BlockDuringBuild,
                            ScoreWeights = new EmbeddingToolsScoreWeights
                            {
                                Name = wNameF,
                                Description = wDescF
                            },
                            DynamicThresholds = toolsCfg.DynamicThresholds
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 构建/阻断选项
            LabelWithTip(list, "向量索引构建:", "控制工具向量索引的自动构建与阻断策略。");
            bool autoBuild = toolsCfg.AutoBuildOnStart;
            list.CheckboxLabeled("启动时自动构建索引", ref autoBuild);
            bool blockDuring = toolsCfg.BlockDuringBuild;
            list.CheckboxLabeled("构建期间阻断工具调用", ref blockDuring);
            if (autoBuild != toolsCfg.AutoBuildOnStart || blockDuring != toolsCfg.BlockDuringBuild)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft.Embedding.Enabled,
                        TopK = _draft.Embedding.TopK,
                        
                        MaxContextChars = _draft.Embedding.MaxContextChars,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg.Mode,
                            Top1Threshold = toolsCfg.Top1Threshold,
                            LightningTop1Threshold = toolsCfg.LightningTop1Threshold,
                            IndexPath = toolsCfg.IndexPath,
                            AutoBuildOnStart = autoBuild,
                            BlockDuringBuild = blockDuring,
                            ScoreWeights = toolsCfg.ScoreWeights,
                            DynamicThresholds = toolsCfg.DynamicThresholds
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 动态阈值
            var dyn = toolsCfg.DynamicThresholds ?? new EmbeddingDynamicThresholds();
            LabelWithTip(list, "动态阈值:", "根据近期命中质量对 Top1 阈值做平滑微调（保持在最小/最大阈值范围内）。");
            bool dynEnabled = dyn.Enabled;
            list.CheckboxLabeled("启用动态阈值", ref dynEnabled);
            float dynSmoothing = (float)dyn.Smoothing;
            dynSmoothing = list.Slider(dynSmoothing, 0f, 1f);
            list.Label($"动态阈值平滑: {dynSmoothing:F2}");
            float dynMin = (float)dyn.MinTop1;
            dynMin = list.Slider(dynMin, 0.50f, 0.95f);
            list.Label($"最小Top1阈值: {dynMin:F2}");
            float dynMax = (float)dyn.MaxTop1;
            dynMax = list.Slider(dynMax, 0.55f, 0.99f);
            dynMax = Mathf.Max(dynMax, dynMin + 0.01f);
            list.Label($"最大Top1阈值: {dynMax:F2}");

            if (dynEnabled != dyn.Enabled ||
                !Mathf.Approximately(dynSmoothing, (float)dyn.Smoothing) ||
                !Mathf.Approximately(dynMin, (float)dyn.MinTop1) ||
                !Mathf.Approximately(dynMax, (float)dyn.MaxTop1))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft.Embedding.Enabled,
                        TopK = _draft.Embedding.TopK,
                        
                        MaxContextChars = _draft.Embedding.MaxContextChars,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg.Mode,
                            Top1Threshold = toolsCfg.Top1Threshold,
                            LightningTop1Threshold = toolsCfg.LightningTop1Threshold,
                            IndexPath = toolsCfg.IndexPath,
                            AutoBuildOnStart = toolsCfg.AutoBuildOnStart,
                            BlockDuringBuild = toolsCfg.BlockDuringBuild,
                            ScoreWeights = toolsCfg.ScoreWeights,
                            DynamicThresholds = new EmbeddingDynamicThresholds
                            {
                                Enabled = dynEnabled,
                                Smoothing = dynSmoothing,
                                MinTop1 = dynMin,
                                MaxTop1 = dynMax
                            }
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 轻量规划器（S4）
            list.GapLine();
            LabelWithTip(list, "轻量规划器（S4）:", "将 RAG 命中与工具要点整合为 final_prompt 并注入 system 提示，增强总结质量（默认关闭）。");
            var planning = _draft.Orchestration.Planning;
            bool enablePlan = planning.EnableLightChaining;
            list.CheckboxLabeled("启用轻量规划器", ref enablePlan);
            int maxSteps = planning.MaxSteps;
            float stepsF = list.Slider(maxSteps, 1, 5);
            int newSteps = Mathf.Clamp(Mathf.RoundToInt(stepsF), 1, 5);
            list.Label($"MaxSteps: {newSteps}");
            // 其余参数
            bool allowParallel = planning.AllowParallel;
            list.CheckboxLabeled("阶段内允许只读小并发", ref allowParallel);
            int curMaxPar = planning.MaxParallelism;
            float maxParF = list.Slider(curMaxPar, 1, 8);
            int newMaxPar = Mathf.Clamp(Mathf.RoundToInt(maxParF), 1, 8);
            list.Label($"MaxParallelism: {newMaxPar}");

            int curFanout = planning.FanoutPerStage;
            float fanoutF = list.Slider(curFanout, 1, 5);
            int newFanout = Mathf.Clamp(Mathf.RoundToInt(fanoutF), 1, 5);
            list.Label($"Fanout/Stage: {newFanout}");

            double curSatis = planning.SatisfactionThreshold;
            float satisF = list.Slider((float)curSatis, 0.5f, 0.99f);
            double newSatis = System.Math.Round(satisF, 2);
            list.Label($"SatisfactionThreshold: {newSatis:F2}");

            if (enablePlan != planning.EnableLightChaining ||
                newSteps != planning.MaxSteps ||
                allowParallel != planning.AllowParallel ||
                newMaxPar != planning.MaxParallelism ||
                newFanout != planning.FanoutPerStage ||
                System.Math.Abs(newSatis - planning.SatisfactionThreshold) > 0.0001)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = _draft.Orchestration.Strategy,
                        Planning = new PlanningConfig
                        {
                            EnableLightChaining = enablePlan,
                            MaxSteps = newSteps,
                            AllowParallel = allowParallel,
                            MaxParallelism = newMaxPar,
                            FanoutPerStage = newFanout,
                            SatisfactionThreshold = newSatis
                        }
                    },
                    Embedding = _draft.Embedding
                };
                planning = _draft.Orchestration.Planning;
            }

            // 进度模板（即时反馈行的渲染格式）
            list.GapLine();
            LabelWithTip(list, "进度模板（可定制）:", "占位符：{Source} / {Stage} / {Message}；不同阶段可在下方为特定 Stage 指定模板。");
            var progressCfg = _draft.Orchestration.Progress;
            var curDefaultTpl = progressCfg.DefaultTemplate ?? "";
            var rectDef = list.GetRect(Text.LineHeight);
            var newDefaultTpl = Widgets.TextField(rectDef, curDefaultTpl);
            if (!string.Equals(newDefaultTpl, curDefaultTpl))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = _draft.Orchestration.Strategy,
                        Planning = _draft.Orchestration.Planning,
                        Progress = new OrchestrationProgressConfig
                        {
                            DefaultTemplate = newDefaultTpl,
                            StageTemplates = progressCfg.StageTemplates,
                            PayloadPreviewChars = progressCfg.PayloadPreviewChars
                        }
                    },
                    Embedding = _draft.Embedding
                };
                progressCfg = _draft.Orchestration.Progress;
            }

            int curPreview = progressCfg.PayloadPreviewChars;
            float previewF = list.Slider(curPreview, 0, 2000);
            int newPreview = Mathf.Clamp(Mathf.RoundToInt(previewF), 0, 2000);
            list.Label($"Payload 预览长度: {newPreview}");
            if (newPreview != curPreview)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = _draft.Orchestration.Strategy,
                        Planning = _draft.Orchestration.Planning,
                        Progress = new OrchestrationProgressConfig
                        {
                            DefaultTemplate = progressCfg.DefaultTemplate,
                            StageTemplates = progressCfg.StageTemplates,
                            PayloadPreviewChars = newPreview
                        }
                    },
                    Embedding = _draft.Embedding
                };
                progressCfg = _draft.Orchestration.Progress;
            }

            // 常用阶段模板快捷编辑
            var commonStages = new[] { "ToolMatch", "Planner", "FinalPrompt" };
            foreach (var stageKey in commonStages)
            {
                LabelWithTip(list, $"Stage 模板：{stageKey}", $"仅应用于 Stage={stageKey} 的进度事件。");
                progressCfg = _draft.Orchestration.Progress;
                string cur = null;
                if (progressCfg.StageTemplates != null && progressCfg.StageTemplates.TryGetValue(stageKey, out var v)) cur = v;
                var rect = list.GetRect(Text.LineHeight);
                var newTpl = Widgets.TextField(rect, cur ?? string.Empty);
                if (!string.Equals(cur ?? string.Empty, newTpl))
                {
                    var dict = new Dictionary<string, string>(progressCfg.StageTemplates ?? new Dictionary<string, string>());
                    if (string.IsNullOrWhiteSpace(newTpl)) dict.Remove(stageKey); else dict[stageKey] = newTpl;
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = new OrchestrationConfig
                        {
                            Strategy = _draft.Orchestration.Strategy,
                            Planning = _draft.Orchestration.Planning,
                            Progress = new OrchestrationProgressConfig
                            {
                                DefaultTemplate = progressCfg.DefaultTemplate,
                                StageTemplates = dict,
                                PayloadPreviewChars = progressCfg.PayloadPreviewChars
                            }
                        },
                        Embedding = _draft.Embedding
                    };
                }
            }

            // 自定义Stage模板添加/更新
            LabelWithTip(list, "自定义 Stage 模板:", "填入 Stage 名称与模板，点击添加/更新。留空模板可删除对应键。");
            var rectKey = list.GetRect(Text.LineHeight);
            _newStageKey = Widgets.TextField(rectKey, _newStageKey ?? string.Empty);
            var rectVal = list.GetRect(Text.LineHeight);
            _newStageValue = Widgets.TextField(rectVal, _newStageValue ?? string.Empty);
            if (list.ButtonText("添加/更新 Stage 模板"))
            {
                if (!string.IsNullOrWhiteSpace(_newStageKey))
                {
                    var dict = new Dictionary<string, string>(_draft.Orchestration.Progress.StageTemplates ?? new Dictionary<string, string>());
                    if (string.IsNullOrWhiteSpace(_newStageValue)) dict.Remove(_newStageKey); else dict[_newStageKey] = _newStageValue;
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = new OrchestrationConfig
                        {
                            Strategy = _draft.Orchestration.Strategy,
                            Planning = _draft.Orchestration.Planning,
                            Progress = new OrchestrationProgressConfig
                            {
                                DefaultTemplate = _draft.Orchestration.Progress.DefaultTemplate,
                                StageTemplates = dict,
                                PayloadPreviewChars = _draft.Orchestration.Progress.PayloadPreviewChars
                            }
                        },
                        Embedding = _draft.Embedding
                    };
                }
            }

            if (list.ButtonText("保存并应用"))
            {
                try
                {
                    var config = CoreServices.Locator.Get<IConfigurationService>();
                    config.Apply(_draft);
                    Verse.Messages.Message("RimAI: 设置已应用", RimWorld.MessageTypeDefOf.PositiveEvent, historical: false);
                }
                catch (System.Exception ex)
                {
                    Verse.Messages.Message("RimAI: 应用设置失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                }
            }

            list.Gap();
            list.Label("工具向量索引:");
            IToolVectorIndexService index = null;
            try { index = CoreServices.Locator.Get<IToolVectorIndexService>(); } catch { /* ignore */ }
            var state = index == null ? "Unavailable" : (index.IsBuilding ? "Building..." : (index.IsReady ? "Ready" : "Not Ready"));
            list.Label($"状态: {state}");
            if (index != null && list.ButtonText(index.IsBuilding ? "正在重建…" : "重建工具索引"))
            {
                try
                {
                    index.MarkStale();
                    _ = index.EnsureBuiltAsync();
                    Verse.Messages.Message("RimAI: 已触发工具索引重建", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                }
                catch (System.Exception ex)
                {
                    Verse.Messages.Message("RimAI: 重建索引失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                }
            }

            if (list.ButtonText("打开索引文件夹"))
            {
                try
                {
                    string dir = index?.IndexFilePath;
                    if (!string.IsNullOrWhiteSpace(dir) && File.Exists(dir))
                    {
                        dir = Path.GetDirectoryName(dir);
                    }
                    if (string.IsNullOrWhiteSpace(dir))
                    {
                        var cfg = CoreServices.Locator.Get<IConfigurationService>();
                        var basePath = cfg?.Current?.Embedding?.Tools?.IndexPath;
                        if (string.IsNullOrWhiteSpace(basePath) || string.Equals(basePath, "auto", System.StringComparison.OrdinalIgnoreCase))
                        {
                            dir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "RimWorld", "RimAI");
                        }
                        else
                        {
                            dir = basePath;
                        }
                    }
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    Process.Start("explorer.exe", dir);
                }
                catch (System.Exception ex)
                {
                    Verse.Messages.Message("RimAI: 打开文件夹失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                }
            }

            list.End();
        }

        private static void LabelWithTip(Listing_Standard list, string label, string tip)
        {
            var rect = list.GetRect(Text.LineHeight);
            Widgets.Label(rect, label);
            if (!string.IsNullOrEmpty(tip))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }
    }
}


