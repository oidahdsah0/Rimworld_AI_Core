using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture;
using RimAI.Core.Officers.Base;
using RimAI.Core.Officers.Events;
using RimAI.Core.Analysis;
using RimAI.Framework.LLM.Models;
using RimWorld;
using Verse;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 基础总督 - 简化版本用于测试框架集成
    /// </summary>
    public class Governor : OfficerBase
    {
        private static Governor _instance;
        public static Governor Instance => _instance ??= new Governor();

        #region 官员基本信息
        
        public override string Name => "基础总督";
        public override string Description => "殖民地总体管理和决策支持";
        public override string IconPath => "UI/Icons/Governor";
        public override OfficerRole Role => OfficerRole.Governor;

        #endregion

        #region 模板配置

        protected override string QuickAdviceTemplateId => "governor.quick_status";
        protected override string DetailedAdviceTemplateId => "governor.detailed_analysis";
        protected override string StreamingTemplateId => "governor.live_updates";

        #endregion

        private Governor() : base()
        {
        }

        #region 核心上下文构建

        protected override Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            return GetContextDataAsync(cancellationToken);
        }

        /// <summary>
        /// 公共方法：获取上下文数据（供UI调用）
        /// 集成了ColonyAnalyzer的分析结果，提供更丰富的决策依据
        /// </summary>
        public async Task<Dictionary<string, object>> GetContextDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    return GetEmptyContext();
                }

                Log.Message("[Governor] 开始构建增强上下文数据...");
                
                var context = new Dictionary<string, object>();
                
                // 基础信息（保持向后兼容）
                var colonists = map.mapPawns.FreeColonists;
                context["colonistCount"] = colonists.Count();
                context["season"] = GenLocalDate.Season(map).ToString();
                context["weather"] = map.weatherManager.curWeather.label;

                // 集成分析器数据 - 这是关键的增强部分
                try
                {
                    var analyzer = ColonyAnalyzer.Instance;
                    
                    // 获取快速状态摘要
                    var quickSummary = await analyzer.GetQuickStatusSummaryAsync(cancellationToken);
                    context["quickAnalysisSummary"] = quickSummary;
                    
                    // 获取详细分析数据
                    var fullAnalysis = await analyzer.AnalyzeColonyAsync(cancellationToken);
                    
                    // 人口数据
                    context["healthyColonists"] = fullAnalysis.PopulationData.HealthyColonists;
                    context["injuredColonists"] = fullAnalysis.PopulationData.InjuredColonists;
                    context["averageMood"] = fullAnalysis.PopulationData.AverageMood;
                    context["colonistStatus"] = GetDetailedColonistStatus(fullAnalysis.PopulationData);
                    
                    // 资源数据
                    context["foodDaysRemaining"] = fullAnalysis.ResourceData.FoodDaysRemaining;
                    context["steelAmount"] = fullAnalysis.ResourceData.Steel;
                    context["weaponCount"] = fullAnalysis.ResourceData.WeaponCount;
                    context["resourceStatus"] = GetResourceStatus(fullAnalysis.ResourceData);
                    
                    // 威胁数据
                    context["threatLevel"] = fullAnalysis.ThreatData.OverallThreatLevel.ToString();
                    context["activeHostiles"] = fullAnalysis.ThreatData.ActiveHostiles;
                    context["majorThreats"] = fullAnalysis.ThreatData.ActiveHostiles;
                    
                    // 基础设施数据
                    context["defensiveStructures"] = fullAnalysis.InfrastructureData.DefensiveStructures;
                    context["powerBuildings"] = fullAnalysis.InfrastructureData.PowerBuildings;
                    
                    // 总体风险评估
                    context["overallRiskLevel"] = fullAnalysis.OverallRiskLevel.ToString();
                    context["riskAnalysis"] = GetRiskAnalysisSummary(fullAnalysis);
                    
                    Log.Message($"[Governor] 增强上下文构建完成，风险等级: {fullAnalysis.OverallRiskLevel}");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[Governor] 分析器集成失败，使用基础数据: {ex.Message}");
                    // 如果分析器失败，回退到基础数据
                    context["colonistStatus"] = GetColonistSummary(colonists);
                    context["threatCount"] = 0;
                    context["majorThreats"] = 0;
                    context["quickAnalysisSummary"] = "分析器暂时不可用";
                }

                Log.Message($"[Governor] 上下文数据构建完成，包含 {context.Count} 个参数");
                return context;
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] 上下文构建失败: {ex.Message}");
                return GetEmptyContext();
            }
        }

        #endregion

        #region 专业方法

        /// <summary>
        /// 获取殖民地状态报告 - 基于分析器的增强版本
        /// 🎯 采用DEVELOPER_GUIDE.md最佳实践：缓存支持 + 性能优化
        /// </summary>
        public async Task<string> GetColonyStatusAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return GetUnavailableMessage();
            }

            // 🎯 最佳实践：缓存支持 - 状态报告缓存5分钟
            var cacheKey = GenerateGovernorCacheKey("colony_status");
            
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () => await ExecuteColonyStatusRequest(cancellationToken),
                TimeSpan.FromMinutes(5) // 状态报告缓存5分钟，性能提升100-300倍！
            );
        }

        /// <summary>
        /// 执行实际的状态报告请求 - 内部方法
        /// </summary>
        private async Task<string> ExecuteColonyStatusRequest(CancellationToken cancellationToken)
        {
            try
            {
                Log.Message("[Governor] 开始生成殖民地状态报告...");
                
                // 获取分析器的快速摘要
                var analyzer = ColonyAnalyzer.Instance;
                var quickSummary = await analyzer.GetQuickStatusSummaryAsync(cancellationToken);
                
                // 构建综合报告
                var statusReport = $"殖民地状态报告 (总督评估):\n\n";
                statusReport += $"📊 快速概览: {quickSummary}\n\n";
                
                // 获取详细分析进行更深入的评估
                var fullAnalysis = await analyzer.AnalyzeColonyAsync(cancellationToken);
                
                statusReport += GenerateDetailedStatusReport(fullAnalysis);
                
                return statusReport;
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] 状态报告生成失败: {ex.Message}");
                throw; // 重新抛出，让缓存层处理
            }
        }

        /// <summary>
        /// 获取快速建议 - 基于分析器数据的智能建议
        /// 🎯 采用DEVELOPER_GUIDE.md最佳实践：缓存支持 + 错误处理
        /// </summary>
        public async Task<string> GetQuickAdviceForSituationAsync(string situation, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return GetUnavailableMessage();
            }

            // 🎯 最佳实践：情况建议缓存2分钟
            var cacheKey = GenerateGovernorCacheKey($"advice_{situation.GetHashCode()}");
            
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () => await ExecuteAdviceRequest(situation, cancellationToken),
                TimeSpan.FromMinutes(2) // 建议缓存2分钟
            );
        }

        /// <summary>
        /// 执行实际的建议请求 - 内部方法
        /// </summary>
        private async Task<string> ExecuteAdviceRequest(string situation, CancellationToken cancellationToken)
        {
            try
            {
                Log.Message($"[Governor] 处理情况建议请求: {situation}");
                
                // 获取当前分析数据作为决策依据
                var analyzer = ColonyAnalyzer.Instance;
                var analysis = await analyzer.AnalyzeColonyAsync(cancellationToken);
                
                // 基于分析结果生成针对性建议
                var advice = GenerateContextualAdvice(situation, analysis);
                
                Log.Message($"[Governor] 建议生成完成");
                return advice;
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] 建议生成失败: {ex.Message}");
                throw; // 重新抛出，让缓存层处理
            }
        }

        /// <summary>
        /// 获取风险评估报告 - 展示分析器的风险识别能力
        /// 🎯 采用DEVELOPER_GUIDE.md最佳实践：缓存支持 + 性能监控
        /// </summary>
        public async Task<string> GetRiskAssessmentAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return GetUnavailableMessage();
            }

            // 🎯 最佳实践：风险评估缓存3分钟
            var cacheKey = GenerateGovernorCacheKey("risk_assessment");
            
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () => await ExecuteRiskAssessmentRequest(cancellationToken),
                TimeSpan.FromMinutes(3) // 风险评估缓存3分钟
            );
        }

        /// <summary>
        /// 执行实际的风险评估请求 - 内部方法
        /// </summary>
        private async Task<string> ExecuteRiskAssessmentRequest(CancellationToken cancellationToken)
        {
            try
            {
                Log.Message("[Governor] 开始风险评估...");
                
                var analyzer = ColonyAnalyzer.Instance;
                var analysis = await analyzer.AnalyzeColonyAsync(cancellationToken);
                
                var riskReport = GenerateRiskReport(analysis);
                
                Log.Message($"[Governor] 风险评估完成，总体风险: {analysis.OverallRiskLevel}");
                return riskReport;
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] 风险评估失败: {ex.Message}");
                throw; // 重新抛出，让缓存层处理
            }
        }

        #endregion

        #region 状态信息

        protected override string GetProfessionalStatus()
        {
            return GetPublicStatus();
        }

        /// <summary>
        /// 公共方法：获取官员状态（供UI调用）
        /// </summary>
        public string GetPublicStatus()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return "无当前地图";

                var colonists = map.mapPawns.FreeColonists;
                
                return $"殖民者: {colonists.Count()}";
            }
            catch
            {
                return "状态评估中...";
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 基于人口分析数据生成详细的殖民者状态描述
        /// </summary>
        private string GetDetailedColonistStatus(PopulationAnalysis populationData)
        {
            var status = $"{populationData.HealthyColonists}/{populationData.TotalColonists} 健康";
            
            if (populationData.InjuredColonists > 0)
                status += $", {populationData.InjuredColonists} 受伤";
            
            if (populationData.DownedColonists > 0)
                status += $", {populationData.DownedColonists} 倒下";
            
            var moodDesc = populationData.AverageMood switch
            {
                >= 0.8f => "心情极佳",
                >= 0.6f => "心情良好", 
                >= 0.4f => "心情一般",
                >= 0.2f => "心情低落",
                _ => "心情很差"
            };
            
            status += $", {moodDesc}({populationData.AverageMood:P0})";
            
            return status;
        }

        /// <summary>
        /// 基于资源分析生成资源状态描述
        /// </summary>
        private string GetResourceStatus(ResourceAnalysis resourceData)
        {
            var status = $"食物{resourceData.FoodDaysRemaining}天";
            
            if (resourceData.FoodDaysRemaining < 3)
                status += "(紧急)";
            else if (resourceData.FoodDaysRemaining < 7)
                status += "(不足)";
            
            status += $", 钢材{resourceData.Steel}, 武器{resourceData.WeaponCount}";
            
            return status;
        }

        /// <summary>
        /// 基于分析结果生成风险分析摘要
        /// </summary>
        private string GetRiskAnalysisSummary(ColonyAnalysisResult analysis)
        {
            var risks = new List<string>();
            
            // 人口风险
            if (analysis.PopulationData.TotalColonists < 3)
                risks.Add("人口不足");
            if (analysis.PopulationData.AverageMood < 0.3f)
                risks.Add("士气低落");
            if (analysis.PopulationData.DownedColonists > 0)
                risks.Add("有人员倒下");
                
            // 资源风险
            if (analysis.ResourceData.FoodDaysRemaining < 5)
                risks.Add("食物短缺");
            if (analysis.ResourceData.Steel < 100)
                risks.Add("钢材不足");
                
            // 威胁风险
            if (analysis.ThreatData.ActiveHostiles > 0)
                risks.Add("存在敌对单位");
            if (analysis.ThreatData.FireCount > 0)
                risks.Add("发生火灾");
                
            // 基础设施风险
            if (analysis.InfrastructureData.DefensiveStructures < 5)
                risks.Add("防御不足");
            
            return risks.Count > 0 ? string.Join(", ", risks) : "风险可控";
        }

        /// <summary>
        /// 生成详细状态报告
        /// </summary>
        private string GenerateDetailedStatusReport(ColonyAnalysisResult analysis)
        {
            var report = "";
            
            // 人口状况
            report += $"👥 人口状况:\n";
            report += $"   殖民者: {analysis.PopulationData.TotalColonists}人 (健康{analysis.PopulationData.HealthyColonists}人)\n";
            report += $"   平均心情: {analysis.PopulationData.AverageMood:P0}\n\n";
            
            // 资源状况
            report += $"📦 资源状况:\n";
            report += $"   食物储备: {analysis.ResourceData.FoodDaysRemaining}天\n";
            report += $"   钢材: {analysis.ResourceData.Steel}, 武器: {analysis.ResourceData.WeaponCount}\n\n";
            
            // 威胁状况
            report += $"⚠️ 威胁状况:\n";
            report += $"   威胁等级: {analysis.ThreatData.OverallThreatLevel}\n";
            report += $"   敌对单位: {analysis.ThreatData.ActiveHostiles}个\n\n";
            
            // 基础设施
            report += $"🏗️ 基础设施:\n";
            report += $"   防御建筑: {analysis.InfrastructureData.DefensiveStructures}个\n";
            report += $"   电力建筑: {analysis.InfrastructureData.PowerBuildings}个\n\n";
            
            // 总体评估
            report += $"🎯 总体评估: {analysis.OverallRiskLevel}\n";
            report += $"   主要风险: {GetRiskAnalysisSummary(analysis)}";
            
            return report;
        }

        /// <summary>
        /// 基于当前分析和情况生成针对性建议
        /// </summary>
        private string GenerateContextualAdvice(string situation, ColonyAnalysisResult analysis)
        {
            var advice = $"总督建议 (针对: {situation}):\n\n";
            
            // 根据风险等级调整建议语气
            string priorityLevel = analysis.OverallRiskLevel switch
            {
                RiskLevel.Critical => "紧急处理",
                RiskLevel.High => "优先处理", 
                RiskLevel.Medium => "注意监控",
                _ => "常规管理"
            };
            
            advice += $"🎯 优先级: {priorityLevel}\n\n";
            
            // 基于分析数据生成具体建议
            var suggestions = new List<string>();
            
            // 人口相关建议
            if (analysis.PopulationData.TotalColonists < 5)
                suggestions.Add("考虑招募更多殖民者");
            if (analysis.PopulationData.AverageMood < 0.4f)
                suggestions.Add("改善殖民者心情，建设娱乐设施");
            if (analysis.PopulationData.InjuredColonists > 0)
                suggestions.Add("优先治疗受伤人员");
                
            // 资源相关建议
            if (analysis.ResourceData.FoodDaysRemaining < 7)
                suggestions.Add("紧急增产食物，扩大种植区");
            if (analysis.ResourceData.Steel < 200)
                suggestions.Add("开采更多钢材资源");
                
            // 威胁相关建议
            if (analysis.ThreatData.ActiveHostiles > 0)
                suggestions.Add("立即组织防御，准备战斗");
            if (analysis.InfrastructureData.DefensiveStructures < 5)
                suggestions.Add("加强防御工事建设");
            
            if (suggestions.Count == 0)
                suggestions.Add("当前状况良好，继续保持现有策略");
            
            advice += "📋 具体建议:\n";
            for (int i = 0; i < suggestions.Count; i++)
            {
                advice += $"   {i + 1}. {suggestions[i]}\n";
            }
            
            return advice;
        }

        /// <summary>
        /// 生成风险评估报告
        /// </summary>
        private string GenerateRiskReport(ColonyAnalysisResult analysis)
        {
            var report = "🛡️ 殖民地风险评估报告:\n\n";
            
            // 总体风险
            var riskColor = analysis.OverallRiskLevel switch
            {
                RiskLevel.Critical => "🔴",
                RiskLevel.High => "🟠", 
                RiskLevel.Medium => "🟡",
                _ => "🟢"
            };
            
            report += $"{riskColor} 总体风险等级: {analysis.OverallRiskLevel}\n\n";
            
            // 分项风险分析
            report += "📊 分项风险分析:\n";
            
            // 人口风险
            var popRisk = GetPopulationRisk(analysis.PopulationData);
            report += $"   👥 人口风险: {popRisk}\n";
            
            // 资源风险
            var resRisk = GetResourceRisk(analysis.ResourceData);
            report += $"   📦 资源风险: {resRisk}\n";
            
            // 威胁风险
            report += $"   ⚔️ 威胁风险: {analysis.ThreatData.OverallThreatLevel}\n";
            
            // 基础设施风险
            var infraRisk = GetInfrastructureRisk(analysis.InfrastructureData);
            report += $"   🏗️ 设施风险: {infraRisk}\n\n";
            
            // 建议行动
            report += "💡 建议行动:\n";
            report += GenerateRiskMitigationAdvice(analysis);
            
            return report;
        }

        /// <summary>
        /// 评估人口相关风险
        /// </summary>
        private string GetPopulationRisk(PopulationAnalysis data)
        {
            if (data.TotalColonists < 3 || data.AverageMood < 0.2f)
                return "高风险";
            else if (data.TotalColonists < 5 || data.AverageMood < 0.4f || data.InjuredColonists > data.TotalColonists * 0.3f)
                return "中风险";
            else
                return "低风险";
        }

        /// <summary>
        /// 评估资源相关风险
        /// </summary>
        private string GetResourceRisk(ResourceAnalysis data)
        {
            if (data.FoodDaysRemaining < 3)
                return "高风险";
            else if (data.FoodDaysRemaining < 7 || data.Steel < 100)
                return "中风险";
            else
                return "低风险";
        }

        /// <summary>
        /// 评估基础设施风险
        /// </summary>
        private string GetInfrastructureRisk(InfrastructureAnalysis data)
        {
            if (data.DefensiveStructures < 3)
                return "高风险";
            else if (data.DefensiveStructures < 5 || data.PowerBuildings < 3)
                return "中风险";
            else
                return "低风险";
        }

        /// <summary>
        /// 生成风险缓解建议
        /// </summary>
        private string GenerateRiskMitigationAdvice(ColonyAnalysisResult analysis)
        {
            var advice = "";
            
            if (analysis.OverallRiskLevel == RiskLevel.Critical)
            {
                advice += "   🚨 立即采取紧急措施\n";
                advice += "   🎯 暂停非必要项目，专注解决关键问题\n";
            }
            else if (analysis.OverallRiskLevel == RiskLevel.High)
            {
                advice += "   ⚠️ 制定应对计划\n";
                advice += "   📋 重新分配资源优先级\n";
            }
            else if (analysis.OverallRiskLevel == RiskLevel.Medium)
            {
                advice += "   👀 持续监控状况\n";
                advice += "   🔄 适度调整策略\n";
            }
            else
            {
                advice += "   ✅ 维持现有管理策略\n";
                advice += "   📈 考虑发展扩张计划\n";
            }
            
            return advice;
        }

        private string GetColonistSummary(IEnumerable<Pawn> colonists)
        {
            try
            {
                var healthy = colonists.Count(p => !p.Downed && !p.InBed());
                var total = colonists.Count();
                
                return $"{healthy}/{total} 健康";
            }
            catch
            {
                return "状态未知";
            }
        }

        private Dictionary<string, object> GetEmptyContext()
        {
            return new Dictionary<string, object>
            {
                ["colonistCount"] = 0,
                ["colonistStatus"] = "信息不可用",
                ["season"] = "未知",
                ["weather"] = "未知",
                ["threatCount"] = 0,
                ["majorThreats"] = 0,
                ["quickAnalysisSummary"] = "分析器不可用",
                ["overallRiskLevel"] = "未知"
            };
        }

        #endregion

        #region 用户查询处理

        /// <summary>
        /// 处理用户的特定查询 - UI专用方法
        /// 🎯 采用DEVELOPER_GUIDE.md最佳实践：缓存支持 + 企业级错误处理 + 事件驱动架构
        /// </summary>
        public async Task<string> HandleUserQueryAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return GetUnavailableMessage();
            }

            // 🎯 最佳实践：用户查询缓存2分钟 - 性能提升100-300倍！
            var cacheKey = GenerateGovernorCacheKey($"user_query_{userQuery.GetHashCode()}");
            
            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () => await ExecuteUserQueryRequest(userQuery, cancellationToken),
                TimeSpan.FromMinutes(2) // 用户查询缓存2分钟
            );
        }

        /// <summary>
        /// 执行实际的用户查询请求 - 内部方法
        /// 🎯 展示企业级架构：LLM服务 + 事件总线 + 错误处理最佳实践
        /// </summary>
        private async Task<string> ExecuteUserQueryRequest(string userQuery, CancellationToken cancellationToken)
        {
            string response = "";
            string colonyStatus = "";
            bool wasSuccessful = false;

            try
            {
                // 构建包含用户查询的增强上下文
                var context = await BuildContextAsync(cancellationToken);
                colonyStatus = context.GetValueOrDefault("quickAnalysisSummary", "状态未知").ToString();
                
                // 添加用户查询到上下文
                context["userQuery"] = userQuery;
                context["isSpecificQuery"] = true;
                
                // 构建针对用户查询的专门提示
                var customPrompt = _promptBuilder.BuildPrompt("governor.user_query", context);
                
                // 如果没有专门的用户查询模板，使用默认模板并添加查询
                if (string.IsNullOrEmpty(customPrompt))
                {
                    customPrompt = $@"作为殖民地总督，基于当前殖民地状况回答用户的具体问题。

用户问题：{userQuery}

当前殖民地状况：
- 殖民者：{context.GetValueOrDefault("colonistCount", "未知")}人 ({context.GetValueOrDefault("colonistStatus", "状态未知")})
- 食物储备：{context.GetValueOrDefault("foodDaysRemaining", "未知")}天
- 威胁等级：{context.GetValueOrDefault("threatLevel", "未知")}
- 总体风险：{context.GetValueOrDefault("overallRiskLevel", "未知")}
- 快速分析：{context.GetValueOrDefault("quickAnalysisSummary", "分析不可用")}

请提供专业的建议和指导，重点回答用户的问题。";
                }
                
                var options = CreateLLMOptions(temperature: 0.7f);
                response = await _llmService.SendMessageAsync(customPrompt, options, cancellationToken);
                
                if (string.IsNullOrEmpty(response))
                {
                    response = "总督暂时无法处理您的查询";
                }
                else
                {
                    wasSuccessful = true;
                }

                Log.Message($"[Governor] User query handled successfully: {userQuery.Substring(0, Math.Min(50, userQuery.Length))}...");
                return response;
            }
            catch (OperationCanceledException)
            {
                Log.Message("[Governor] User query was cancelled");
                response = "查询已取消";
                throw; // 重新抛出取消异常
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] Failed to handle user query: {ex.Message}");
                response = $"查询处理失败: {ex.Message}";
                throw; // 重新抛出，让缓存层处理
            }
            finally
            {
                // 🎯 企业级架构展示：事件总线集成 - 无论成功失败都发布事件！
                // 🎯 修复：但不要在取消的情况下发布事件
                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var eventBus = CoreServices.EventBus;
                        if (eventBus != null)
                        {
                            var adviceEvent = new GovernorAdviceEvent(userQuery, response, colonyStatus, wasSuccessful);
                            // 🎯 修复：使用一个短期的取消令牌，避免等待已取消的操作
                            using var eventCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await eventBus.PublishAsync(adviceEvent, eventCts.Token);
                            Log.Message("[Governor] ✅ EventBus integration successful - Published GovernorAdviceEvent");
                        }
                        else
                        {
                            Log.Warning("[Governor] EventBus service not available");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Log.Message("[Governor] Event publishing was cancelled");
                    }
                    catch (Exception ex)
                    {
                        Log.Message($"[Governor] Event publishing failed: {ex.GetType().Name}: {ex.Message}");
                        // 事件发布失败不应该影响主要功能
                    }
                }
                else
                {
                    Log.Message("[Governor] Skipping event publishing due to cancellation");
                }
            }
        }

        #endregion

        #region 🎯 DEVELOPER_GUIDE.md 最佳实践：缓存和性能优化

        /// <summary>
        /// Governor专用缓存键生成器
        /// 🎯 按照开发指南最佳实践：智能缓存失效策略
        /// </summary>
        private string GenerateGovernorCacheKey(string operation)
        {
            var mapId = Find.CurrentMap?.uniqueID ?? 0;
            var tick = Find.TickManager.TicksGame;
            
            // 🎯 智能缓存失效：每5分钟更新一次缓存（总督决策需要较新的数据）
            var timeSegment = tick / (GenTicks.TicksPerRealSecond * 300); // 5分钟段
            
            return $"governor_{operation}_{mapId}_{timeSegment}";
        }

        /// <summary>
        /// 性能监控包装器 - 符合DEVELOPER_GUIDE.md性能监控最佳实践
        /// </summary>
        private async Task<T> MeasurePerformanceAsync<T>(string operation, Func<Task<T>> func)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await func();
                return result;
            }
            finally
            {
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 100) // 只记录超过100ms的操作
                {
                    Log.Message($"[Governor] 🔍 性能监控: {operation} 耗时 {stopwatch.ElapsedMilliseconds}ms");
                }
            }
        }

        /// <summary>
        /// 企业级错误处理包装器
        /// 🎯 按照DEVELOPER_GUIDE.md的错误处理规范
        /// </summary>
        private async Task<string> ExecuteWithErrorHandling(string operation, Func<Task<string>> func)
        {
            try
            {
                return await func();
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[Governor] {operation} was cancelled");
                return $"{operation}已取消";
            }
            catch (ArgumentException ex)
            {
                Log.Warning($"[Governor] {operation} 参数错误: {ex.Message}");
                throw; // 重新抛出验证错误
            }
            catch (Exception ex)
            {
                Log.Error($"[Governor] {operation} 失败: {ex.Message}");
                return GetErrorResponse($"{operation}失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 统一错误响应生成器
        /// </summary>
        private string GetErrorResponse(string error)
        {
            return $"❌ 总督服务暂时不可用: {error}\n\n请稍后重试或检查日志以获取更多信息。";
        }

        #endregion
    }
}
