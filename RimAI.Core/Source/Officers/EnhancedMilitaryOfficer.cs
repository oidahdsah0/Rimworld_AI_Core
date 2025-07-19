using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Events;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Officers.Base;
using RimWorld;
using Verse;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 新架构军事官员 - 展示完整的架构能力
    /// </summary>
    public class MilitaryOfficer : OfficerBase
    {
        private static MilitaryOfficer _instance;
        public static MilitaryOfficer Instance => _instance ??= new MilitaryOfficer();

        #region 官员基本信息

        public override string Name => "军事指挥官";
        public override string Description => "负责殖民地防务、战斗策略和威胁应对";
        public override string IconPath => "UI/Icons/Military";
        public override OfficerRole Role => OfficerRole.Military;

        #endregion

        #region 模板配置

        protected override string QuickAdviceTemplateId => "military.threat_analysis";
        protected override string DetailedAdviceTemplateId => "military.defense_strategy";
        protected override string StreamingTemplateId => "military.battle_report";

        #endregion

        private MilitaryOfficer() : base()
        {
            // 订阅威胁检测事件
            var eventBus = _cacheService.GetOrCreateAsync(
                "eventbus_subscription",
                () => Task.FromResult(CoreServices.EventBus),
                TimeSpan.FromHours(1)
            ).Result;

            if (eventBus != null)
            {
                eventBus.Subscribe<ThreatDetectedEvent>(new MilitaryThreatHandler(this));
                Log.Message("[MilitaryOfficer] Subscribed to threat detection events");
            }
        }

        #region 核心上下文构建

        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = "military_context_" + Find.TickManager.TicksGame / (GenTicks.TicksPerRealSecond * 180); // 3分钟更新

            return await _cacheService.GetOrCreateAsync(
                cacheKey,
                async () =>
                {
                    var context = new Dictionary<string, object>();
                    
                    try
                    {
                        var map = Find.CurrentMap;
                        if (map == null)
                        {
                            return GetEmptyMilitaryContext();
                        }

                        // 基础状态分析
                        var colonyStatus = _analyzer.AnalyzeCurrentStatus();
                        var threats = _analyzer.IdentifyThreats();

                        // 军事力量评估
                        var combatCapability = AnalyzeCombatCapability(map);
                        var defensivePositions = AnalyzeDefensivePositions(map);
                        var threatAssessment = AnalyzeThreatSituation(threats, map);

                        // 构建上下文
                        context["threatInfo"] = GenerateThreatSummary(threats);
                        context["combatPersonnel"] = combatCapability.Personnel;
                        context["weapons"] = combatCapability.WeaponStatus;
                        context["defenses"] = defensivePositions.Summary;
                        context["tacticalAdvantages"] = defensivePositions.Advantages;
                        
                        // 环境因素
                        context["terrain"] = GetTerrainAssessment(map);
                        context["weather"] = colonyStatus.WeatherCondition;
                        context["visibility"] = GetVisibilityCondition(map);
                        
                        // 态势分析
                        context["overallThreatLevel"] = threatAssessment.Level.ToString();
                        context["priorityThreats"] = threatAssessment.PriorityThreats;
                        context["recommendedActions"] = threatAssessment.RecommendedActions;

                        Log.Message($"[MilitaryOfficer] Military context built with {context.Count} parameters");
                        return context;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[MilitaryOfficer] Failed to build military context: {ex.Message}");
                        return GetEmptyMilitaryContext();
                    }
                },
                TimeSpan.FromMinutes(3)
            );
        }

        #endregion

        #region 专业军事方法

        /// <summary>
        /// 威胁评估分析
        /// </summary>
        public async Task<ThreatAssessmentResult> AnalyzeThreatAsync(ThreatInfo threat, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                context["specificThreat"] = $"类型: {threat.Type}, 等级: {threat.Level}, 描述: {threat.Description}";
                
                // 使用结构化响应获取详细分析
                var analysis = await GetStructuredAdviceAsync<ThreatAssessmentResult>(cancellationToken);
                
                if (analysis != null)
                {
                    Log.Message($"[MilitaryOfficer] Threat analysis completed for: {threat.Type}");
                    return analysis;
                }

                // 回退到文本分析
                var textAnalysis = await GetAdviceAsync(cancellationToken);
                return new ThreatAssessmentResult
                {
                    ThreatLevel = threat.Level,
                    Analysis = textAnalysis,
                    Recommendations = new List<string> { "详细分析不可用，建议手动评估" }
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] Threat analysis failed: {ex.Message}");
                return new ThreatAssessmentResult
                {
                    ThreatLevel = ThreatLevel.Medium,
                    Analysis = $"威胁分析失败: {ex.Message}",
                    Recommendations = new List<string> { "建议手动制定应对策略" }
                };
            }
        }

        /// <summary>
        /// 战斗准备建议
        /// </summary>
        public async Task<BattlePreparationAdvice> GetBattlePreparationAsync(string situation, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                context["battleSituation"] = situation;
                context["preparation"] = "战前准备分析";

                var advice = await GetStructuredAdviceAsync<BattlePreparationAdvice>(cancellationToken);
                
                if (advice != null)
                {
                    Log.Message("[MilitaryOfficer] Battle preparation advice generated");
                    return advice;
                }

                // 文本回退
                var textAdvice = await GetAdviceAsync(cancellationToken);
                return new BattlePreparationAdvice
                {
                    PreparationSteps = new List<string> { textAdvice },
                    EstimatedPreparationTime = "未知",
                    CriticalResources = new List<string> { "需要详细评估" }
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] Battle preparation failed: {ex.Message}");
                return new BattlePreparationAdvice
                {
                    PreparationSteps = new List<string> { $"准备建议生成失败: {ex.Message}" },
                    EstimatedPreparationTime = "未知",
                    CriticalResources = new List<string>()
                };
            }
        }

        #endregion

        #region 专业状态信息

        protected override string GetProfessionalStatus()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return "无当前地图";

                var combatCapability = AnalyzeCombatCapability(map);
                var threats = _analyzer.IdentifyThreats();
                var militaryThreats = threats.Count(t => t.Type.Contains("敌") || t.Type.Contains("威胁") || t.Type.Contains("攻击"));

                return $"战备状态: {combatCapability.ReadinessLevel}, {militaryThreats} 个军事威胁";
            }
            catch
            {
                return "军事态势评估中...";
            }
        }

        #endregion

        #region 军事分析辅助方法

        private CombatCapability AnalyzeCombatCapability(Map map)
        {
            try
            {
                var colonists = map.mapPawns.FreeColonists;
                var combatants = colonists.Where(p => !p.Downed && !p.InBed() && 
                    (p.skills?.GetSkill(SkillDefOf.Shooting)?.Level > 3 || 
                     p.skills?.GetSkill(SkillDefOf.Melee)?.Level > 3)).ToList();

                var weapons = GetWeaponInventory(map);
                
                return new CombatCapability
                {
                    Personnel = $"{combatants.Count}/{colonists.Count()} 可战斗人员",
                    WeaponStatus = $"{weapons.Count} 件武器装备",
                    ReadinessLevel = combatants.Count >= colonists.Count() * 0.7 ? "高" : 
                                   combatants.Count >= colonists.Count() * 0.5 ? "中" : "低"
                };
            }
            catch (Exception ex)
            {
                Log.Warning($"[MilitaryOfficer] Combat capability analysis failed: {ex.Message}");
                return new CombatCapability
                {
                    Personnel = "战力评估失败",
                    WeaponStatus = "装备统计失败", 
                    ReadinessLevel = "未知"
                };
            }
        }

        private DefensivePositions AnalyzeDefensivePositions(Map map)
        {
            try
            {
                // 分析地图的防御优势
                var buildings = map.listerBuildings.allBuildingsColonist;
                var walls = buildings.Count(b => b.def.building?.isInert == true && b.def.fillPercent >= 0.75f);
                var turrets = buildings.Count(b => b.def.building?.turretGunDef != null);

                return new DefensivePositions
                {
                    Summary = $"防御工事: {walls} 面墙体, {turrets} 个炮塔",
                    Advantages = walls > 50 ? "坚固防线" : turrets > 3 ? "火力优势" : "防御薄弱"
                };
            }
            catch (Exception ex)
            {
                Log.Warning($"[MilitaryOfficer] Defensive position analysis failed: {ex.Message}");
                return new DefensivePositions
                {
                    Summary = "防御分析失败",
                    Advantages = "需要手动评估"
                };
            }
        }

        private ThreatSituationAnalysis AnalyzeThreatSituation(List<ThreatInfo> threats, Map map)
        {
            var analysis = new ThreatSituationAnalysis();
            
            if (threats == null || threats.Count == 0)
            {
                analysis.Level = ThreatLevel.None;
                analysis.PriorityThreats = "暂无威胁";
                analysis.RecommendedActions = "保持常规警戒";
                return analysis;
            }

            // 评估最高威胁等级
            analysis.Level = threats.Max(t => t.Level);
            
            // 优先威胁
            var highPriorityThreats = threats.Where(t => t.Level >= ThreatLevel.High).ToList();
            analysis.PriorityThreats = highPriorityThreats.Count > 0 ? 
                string.Join(", ", highPriorityThreats.Select(t => t.Type)) : "无高优先级威胁";

            // 推荐行动
            analysis.RecommendedActions = analysis.Level switch
            {
                ThreatLevel.Critical => "立即进入战备状态，准备紧急防御",
                ThreatLevel.High => "加强警戒，准备应战",
                ThreatLevel.Medium => "提高防御等级，准备预案",
                ThreatLevel.Low => "保持基本警戒",
                _ => "继续日常巡逻"
            };

            return analysis;
        }

        private List<string> GetWeaponInventory(Map map)
        {
            var weapons = new List<string>();
            
            try
            {
                var allItems = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
                foreach (var item in allItems.Take(10)) // 限制数量
                {
                    weapons.Add($"{item.def.label} x{item.stackCount}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MilitaryOfficer] Weapon inventory failed: {ex.Message}");
                weapons.Add("武器清点失败");
            }

            return weapons;
        }

        private string GenerateThreatSummary(List<ThreatInfo> threats)
        {
            if (threats == null || threats.Count == 0)
                return "当前无重大威胁";

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"检测到 {threats.Count} 个威胁:");
            
            foreach (var threat in threats.Take(5)) // 限制显示数量
            {
                summary.AppendLine($"- {threat.Type}: {threat.Description} (等级: {threat.Level})");
            }

            return summary.ToString();
        }

        private string GetTerrainAssessment(Map map)
        {
            try
            {
                var terrainData = map.terrainGrid;
                // 简单的地形评估
                return "复合地形 - 需要详细侦察";
            }
            catch
            {
                return "地形信息不可用";
            }
        }

        private string GetVisibilityCondition(Map map)
        {
            try
            {
                var weather = map.weatherManager.curWeather;
                if (weather.def.defName.Contains("Fog") || weather.def.defName.Contains("Rain"))
                    return "能见度降低";
                    
                return "能见度良好";
            }
            catch
            {
                return "能见度未知";
            }
        }

        private Dictionary<string, object> GetEmptyMilitaryContext()
        {
            return new Dictionary<string, object>
            {
                ["threatInfo"] = "威胁信息获取失败",
                ["combatPersonnel"] = "战力统计不可用",
                ["weapons"] = "装备清单不可用",
                ["defenses"] = "防御态势未知",
                ["tacticalAdvantages"] = "战术评估失败",
                ["terrain"] = "地形未知",
                ["weather"] = "天气未知",
                ["visibility"] = "能见度未知",
                ["overallThreatLevel"] = "未知",
                ["priorityThreats"] = "威胁评估失败",
                ["recommendedActions"] = "建议获取军事情报"
            };
        }

        #endregion

        #region 数据模型

        private class CombatCapability
        {
            public string Personnel { get; set; }
            public string WeaponStatus { get; set; }
            public string ReadinessLevel { get; set; }
        }

        private class DefensivePositions
        {
            public string Summary { get; set; }
            public string Advantages { get; set; }
        }

        private class ThreatSituationAnalysis
        {
            public ThreatLevel Level { get; set; }
            public string PriorityThreats { get; set; }
            public string RecommendedActions { get; set; }
        }

        #endregion
    }

    #region 军事相关数据模型

    public class ThreatAssessmentResult
    {
        public ThreatLevel ThreatLevel { get; set; }
        public string Analysis { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public DateTime AssessmentTime { get; set; } = DateTime.Now;
    }

    public class BattlePreparationAdvice
    {
        public List<string> PreparationSteps { get; set; } = new List<string>();
        public string EstimatedPreparationTime { get; set; }
        public List<string> CriticalResources { get; set; } = new List<string>();
    }

    #endregion

    #region 事件处理器

    /// <summary>
    /// 军事威胁事件处理器
    /// </summary>
    public class MilitaryThreatHandler : IEventHandler<ThreatDetectedEvent>
    {
        private readonly MilitaryOfficer _officer;

        public MilitaryThreatHandler(MilitaryOfficer officer)
        {
            _officer = officer;
        }

        public async Task HandleAsync(ThreatDetectedEvent eventData, CancellationToken cancellationToken = default)
        {
            try
            {
                Log.Message($"[MilitaryThreatHandler] Processing threat: {eventData.Threat.Type}");
                
                // 自动分析威胁
                var analysis = await _officer.AnalyzeThreatAsync(eventData.Threat, cancellationToken);
                
                // 根据威胁等级决定是否发送通知
                if (eventData.Threat.Level >= ThreatLevel.High)
                {
                    var message = $"⚠️ 高等级威胁检测: {eventData.Threat.Description}\n建议: {analysis.Analysis}";
                    Messages.Message(message, MessageTypeDefOf.ThreatBig);
                }

                Log.Message($"[MilitaryThreatHandler] Threat analysis completed");
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryThreatHandler] Failed to handle threat event: {ex.Message}");
            }
        }

        public Task HandleAsync(IEvent eventData, CancellationToken cancellationToken = default)
        {
            if (eventData is ThreatDetectedEvent threatEvent)
            {
                return HandleAsync(threatEvent, cancellationToken);
            }
            return Task.CompletedTask;
        }
    }

    #endregion
}
