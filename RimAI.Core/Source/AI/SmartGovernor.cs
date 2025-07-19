using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Officers.Base;
using Verse;

namespace RimAI.Core.AI
{
    /// <summary>
    /// 智能总督 - 基于新架构的总督实现
    /// 展示如何使用统一的官员基类
    /// </summary>
    public class SmartGovernor : OfficerBase
    {
        private static SmartGovernor _instance;
        public static SmartGovernor Instance => _instance ??= new SmartGovernor();

        #region 官员基本信息

        public override string Name => "智能总督";
        public override string Description => "负责殖民地整体管理和紧急决策，提供全方位的管理建议";
        public override string IconPath => "UI/Icons/Governor"; // 可以自定义图标路径
        public override OfficerRole Role => OfficerRole.Governor;

        #endregion

        #region 模板配置

        protected override string QuickAdviceTemplateId => "governor.quick_decision";
        protected override string DetailedAdviceTemplateId => "governor.detailed_strategy";
        protected override string StreamingTemplateId => "narrator.event_narration";

        #endregion

        private SmartGovernor() : base() { }

        #region 核心上下文构建

        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            // 使用缓存提高性能
            var cacheKey = "governor_context_" + Find.TickManager.TicksGame / (GenTicks.TicksPerRealSecond * 300); // 5分钟更新一次

            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var context = new Dictionary<string, object>();

                    try
                    {
                        // 分析殖民地状态
                        var status = _analyzer.AnalyzeCurrentStatus();
                        var threats = _analyzer.IdentifyThreats();
                        var resources = _analyzer.GenerateResourceReport();
                        var overview = _analyzer.GetColonyOverview();

                        // 基础信息
                        context["colonistCount"] = status.ColonistCount;
                        context["threatLevel"] = status.ThreatLevel.ToString();
                        context["season"] = status.Season;
                        context["weather"] = status.WeatherCondition;

                        // 资源状况
                        context["resourceStatus"] = resources.OverallStatus;
                        context["resourceInventory"] = GenerateResourceInventory(status.ResourceLevels);
                        
                        // 威胁分析
                        context["threats"] = GenerateThreatsDescription(threats);
                        
                        // 殖民者详情
                        context["colonistDetails"] = GenerateColonistDetails(status.Colonists);
                        
                        // 活跃事件
                        context["activeEvents"] = string.Join(", ", status.ActiveEvents);
                        
                        // 殖民地概况
                        context["colonyStatus"] = overview;
                        
                        // 建筑和设施 (模拟数据)
                        context["buildings"] = "建筑设施分析中...";
                        context["research"] = "当前研究项目分析中...";
                        
                        // 默认值
                        context["situation"] = "请描述具体情况";
                        context["maxWords"] = "100";

                        Log.Message($"[SmartGovernor] Context built successfully with {context.Count} parameters");
                        return context;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[SmartGovernor] Failed to build context: {ex.Message}");
                        
                        // 返回基础上下文
                        return new Dictionary<string, object>
                        {
                            ["colonistCount"] = "未知",
                            ["threatLevel"] = "未知",
                            ["resourceStatus"] = "数据获取失败",
                            ["situation"] = "数据分析中...",
                            ["colonyStatus"] = "殖民地状态分析失败，请稍后重试"
                        };
                    }
                },
                TimeSpan.FromMinutes(5)
            );
        }

        #endregion

        #region 专业方法

        /// <summary>
        /// 获取快速决策建议 - 兼容旧接口
        /// </summary>
        public async Task<string> GetQuickDecision(string situation, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                context["situation"] = situation;
                context["maxWords"] = "100"; // 快速决策限制字数

                var prompt = _promptBuilder.BuildPrompt(QuickAdviceTemplateId, context);
                var options = CreateLLMOptions(temperature: 0.7f, forceStreaming: _llmService.IsStreamingAvailable);

                var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
                
                Log.Message($"[SmartGovernor] Quick decision provided for: {situation}");
                return response ?? "无法获取快速决策建议";
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Quick decision was cancelled");
                return "决策已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Quick decision failed: {ex.Message}");
                return GetErrorMessage(ex.Message);
            }
        }

        /// <summary>
        /// 获取详细管理策略 - 兼容旧接口
        /// </summary>
        public async Task<string> GetDetailedStrategy(string colonyStatus, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                if (!string.IsNullOrEmpty(colonyStatus))
                {
                    context["colonyStatus"] = colonyStatus;
                }

                var prompt = _promptBuilder.BuildPrompt(DetailedAdviceTemplateId, context);
                var options = CreateLLMOptions(temperature: 0.6f);

                var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
                
                Log.Message("[SmartGovernor] Detailed strategy generated");
                return response ?? "无法生成详细策略";
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Detailed strategy was cancelled");
                return "策略生成已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Detailed strategy failed: {ex.Message}");
                return GetErrorMessage(ex.Message);
            }
        }

        /// <summary>
        /// 获取实时事件解说 - 兼容旧接口
        /// </summary>
        public async Task<string> GetEventNarration(string eventDescription, Action<string> onPartialNarration = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                context["eventDescription"] = eventDescription;
                context["location"] = "殖民地";
                context["involvedPersonnel"] = "殖民者";
                context["currentSituation"] = "进行中";
                context["impact"] = "评估中";

                if (onPartialNarration != null && _llmService.IsStreamingAvailable)
                {
                    return await GetStreamingAdviceAsync(onPartialNarration, cancellationToken);
                }
                else
                {
                    var prompt = _promptBuilder.BuildPrompt(StreamingTemplateId, context);
                    var options = CreateLLMOptions(temperature: 0.8f);
                    
                    var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
                    Log.Message($"[SmartGovernor] Event narration completed: {eventDescription}");
                    
                    return response ?? "无法生成事件解说";
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message("[SmartGovernor] Event narration was cancelled");
                return "解说已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[SmartGovernor] Event narration failed: {ex.Message}");
                return GetErrorMessage(ex.Message);
            }
        }

        #endregion

        #region 专业状态信息

        protected override string GetProfessionalStatus()
        {
            try
            {
                var status = _analyzer.AnalyzeCurrentStatus();
                var threatCount = _analyzer.IdentifyThreats().Count;
                
                return $"管理 {status.ColonistCount} 名殖民者, {threatCount} 个威胁需关注";
            }
            catch
            {
                return "状态分析中...";
            }
        }

        #endregion

        #region 辅助方法

        private string GenerateResourceInventory(Dictionary<string, float> resourceLevels)
        {
            if (resourceLevels == null || resourceLevels.Count == 0)
                return "资源清单获取中...";

            var inventory = new System.Text.StringBuilder();
            foreach (var resource in resourceLevels)
            {
                inventory.AppendLine($"- {resource.Key}: {resource.Value:F0}");
            }
            return inventory.ToString();
        }

        private string GenerateThreatsDescription(List<ThreatInfo> threats)
        {
            if (threats == null || threats.Count == 0)
                return "暂无重大威胁";

            var description = new System.Text.StringBuilder();
            foreach (var threat in threats)
            {
                description.AppendLine($"- {threat.Description} ({threat.Level})");
            }
            return description.ToString();
        }

        private string GenerateColonistDetails(List<ColonistInfo> colonists)
        {
            if (colonists == null || colonists.Count == 0)
                return "殖民者信息获取中...";

            var details = new System.Text.StringBuilder();
            foreach (var colonist in colonists)
            {
                var skillsText = colonist.Skills.Count > 0 ? string.Join(", ", colonist.Skills) : "无特长";
                details.AppendLine($"- {colonist.Name} ({colonist.Profession}): {colonist.HealthStatus}, {colonist.MoodStatus}");
                details.AppendLine($"  技能: {skillsText}");
            }
            return details.ToString();
        }

        #endregion
    }
}
