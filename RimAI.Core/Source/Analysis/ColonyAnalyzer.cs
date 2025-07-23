using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Core.Services;
using RimAI.Core.Officers;

namespace RimAI.Core.Analysis
{
    /// <summary>
    /// 殖民地分析器 - 简化版本
    /// 提供基础的殖民地状态分析功能，作为官员决策的数据支撑
    /// 这个类是实现其他官员的重要参考模板
    /// </summary>
    public class ColonyAnalyzer : IColonyAnalyzer
    {
        private readonly ISafeAccessService _safeAccess;
        private readonly ICacheService _cache;

        public ColonyAnalyzer()
        {
            this._safeAccess = CoreServices.SafeAccessService;
            this._cache = CoreServices.CacheService; // Corrected to CacheService
        }

        #region 核心分析方法

        /// <summary>
        /// 获取完整的殖民地分析报告
        /// 这是主要的分析入口点，其他官员可以参考这个结构
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>结构化的分析数据</returns>
        public async Task<ColonyAnalysisResult> AnalyzeColonyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Log.Message("[ColonyAnalyzer] 开始殖民地分析...");
                
                var map = Find.CurrentMap;
                if (map == null)
                {
                    return CreateEmptyAnalysisResult("没有活动地图");
                }

                var result = new ColonyAnalysisResult
                {
                    AnalysisTime = DateTime.Now,
                    MapName = map.ToString()
                };

                // 并行执行各项分析（展示异步分析流的实现方式）
                var analysisTask = Task.Run(async () =>
                {
                    // 基础人口分析
                    result.PopulationData = await AnalyzePopulationAsync(map, cancellationToken);
                    
                    // 资源分析  
                    result.ResourceData = await AnalyzeResourcesAsync(map, cancellationToken);
                    
                    // 威胁分析
                    result.ThreatData = await AnalyzeThreatsAsync(map, cancellationToken);
                    
                    // 基础设施分析
                    result.InfrastructureData = await AnalyzeInfrastructureAsync(map, cancellationToken);
                    
                    // 计算总体风险等级
                    result.OverallRiskLevel = CalculateOverallRisk(result);
                    
                    return result;
                }, cancellationToken);

                var analysisResult = await analysisTask;
                
                Log.Message($"[ColonyAnalyzer] 分析完成，风险等级: {analysisResult.OverallRiskLevel}");
                return analysisResult;
            }
            catch (OperationCanceledException)
            {
                Log.Message("[ColonyAnalyzer] 分析被取消");
                return CreateEmptyAnalysisResult("分析被取消");
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] 分析失败: {ex.Message}");
                return CreateEmptyAnalysisResult($"分析失败: {ex.Message}");
            }
        }

        #endregion

        #region 具体分析实现

        /// <summary>
        /// 人口分析 - 分析殖民者状态、技能分布等
        /// 使用SafeAccessService统一处理并发访问问题
        /// 其他官员可以参考这个方法的结构来实现专业分析
        /// </summary>
        private async Task<PopulationAnalysis> AnalyzePopulationAsync(Map map, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                // 使用SafeAccessService安全获取集合，无需手动处理异常
                var colonists = _safeAccess.GetColonistsSafe(map);
                var prisoners = _safeAccess.GetPrisonersSafe(map);
                
                var analysis = new PopulationAnalysis
                {
                    TotalColonists = colonists.Count,
                    TotalPrisoners = prisoners.Count
                };

                // 健康状态分析 - 使用安全操作包装器
                analysis.HealthyColonists = _safeAccess.SafePawnOperation(
                    colonists,
                    pawns => pawns.Count(p => !p.Downed && !p.InBed() && p.health.hediffSet.PainTotal < 0.1f),
                    0,
                    "CountHealthyColonists"
                );

                analysis.InjuredColonists = _safeAccess.SafePawnOperation(
                    colonists,
                    pawns => pawns.Count(p => p.health.hediffSet.PainTotal >= 0.1f),
                    0,
                    "CountInjuredColonists"
                );

                analysis.DownedColonists = _safeAccess.SafePawnOperation(
                    colonists,
                    pawns => pawns.Count(p => p.Downed),
                    0,
                    "CountDownedColonists"
                );

                // 心情分析 - 使用安全操作包装器
                analysis.AverageMood = _safeAccess.SafePawnOperation(
                    colonists,
                    pawns => {
                        var moodSum = 0f;
                        var moodCount = 0;
                        foreach (var colonist in pawns)
                        {
                            try
                            {
                                if (colonist?.needs?.mood?.CurLevel != null)
                                {
                                    moodSum += colonist.needs.mood.CurLevel;
                                    moodCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[ColonyAnalyzer] Error accessing colonist mood: {ex.Message}");
                            }
                        }
                        return moodCount > 0 ? moodSum / moodCount : 0.5f;
                    },
                    0.5f,
                    "CalculateAverageMood"
                );

                // 技能分布分析
                analysis.SkillDistribution = _safeAccess.SafePawnOperation(
                    colonists,
                    pawns => AnalyzeSkillDistribution(pawns),
                    new Dictionary<string, float>(),
                    "AnalyzeSkillDistribution"
                );

                Log.Message($"[ColonyAnalyzer] 人口分析完成: {analysis.TotalColonists}人, 健康{analysis.HealthyColonists}人");
                return analysis;
            }, cancellationToken);
        }

        /// <summary>
        /// 资源分析 - 分析食物、材料、武器等储备情况
        /// 使用SafeAccessService统一处理RimWorld API访问
        /// </summary>
        private async Task<ResourceAnalysis> AnalyzeResourcesAsync(Map map, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var analysis = new ResourceAnalysis();

                // 食物分析 - 使用安全访问服务
                var foodItems = _safeAccess.GetThingGroupSafe(map, ThingRequestGroup.FoodSourceNotPlantOrTree);
                analysis.TotalFood = _safeAccess.SafeThingOperation(
                    foodItems,
                    items => items.Sum(t => 
                    {
                        try
                        {
                            return t.def.IsNutritionGivingIngestible ? t.stackCount * t.GetStatValue(StatDefOf.Nutrition) : 0f;
                        }
                        catch
                        {
                            return 0f;
                        }
                    }),
                    0f,
                    "CalculateTotalFood"
                );

                // 材料分析 - 使用安全访问服务
                analysis.Steel = _safeAccess.SafeThingOperation(
                    _safeAccess.GetThingsSafe(map, ThingDefOf.Steel),
                    items => items.Sum(t => t?.stackCount ?? 0),
                    0,
                    "CalculateSteel"
                );

                analysis.Wood = _safeAccess.SafeThingOperation(
                    _safeAccess.GetThingsSafe(map, ThingDefOf.WoodLog),
                    items => items.Sum(t => t?.stackCount ?? 0),
                    0,
                    "CalculateWood"
                );

                // 武器分析
                analysis.WeaponCount = _safeAccess.GetThingGroupSafe(map, ThingRequestGroup.Weapon).Count;

                // 计算储备天数
                var colonistCount = _safeAccess.GetColonistCountSafe(map);
                analysis.FoodDaysRemaining = colonistCount > 0 ? (int)(analysis.TotalFood / (colonistCount * 2.0f)) : 999;

                Log.Message($"[ColonyAnalyzer] 资源分析完成: 食物{analysis.TotalFood:F1}, 钢材{analysis.Steel}, 木材{analysis.Wood}");
                return analysis;
            }, cancellationToken);
        }

        /// <summary>
        /// 威胁分析 - 分析当前和潜在威胁
        /// 使用SafeAccessService处理并发访问问题
        /// </summary>
        private async Task<ThreatAnalysis> AnalyzeThreatsAsync(Map map, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var analysis = new ThreatAnalysis();

                // 敌对生物分析 - 使用安全访问服务
                var allPawns = _safeAccess.GetAllPawnsSafe(map);
                var hostilePawns = _safeAccess.SafePawnOperation(
                    allPawns,
                    pawns => pawns.Where(p => 
                    {
                        try
                        {
                            return p?.Faction != null && p.Faction.HostileTo(Faction.OfPlayer);
                        }
                        catch
                        {
                            return false;
                        }
                    }).ToList(),
                    new List<Pawn>(),
                    "FilterHostilePawns"
                );
                
                analysis.ActiveHostiles = hostilePawns.Count;
                analysis.HostileStrength = CalculateHostileStrength(hostilePawns);

                // 环境威胁
                analysis.WeatherThreat = CalculateWeatherThreat(map);
                
                // 火灾威胁 - 使用安全访问服务
                analysis.FireCount = _safeAccess.GetThingsSafe(map, ThingDefOf.Fire).Count;

                // 计算总威胁等级
                analysis.OverallThreatLevel = CalculateThreatLevel(analysis);

                Log.Message($"[ColonyAnalyzer] 威胁分析完成: 敌人{analysis.ActiveHostiles}个, 威胁等级{analysis.OverallThreatLevel}");
                return analysis;
            }, cancellationToken);
        }

        /// <summary>
        /// 基础设施分析 - 分析建筑、防御、电力等
        /// 使用SafeAccessService统一处理建筑访问
        /// </summary>
        private async Task<InfrastructureAnalysis> AnalyzeInfrastructureAsync(Map map, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var analysis = new InfrastructureAnalysis();

                // 使用SafeAccessService获取建筑列表
                var allBuildings = _safeAccess.GetBuildingsSafe(map);

                // 防御建筑分析 - 使用安全操作包装器
                analysis.DefensiveStructures = _safeAccess.SafeBuildingOperation(
                    allBuildings,
                    buildings => buildings.Where(b =>
                    {
                        try
                        {
                            return b?.def?.building?.IsTurret == true || 
                                   b?.def?.defName?.Contains("Wall") == true || 
                                   b?.def?.defName?.Contains("Door") == true;
                        }
                        catch
                        {
                            return false;
                        }
                    }).Count(),
                    0,
                    "CountDefensiveStructures"
                );

                // 电力系统分析
                analysis.PowerBuildings = _safeAccess.SafeBuildingOperation(
                    allBuildings,
                    buildings => buildings.Where(b => 
                    {
                        try
                        {
                            return b?.TryGetComp<CompPowerTrader>() != null;
                        }
                        catch
                        {
                            return false;
                        }
                    }).Count(),
                    0,
                    "CountPowerBuildings"
                );

                // 住房质量分析
                analysis.BedroomCount = _safeAccess.SafeBuildingOperation(
                    allBuildings,
                    buildings => buildings.Where(b => 
                    {
                        try
                        {
                            return b?.def?.defName?.Contains("Bed") == true;
                        }
                        catch
                        {
                            return false;
                        }
                    }).Count(),
                    0,
                    "CountBedrooms"
                );

                Log.Message($"[ColonyAnalyzer] 基础设施分析完成: 防御建筑{analysis.DefensiveStructures}个, 电力建筑{analysis.PowerBuildings}个");
                return analysis;
            }, cancellationToken);
        }

        #endregion

        #region 辅助计算方法

        /// <summary>
        /// 分析技能分布 - 识别殖民地的技能优势和短板
        /// </summary>
        private Dictionary<string, float> AnalyzeSkillDistribution(List<Pawn> colonists)
        {
            var skillAverages = new Dictionary<string, float>();
            
            var importantSkills = new[] { "Mining", "Construction", "Growing", "Cooking", "Medicine", "Shooting" };
            
            foreach (var skillName in importantSkills)
            {
                var skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(skillName);
                if (skillDef != null)
                {
                    var totalLevel = colonists.Sum(c => c.skills.GetSkill(skillDef).Level);
                    skillAverages[skillName] = colonists.Count > 0 ? (float)totalLevel / colonists.Count : 0f;
                }
            }

            return skillAverages;
        }

        /// <summary>
        /// 计算敌对单位的总体战斗力
        /// </summary>
        private float CalculateHostileStrength(List<Pawn> hostiles)
        {
            return hostiles.Sum(p => 
            {
                var weapon = p.equipment?.Primary;
                var baseStrength = p.RaceProps.baseHealthScale;
                var weaponMultiplier = weapon?.def.BaseMass ?? 1f;
                return baseStrength * weaponMultiplier;
            });
        }

        /// <summary>
        /// 计算天气威胁等级
        /// 使用SafeAccessService安全获取天气信息
        /// </summary>
        private float CalculateWeatherThreat(Map map)
        {
            var weather = _safeAccess.GetCurrentWeatherSafe(map);
            
            if (weather == null)
            {
                Log.Warning("[ColonyAnalyzer] Unable to get weather information");
                return 0.1f; // 默认低威胁
            }
            
            // 使用字符串比较来判断危险天气
            var weatherName = weather.defName.ToLower();
            
            if (weatherName.Contains("blizzard") || weatherName.Contains("tornado") || weatherName.Contains("storm"))
                return 0.8f;
            else if (weatherName.Contains("rain") || weatherName.Contains("snow") || weatherName.Contains("fog"))
                return 0.4f;
            else
                return 0.1f;
        }

        /// <summary>
        /// 计算综合威胁等级
        /// </summary>
        private RiskLevel CalculateThreatLevel(ThreatAnalysis threats)
        {
            var score = 0f;
            score += threats.ActiveHostiles * 0.3f;
            score += threats.HostileStrength * 0.1f;
            score += threats.WeatherThreat * 0.2f;
            score += threats.FireCount * 0.1f;

            if (score > 2.0f) return RiskLevel.Critical;
            if (score > 1.0f) return RiskLevel.High;
            if (score > 0.5f) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        /// <summary>
        /// 计算总体风险等级
        /// </summary>
        private RiskLevel CalculateOverallRisk(ColonyAnalysisResult result)
        {
            var riskFactors = 0;

            // 人口风险
            if (result.PopulationData.TotalColonists < 3) riskFactors++;
            if (result.PopulationData.AverageMood < 0.3f) riskFactors++;
            if (result.PopulationData.DownedColonists > result.PopulationData.TotalColonists * 0.3f) riskFactors++;

            // 资源风险
            if (result.ResourceData.FoodDaysRemaining < 5) riskFactors += 2;
            if (result.ResourceData.Steel < 100) riskFactors++;

            // 威胁风险
            if (result.ThreatData.OverallThreatLevel == RiskLevel.Critical) riskFactors += 3;
            else if (result.ThreatData.OverallThreatLevel == RiskLevel.High) riskFactors += 2;
            else if (result.ThreatData.OverallThreatLevel == RiskLevel.Medium) riskFactors += 1;

            // 基础设施风险
            if (result.InfrastructureData.DefensiveStructures < 5) riskFactors++;

            if (riskFactors >= 6) return RiskLevel.Critical;
            if (riskFactors >= 4) return RiskLevel.High;
            if (riskFactors >= 2) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        /// <summary>
        /// 创建空的分析结果（错误情况下使用）
        /// </summary>
        private ColonyAnalysisResult CreateEmptyAnalysisResult(string errorMessage)
        {
            return new ColonyAnalysisResult
            {
                AnalysisTime = DateTime.Now,
                MapName = "Unknown",
                OverallRiskLevel = RiskLevel.Low,
                PopulationData = new PopulationAnalysis(),
                ResourceData = new ResourceAnalysis(),
                ThreatData = new ThreatAnalysis(),
                InfrastructureData = new InfrastructureAnalysis(),
                ErrorMessage = errorMessage
            };
        }

        #endregion

        #region 快捷分析方法（供官员快速调用）

        /// <summary>
        /// 快速获取殖民地状态摘要 - 供Governor等官员快速调用
        /// </summary>
        public async Task<string> GetQuickStatusSummaryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var analysis = await AnalyzeColonyAsync(cancellationToken);
                
                return $"殖民者: {analysis.PopulationData.TotalColonists} " +
                       $"(健康{analysis.PopulationData.HealthyColonists}) | " +
                       $"食物: {analysis.ResourceData.FoodDaysRemaining}天 | " +
                       $"威胁: {analysis.ThreatData.OverallThreatLevel} | " +
                       $"总体风险: {analysis.OverallRiskLevel}";
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] 快速状态摘要失败: {ex.Message}");
                return $"状态获取失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取特定领域的详细分析 - 供专业官员调用
        /// </summary>
        public async Task<T> GetSpecializedAnalysisAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            var fullAnalysis = await AnalyzeColonyAsync(cancellationToken);
            
            if (typeof(T) == typeof(PopulationAnalysis))
                return fullAnalysis.PopulationData as T;
            else if (typeof(T) == typeof(ResourceAnalysis))
                return fullAnalysis.ResourceData as T;
            else if (typeof(T) == typeof(ThreatAnalysis))
                return fullAnalysis.ThreatData as T;
            else if (typeof(T) == typeof(InfrastructureAnalysis))
                return fullAnalysis.InfrastructureData as T;
            
            return null;
        }

        #endregion
    }

    #region 数据结构定义

    /// <summary>
    /// 完整的殖民地分析结果
    /// </summary>
    public class ColonyAnalysisResult
    {
        public DateTime AnalysisTime { get; set; }
        public string MapName { get; set; }
        public RiskLevel OverallRiskLevel { get; set; }
        public PopulationAnalysis PopulationData { get; set; }
        public ResourceAnalysis ResourceData { get; set; }
        public ThreatAnalysis ThreatData { get; set; }
        public InfrastructureAnalysis InfrastructureData { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 人口分析数据
    /// </summary>
    public class PopulationAnalysis
    {
        public int TotalColonists { get; set; }
        public int TotalPrisoners { get; set; }
        public int HealthyColonists { get; set; }
        public int InjuredColonists { get; set; }
        public int DownedColonists { get; set; }
        public float AverageMood { get; set; }
        public Dictionary<string, float> SkillDistribution { get; set; } = new Dictionary<string, float>();
    }

    /// <summary>
    /// 资源分析数据
    /// </summary>
    public class ResourceAnalysis
    {
        public float TotalFood { get; set; }
        public int Steel { get; set; }
        public int Wood { get; set; }
        public int WeaponCount { get; set; }
        public int FoodDaysRemaining { get; set; }
    }

    /// <summary>
    /// 威胁分析数据
    /// </summary>
    public class ThreatAnalysis
    {
        public int ActiveHostiles { get; set; }
        public float HostileStrength { get; set; }
        public float WeatherThreat { get; set; }
        public int FireCount { get; set; }
        public RiskLevel OverallThreatLevel { get; set; }
    }

    /// <summary>
    /// 基础设施分析数据
    /// </summary>
    public class InfrastructureAnalysis
    {
        public int DefensiveStructures { get; set; }
        public int PowerBuildings { get; set; }
        public int BedroomCount { get; set; }
    }

    /// <summary>
    /// 风险等级枚举
    /// </summary>
    public enum RiskLevel
    {
        Low = 0,      // 低风险 - 绿色
        Medium = 1,   // 中风险 - 黄色  
        High = 2,     // 高风险 - 橙色
        Critical = 3  // 严重风险 - 红色
    }

    #endregion
}
