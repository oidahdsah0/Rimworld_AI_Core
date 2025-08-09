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
using System.Runtime.InteropServices;

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
        // 控件间距（像素）。可按需调整。
        public static float UIControlSpacing = 5f;

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

        private Vector2 _scrollPos;
        private float _scrollHeight;

        public void Draw(Rect inRect)
        {
            if (_draft == null) _draft = CoreConfig.CreateDefault();
            var list = new Listing_Standard();

            // 预留滚动条区域（固定内容高度，避免自动计算导致控件不渲染）
            var outRect = inRect;
            var contentHeight = 2000f; // 固定初始高度，可按需调整
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(outRect, ref _scrollPos, viewRect);
            list.Begin(viewRect);
            list.ColumnWidth = viewRect.width;

            int section = 1;
            SectionTitle(list, $"{section++}. 工具匹配 - 模式");
            LabelWithTip(list,
                "工具调用模式（唯一选择）:",
                "- LightningFast: 仅当Top1高置信且工具支持快速文本直出，跳过二次LLM总结\n- FastTop1: Top1≥阈值则仅暴露该工具\n- NarrowTopK: 收缩TopK后由LLM自行选择\n- Classic: 暴露全部工具\n- Auto: 优先FastTop1，不足则NarrowTopK，再降级Classic");
            var modes = new[] { "LightningFast", "FastTop1", "NarrowTopK", "Classic", "Auto" };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                bool selected = string.Equals((_draft?.Embedding?.Tools?.Mode) ?? "Classic", mode, System.StringComparison.OrdinalIgnoreCase);
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
                            Enabled = _draft?.Embedding?.Enabled ?? true,
                            TopK = _draft?.Embedding?.TopK ?? 5,
                            
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = mode,
                                Top1Threshold = _draft?.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = _draft?.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = _draft?.Embedding?.Tools?.IndexPath ?? "auto",
                                AutoBuildOnStart = _draft?.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = _draft?.Embedding?.Tools?.BlockDuringBuild ?? true,
                                ScoreWeights = _draft?.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = _draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        }
                    };
                }
                list.Gap(UIControlSpacing);
            }

            // 保存/重置：仅作用于「工具匹配 - 模式」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var draftTools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = cur.Embedding?.TopK ?? 5,
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = new EmbeddingToolsConfig
                                {
                                    Mode = draftTools.Mode, // 仅更新模式
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘工具匹配-模式’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    // 恢复为默认 FastTop1 模式（不立即应用，仅更新草稿）
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = _draft?.Embedding?.Enabled ?? true,
                            TopK = _draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = "FastTop1",
                                Top1Threshold = _draft?.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = _draft?.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = _draft?.Embedding?.Tools?.IndexPath ?? "auto",
                                AutoBuildOnStart = _draft?.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = _draft?.Embedding?.Tools?.BlockDuringBuild ?? true,
                                ScoreWeights = _draft?.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = _draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        }
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 工具匹配 - 参数");
            // 工具匹配参数
            LabelWithTip(list, "工具匹配参数:", "用于控制工具检索与匹配的关键参数");

            // TopK（1~20）
            int curTopK = _draft?.Embedding?.TopK ?? 5;
            LabelWithTip(list, $"TopK: {curTopK}", "NarrowTopK 模式下给 LLM 的候选工具个数，越大选择自由度越高，但提示词更长。");
            float topKf = list.Slider(curTopK, 1f, 20f);
            int newTopK = Mathf.Clamp(Mathf.RoundToInt(topKf), 1, 20);
            list.Gap(UIControlSpacing);
            if (newTopK != curTopK)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft?.Embedding?.Enabled ?? true,
                        TopK = newTopK,
                        
                        MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig()
                    }
                };
            }

            // 保存/重置：仅作用于「工具匹配 - 参数」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = _draft?.Embedding?.TopK ?? (cur.Embedding?.TopK ?? 5), // 仅更新 TopK
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = cur.Embedding?.Tools ?? new EmbeddingToolsConfig()
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘工具匹配-参数’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = true,
                            TopK = 5,
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig()
                        }
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 工具匹配 - 阈值与权重");
            // 阈值（Top1 / LightningTop1）
            var toolsCfg = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
            float curTop1 = (float)(toolsCfg?.Top1Threshold ?? 0.82);
            LabelWithTip(list, $"Top1 阈值: {curTop1:F2}", "FastTop1 模式的最低命中置信度（余弦相似度）。低于该值将降级到 NarrowTopK。");
            float newTop1 = list.Slider(curTop1, 0.50f, 0.95f);
            list.Gap(UIControlSpacing);

            float curLight = (float)(toolsCfg?.LightningTop1Threshold ?? 0.86);
            LabelWithTip(list, $"Lightning Top1 阈值: {curLight:F2}", "闪电直出模式的更高置信阈值。仅当Top1≥此值且工具支持快速文本直出时才无需二次LLM总结。");
            float newLight = list.Slider(curLight, 0.60f, 0.99f);
            newLight = Mathf.Max(newLight, newTop1 + 0.01f);
            list.Gap(UIControlSpacing);

            if (!Mathf.Approximately(newTop1, curTop1) || !Mathf.Approximately(newLight, curLight))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft?.Embedding?.Enabled ?? true,
                        TopK = _draft?.Embedding?.TopK ?? 5,
                        
                        MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg?.Mode ?? "Classic",
                            Top1Threshold = newTop1,
                            LightningTop1Threshold = newLight,
                            IndexPath = toolsCfg?.IndexPath ?? "auto",
                            AutoBuildOnStart = toolsCfg?.AutoBuildOnStart ?? true,
                            BlockDuringBuild = toolsCfg?.BlockDuringBuild ?? true,
                            ScoreWeights = toolsCfg?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                            DynamicThresholds = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 权重（Name / Description）：使用单滑条控制 Name，Description = 1 - Name
            double curWName = toolsCfg?.ScoreWeights?.Name ?? 0.6;
            LabelWithTip(list, $"分数权重 Name: {curWName:F2} / Description: {(1 - curWName):F2}", "用于合并 name/description 两条向量分数的权重。向右增加对工具名的偏置，向左增加对描述的偏置。");
            float wNameF = list.Slider((float)curWName, 0f, 1f);
            float wDescF = 1f - wNameF;
            list.Gap(UIControlSpacing);
            if (!Mathf.Approximately(wNameF, (float)curWName))
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft?.Embedding?.Enabled ?? true,
                        TopK = _draft?.Embedding?.TopK ?? 5,
                        
                        MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg?.Mode ?? "Classic",
                            Top1Threshold = toolsCfg?.Top1Threshold ?? 0.82,
                            LightningTop1Threshold = toolsCfg?.LightningTop1Threshold ?? 0.86,
                            IndexPath = toolsCfg?.IndexPath ?? "auto",
                            AutoBuildOnStart = toolsCfg?.AutoBuildOnStart ?? true,
                            BlockDuringBuild = toolsCfg?.BlockDuringBuild ?? true,
                            ScoreWeights = new EmbeddingToolsScoreWeights
                            {
                                Name = wNameF,
                                Description = wDescF
                            },
                            DynamicThresholds = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 保存/重置：仅作用于「工具匹配 - 阈值与权重」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dt = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = cur.Embedding?.TopK ?? 5,
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = new EmbeddingToolsConfig
                                {
                                    Mode = cur.Embedding?.Tools?.Mode ?? "Classic",
                                    Top1Threshold = dt.Top1Threshold,
                                    LightningTop1Threshold = dt.LightningTop1Threshold,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = dt.ScoreWeights ?? new EmbeddingToolsScoreWeights { Name = 0.6, Description = 0.4 },
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘阈值与权重’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    var currentTools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = _draft?.Embedding?.Enabled ?? true,
                            TopK = _draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = currentTools?.Mode ?? "Classic",
                                Top1Threshold = 0.82,
                                LightningTop1Threshold = 0.86,
                                IndexPath = currentTools?.IndexPath ?? "auto",
                                AutoBuildOnStart = currentTools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = currentTools?.BlockDuringBuild ?? true,
                                ScoreWeights = new EmbeddingToolsScoreWeights { Name = 0.6, Description = 0.4 },
                                DynamicThresholds = currentTools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        }
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 向量索引 - 构建与阻断");
            // 构建/阻断选项
            LabelWithTip(list, "向量索引构建:", "控制工具向量索引的自动构建与阻断策略。");
            bool autoBuild = toolsCfg?.AutoBuildOnStart ?? true;
            list.CheckboxLabeled("启动时自动构建索引", ref autoBuild);
            list.Gap(UIControlSpacing);
            bool blockDuring = toolsCfg?.BlockDuringBuild ?? true;
            list.CheckboxLabeled("构建期间阻断工具调用", ref blockDuring);
            list.Gap(UIControlSpacing);
            if (autoBuild != toolsCfg.AutoBuildOnStart || blockDuring != toolsCfg.BlockDuringBuild)
            {
                _draft = new CoreConfig
                {
                    LLM = _draft.LLM,
                    EventAggregator = _draft.EventAggregator,
                    Orchestration = _draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = _draft?.Embedding?.Enabled ?? true,
                        TopK = _draft?.Embedding?.TopK ?? 5,
                        
                        MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg?.Mode ?? "Classic",
                            Top1Threshold = toolsCfg?.Top1Threshold ?? 0.82,
                            LightningTop1Threshold = toolsCfg?.LightningTop1Threshold ?? 0.86,
                            IndexPath = toolsCfg?.IndexPath ?? "auto",
                            AutoBuildOnStart = autoBuild,
                            BlockDuringBuild = blockDuring,
                            ScoreWeights = toolsCfg?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                            DynamicThresholds = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                        }
                    }
                };
                toolsCfg = _draft.Embedding.Tools;
            }

            // 保存/重置：仅作用于「向量索引 - 构建与阻断」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dt = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = cur.Embedding?.TopK ?? 5,
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = new EmbeddingToolsConfig
                                {
                                    Mode = cur.Embedding?.Tools?.Mode ?? "Classic",
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = dt.AutoBuildOnStart,
                                    BlockDuringBuild = dt.BlockDuringBuild,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘构建与阻断’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    var currentTools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = _draft?.Embedding?.Enabled ?? true,
                            TopK = _draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = currentTools?.Mode ?? "Classic",
                                Top1Threshold = currentTools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = currentTools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = currentTools?.IndexPath ?? "auto",
                                AutoBuildOnStart = true,
                                BlockDuringBuild = true,
                                ScoreWeights = currentTools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = currentTools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        }
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 工具匹配 - 动态阈值");
            // 动态阈值
            var dyn = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds();
            LabelWithTip(list, "动态阈值:", "根据近期命中质量对 Top1 阈值做平滑微调（保持在最小/最大阈值范围内）。");
            bool dynEnabled = dyn?.Enabled ?? true;
            list.CheckboxLabeled("启用动态阈值", ref dynEnabled);
            list.Gap(UIControlSpacing);
            float dynSmoothing = (float)(dyn?.Smoothing ?? 0.2);
            LabelWithTip(list, $"动态阈值平滑: {dynSmoothing:F2}", "");
            dynSmoothing = list.Slider(dynSmoothing, 0f, 1f);
            list.Gap(UIControlSpacing);
            float dynMin = (float)(dyn?.MinTop1 ?? 0.78);
            LabelWithTip(list, $"最小Top1阈值: {dynMin:F2}", "");
            dynMin = list.Slider(dynMin, 0.50f, 0.95f);
            list.Gap(UIControlSpacing);
            float dynMax = (float)(dyn?.MaxTop1 ?? 0.90);
            LabelWithTip(list, $"最大Top1阈值: {dynMax:F2}", "");
            dynMax = list.Slider(dynMax, 0.55f, 0.99f);
            dynMax = Mathf.Max(dynMax, dynMin + 0.01f);
            list.Gap(UIControlSpacing);

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
            // 保存/重置：仅作用于「工具匹配 - 动态阈值」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dt = _draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = cur.Embedding?.TopK ?? 5,
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = new EmbeddingToolsConfig
                                {
                                    Mode = cur.Embedding?.Tools?.Mode ?? "Classic",
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = new EmbeddingDynamicThresholds
                                    {
                                        Enabled = dt.Enabled,
                                        Smoothing = dt.Smoothing,
                                        MinTop1 = dt.MinTop1,
                                        MaxTop1 = dt.MaxTop1
                                    }
                                }
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘动态阈值’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    var currentTools = _draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                    _draft = new CoreConfig
                    {
                        LLM = _draft.LLM,
                        EventAggregator = _draft.EventAggregator,
                        Orchestration = _draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = _draft?.Embedding?.Enabled ?? true,
                            TopK = _draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = _draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = currentTools?.Mode ?? "Classic",
                                Top1Threshold = currentTools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = currentTools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = currentTools?.IndexPath ?? "auto",
                                AutoBuildOnStart = currentTools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = currentTools?.BlockDuringBuild ?? true,
                                ScoreWeights = currentTools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = new EmbeddingDynamicThresholds { Enabled = true, Smoothing = 0.2, MinTop1 = 0.78, MaxTop1 = 0.90 }
                            }
                        }
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 编排 - 轻量规划器（S4）");
            LabelWithTip(list, "轻量规划器（S4）:", "将 RAG 命中与工具要点整合为 final_prompt 并注入 system 提示，增强总结质量（默认关闭）。");
            var planning = _draft.Orchestration.Planning;
            bool enablePlan = planning.EnableLightChaining;
            list.CheckboxLabeled("启用轻量规划器", ref enablePlan);
            list.Gap(UIControlSpacing);
            int maxSteps = planning.MaxSteps;
            LabelWithTip(list, $"MaxSteps: {maxSteps}", "");
            float stepsF = list.Slider(maxSteps, 1, 5);
            int newSteps = Mathf.Clamp(Mathf.RoundToInt(stepsF), 1, 5);
            list.Gap(UIControlSpacing);
            // 其余参数
            bool allowParallel = planning.AllowParallel;
            list.CheckboxLabeled("阶段内允许只读小并发", ref allowParallel);
            list.Gap(UIControlSpacing);
            int curMaxPar = planning.MaxParallelism;
            LabelWithTip(list, $"MaxParallelism: {curMaxPar}", "");
            float maxParF = list.Slider(curMaxPar, 1, 8);
            int newMaxPar = Mathf.Clamp(Mathf.RoundToInt(maxParF), 1, 8);
            list.Gap(UIControlSpacing);

            int curFanout = planning.FanoutPerStage;
            LabelWithTip(list, $"Fanout/Stage: {curFanout}", "");
            float fanoutF = list.Slider(curFanout, 1, 5);
            int newFanout = Mathf.Clamp(Mathf.RoundToInt(fanoutF), 1, 5);
            list.Gap(UIControlSpacing);

            double curSatis = planning.SatisfactionThreshold;
            LabelWithTip(list, $"SatisfactionThreshold: {curSatis:F2}", "");
            float satisF = list.Slider((float)curSatis, 0.5f, 0.99f);
            double newSatis = System.Math.Round(satisF, 2);
            list.Gap(UIControlSpacing);

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
            // 保存/重置：仅作用于「编排 - 轻量规划器（S4）」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dp = _draft?.Orchestration?.Planning ?? new PlanningConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = new OrchestrationConfig
                            {
                                Strategy = cur.Orchestration?.Strategy ?? "Classic",
                                Planning = new PlanningConfig
                                {
                                    EnableLightChaining = dp.EnableLightChaining,
                                    MaxSteps = dp.MaxSteps,
                                    AllowParallel = dp.AllowParallel,
                                    MaxParallelism = dp.MaxParallelism,
                                    FanoutPerStage = dp.FanoutPerStage,
                                    SatisfactionThreshold = dp.SatisfactionThreshold
                                },
                                Progress = cur.Orchestration?.Progress ?? new OrchestrationProgressConfig()
                            },
                            Embedding = cur.Embedding
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘轻量规划器’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
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
                                EnableLightChaining = false,
                                MaxSteps = 3,
                                AllowParallel = false,
                                MaxParallelism = 2,
                                FanoutPerStage = 3,
                                SatisfactionThreshold = 0.8
                            },
                            Progress = _draft.Orchestration.Progress
                        },
                        Embedding = _draft.Embedding
                    };
                });

            list.GapLine();
            SectionTitle(list, $"{section++}. 编排 - 进度模板");
            LabelWithTip(list, "进度模板（可定制）:", "占位符：{Source} / {Stage} / {Message}；不同阶段可在下方为特定 Stage 指定模板。");
            var progressCfg = _draft.Orchestration.Progress;
            var curDefaultTpl = progressCfg.DefaultTemplate ?? "";
            var rectDef = list.GetRect(Text.LineHeight);
            var newDefaultTpl = Widgets.TextField(rectDef, curDefaultTpl);
            list.Gap(UIControlSpacing);
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
            LabelWithTip(list, $"Payload 预览长度: {curPreview}", "");
            float previewF = list.Slider(curPreview, 0, 2000);
            int newPreview = Mathf.Clamp(Mathf.RoundToInt(previewF), 0, 2000);
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

            // 保存/重置：仅作用于「编排 - 进度模板」本区字段
            DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var pr = _draft?.Orchestration?.Progress ?? new OrchestrationProgressConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = new OrchestrationConfig
                            {
                                Strategy = cur.Orchestration?.Strategy ?? "Classic",
                                Planning = cur.Orchestration?.Planning ?? new PlanningConfig(),
                                Progress = new OrchestrationProgressConfig
                                {
                                    DefaultTemplate = pr.DefaultTemplate,
                                    StageTemplates = pr.StageTemplates,
                                    PayloadPreviewChars = pr.PayloadPreviewChars
                                }
                            },
                            Embedding = cur.Embedding
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘进度模板’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
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
                                DefaultTemplate = "[${Source}] ${Stage}: ${Message}".Replace("${","{").Replace("}","}"),
                                StageTemplates = new Dictionary<string, string>(),
                                PayloadPreviewChars = 200
                            }
                        },
                        Embedding = _draft.Embedding
                    };
                });

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
                list.Gap(UIControlSpacing);
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
            list.GapLine();
            SectionTitle(list, $"{section++}. 编排 - 自定义 Stage 模板");
            LabelWithTip(list, "自定义 Stage 模板:", "左侧填入 Stage 名称，右侧填入模板文本；点击添加/更新。留空模板可删除对应键。");
            var row = list.GetRect(Text.LineHeight);
            float labelW = 80f;
            float half = row.width / 2f;
            // 左半：Stage 名称
            var keyLabel = new Rect(row.x, row.y, labelW, row.height);
            Widgets.Label(keyLabel, "Stage 名称");
            var keyBox = new Rect(row.x + labelW, row.y, half - labelW - 8f, row.height);
            _newStageKey = Widgets.TextField(keyBox, _newStageKey ?? string.Empty);
            // 右半：模板文本
            var valLabel = new Rect(row.x + half, row.y, labelW, row.height);
            Widgets.Label(valLabel, "模板");
            var valBox = new Rect(row.x + half + labelW, row.y, half - labelW - 8f, row.height);
            _newStageValue = Widgets.TextField(valBox, _newStageValue ?? string.Empty);
            list.Gap(UIControlSpacing);
            // 水平按钮：添加/更新 与 保存并应用
            var actionRow = list.GetRect(32f);
            float halfW = (actionRow.width - UIControlSpacing) / 2f;
            var addBtnRect = new Rect(actionRow.x, actionRow.y, halfW, actionRow.height);
            var saveBtnRect = new Rect(actionRow.x + halfW + UIControlSpacing, actionRow.y, halfW, actionRow.height);

            if (Widgets.ButtonText(addBtnRect, "添加/更新 Stage 模板"))
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

            if (Widgets.ButtonText(saveBtnRect, "保存并应用"))
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
            // 区域重置按钮（仅重置“自定义 Stage 模板输入框”）
            if (DrawResetButton(list, "重置本区设置"))
            {
                _newStageKey = string.Empty;
                _newStageValue = string.Empty;
            }

            list.GapLine();
            SectionTitle(list, $"{section++}. 索引管理");
            list.Label("工具向量索引:");
            IToolVectorIndexService index = null;
            try { index = CoreServices.Locator.Get<IToolVectorIndexService>(); } catch { /* ignore */ }
            var state = index == null ? "Unavailable" : (index.IsBuilding ? "Building..." : (index.IsReady ? "Ready" : "Not Ready"));
            list.Label($"状态: {state}");
            list.Gap(UIControlSpacing);
            // 水平按钮：重建索引 + 打开文件夹（与上方按钮风格对齐，等分宽度）
            var btnRow = list.GetRect(32f);
            float halfW2 = (btnRow.width - UIControlSpacing) / 2f;
            var btn1 = new Rect(btnRow.x, btnRow.y, halfW2, btnRow.height);
            var btn2 = new Rect(btnRow.x + halfW2 + UIControlSpacing, btnRow.y, halfW2, btnRow.height);

            if (index != null)
            {
                if (Widgets.ButtonText(btn1, index.IsBuilding ? "正在重建…" : "重建工具索引"))
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
            }
            if (Widgets.ButtonText(btn2, "打开索引文件夹"))
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
                            dir = GetDefaultIndexBasePath();
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
            _scrollHeight = list.CurHeight; // 当前不参与布局，仅保留为开发参考
            Widgets.EndScrollView();
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

        private static void SectionTitle(Listing_Standard list, string title)
        {
            var old = Text.Font;
            Text.Font = GameFont.Medium;
            var rect = list.GetRect(Text.LineHeight + 6f);
            Widgets.Label(rect, title);
            Text.Font = old;
        }

        private static bool DrawResetButton(Listing_Standard list, string label)
        {
            var row = list.GetRect(28f);
            float width = 160f;
            var rect = new Rect(row.xMax - width, row.y, width, row.height);
            return Widgets.ButtonText(rect, label);
        }

        /// <summary>
        /// 在当前区块尾部绘制 “保存本区设置 / 重置本区设置” 两个按钮，并绑定回调。
        /// </summary>
        private static void DrawSaveResetRow(Listing_Standard list, string saveLabel, System.Action onSave, string resetLabel, System.Action onReset)
        {
            var row = list.GetRect(28f);
            float w = 160f;
            float gap = 8f;
            var saveRect = new Rect(row.xMax - (w * 2 + gap), row.y, w, row.height);
            var resetRect = new Rect(row.xMax - w, row.y, w, row.height);
            if (Widgets.ButtonText(saveRect, saveLabel)) onSave?.Invoke();
            if (Widgets.ButtonText(resetRect, resetLabel)) onReset?.Invoke();
        }

        private static string GetDefaultIndexBasePath()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var local = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
                    var appDataDir = Directory.GetParent(local)?.Parent?.FullName ?? local; // .../AppData
                    var localLow = System.IO.Path.Combine(appDataDir, "LocalLow");
                    return System.IO.Path.Combine(localLow,
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    return System.IO.Path.Combine(home,
                        "Library", "Application Support",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    return System.IO.Path.Combine(home,
                        ".config", "unity3d",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
            }
            catch { }
            // fallback
            return System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "RimWorld", "RimAI");
        }
    }
}


