using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Architecture.Interfaces;
using RimWorld;
using Verse;

namespace RimAI.Core.Analysis
{
    /// <summary>
    /// 安全分析器 - 负责分析殖民地安全状况和防御能力
    /// </summary>
    public class SecurityAnalyzer
    {
        private static SecurityAnalyzer _instance;
        public static SecurityAnalyzer Instance => _instance ??= new SecurityAnalyzer();

        private SecurityAnalyzer() { }

        /// <summary>
        /// 分析整体安全状况
        /// </summary>
        public SecurityReport AnalyzeSecurity()
        {
            var report = new SecurityReport();

            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    Log.Warning("[SecurityAnalyzer] No current map available");
                    return CreateEmptyReport();
                }

                report.DefenseLevel = EvaluateDefenseLevel(map);
                report.WeaponStatus = AnalyzeWeapons(map);
                report.WallIntegrity = AnalyzeWalls(map);
                report.GuardPostsCoverage = AnalyzeGuardPosts(map);
                report.TrapEffectiveness = AnalyzeTraps(map);
                report.SecurityThreats = IdentifySecurityThreats(map);
                report.RecommendedActions = GenerateSecurityRecommendations(report);
                report.LastUpdated = DateTime.Now;

            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze security: {ex.Message}");
                report.DefenseLevel = DefenseLevel.Unknown;
                report.SecurityThreats.Add("安全分析失败");
            }

            return report;
        }

        /// <summary>
        /// 分析防御工事
        /// </summary>
        public DefenseStructureReport AnalyzeDefenseStructures()
        {
            var report = new DefenseStructureReport();

            try
            {
                var map = Find.CurrentMap;
                if (map == null) return report;

                report.Walls = AnalyzeWallStructures(map);
                report.Gates = AnalyzeGates(map);
                report.Turrets = AnalyzeTurrets(map);
                report.Barriers = AnalyzeBarriers(map);
                report.WeakPoints = IdentifyWeakPoints(map);
                report.CoverageGaps = FindCoverageGaps(map);

            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze defense structures: {ex.Message}");
            }

            return report;
        }

        /// <summary>
        /// 评估威胁响应能力
        /// </summary>
        public ThreatResponseCapability EvaluateThreatResponse()
        {
            var capability = new ThreatResponseCapability();

            try
            {
                var map = Find.CurrentMap;
                if (map == null) return capability;

                capability.CombatReadiness = EvaluateCombatReadiness(map);
                capability.WeaponAvailability = EvaluateWeaponAvailability(map);
                capability.MedicalSupport = EvaluateMedicalSupport(map);
                capability.CommunicationSystems = EvaluateCommunication(map);
                capability.ResponseTime = EstimateResponseTime(map);
                capability.OverallRating = CalculateOverallRating(capability);

            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to evaluate threat response: {ex.Message}");
                capability.OverallRating = "评估失败";
            }

            return capability;
        }

        #region 私有分析方法

        private DefenseLevel EvaluateDefenseLevel(Map map)
        {
            try
            {
                int defenseScore = 0;

                // 评估墙体防护
                var walls = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.isInert == true && b.def.fillPercent > 0.5f)
                    .Count();
                defenseScore += Math.Min(walls / 10, 20);

                // 评估武器装备
                var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Count();
                defenseScore += Math.Min(weapons / 5, 15);

                // 评估炮塔
                var turrets = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.IsTurret == true)
                    .Count();
                defenseScore += Math.Min(turrets * 5, 25);

                // 评估陷阱
                var traps = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.isTrap == true)
                    .Count();
                defenseScore += Math.Min(traps, 10);

                // 评估战斗人员
                var fighters = map.mapPawns.FreeColonists
                    .Where(p => p.skills.GetSkill(SkillDefOf.Shooting).Level > 5 || 
                               p.skills.GetSkill(SkillDefOf.Melee).Level > 5)
                    .Count();
                defenseScore += Math.Min(fighters * 3, 30);

                return defenseScore switch
                {
                    < 20 => DefenseLevel.Minimal,
                    < 40 => DefenseLevel.Basic,
                    < 60 => DefenseLevel.Adequate,
                    < 80 => DefenseLevel.Strong,
                    _ => DefenseLevel.Fortress
                };
            }
            catch
            {
                return DefenseLevel.Unknown;
            }
        }

        private WeaponStatusReport AnalyzeWeapons(Map map)
        {
            var report = new WeaponStatusReport();

            try
            {
                var weapons = map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon);
                
                report.TotalWeapons = weapons.Count();
                report.MeleeWeapons = weapons.Count(w => w.def.IsMeleeWeapon);
                report.RangedWeapons = weapons.Count(w => w.def.IsRangedWeapon);
                
                // 按质量分类
                foreach (var weapon in weapons)
                {
                    var quality = weapon.TryGetQuality(out QualityCategory qc) ? qc : QualityCategory.Normal;
                    
                    if (!report.WeaponsByQuality.ContainsKey(quality))
                        report.WeaponsByQuality[quality] = 0;
                    
                    report.WeaponsByQuality[quality]++;
                }

                // 评估武器状态
                report.Status = report.TotalWeapons switch
                {
                    < 5 => "武器严重不足",
                    < 10 => "武器不足",
                    < 20 => "武器充足",
                    _ => "武器充裕"
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze weapons: {ex.Message}");
                report.Status = "武器分析失败";
            }

            return report;
        }

        private WallIntegrityReport AnalyzeWalls(Map map)
        {
            var report = new WallIntegrityReport();

            try
            {
                var walls = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.isInert == true && b.def.fillPercent > 0.8f)
                    .ToList();

                report.TotalWallSections = walls.Count;
                report.DamagedSections = walls.Count(w => w.HitPoints < w.MaxHitPoints * 0.8f);
                report.WeakMaterials = walls.Count(w => w.def.building.Material?.GetStatValueAbstract(StatDefOf.MaxHitPoints) < 100);
                
                var integrityPercentage = report.TotalWallSections > 0 ? 
                    ((report.TotalWallSections - report.DamagedSections) * 100 / report.TotalWallSections) : 100;

                report.OverallIntegrity = integrityPercentage switch
                {
                    < 50 => "墙体破损严重",
                    < 70 => "墙体需要维修",
                    < 90 => "墙体状况良好",
                    _ => "墙体完好"
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze walls: {ex.Message}");
                report.OverallIntegrity = "墙体分析失败";
            }

            return report;
        }

        private List<string> AnalyzeGuardPosts(Map map)
        {
            var coverage = new List<string>();

            try
            {
                var guardPosts = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.hasInteractionCell)
                    .ToList();

                if (guardPosts.Count == 0)
                {
                    coverage.Add("缺少观察哨");
                }
                else
                {
                    coverage.Add($"共有 {guardPosts.Count} 个潜在观察点");
                }

                // 检查关键区域覆盖
                var entrances = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.IsDoor == true)
                    .Count();

                if (entrances > guardPosts.Count * 2)
                {
                    coverage.Add("入口点监控不足");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze guard posts: {ex.Message}");
                coverage.Add("观察哨分析失败");
            }

            return coverage;
        }

        private string AnalyzeTraps(Map map)
        {
            try
            {
                var traps = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.isTrap == true)
                    .ToList();

                if (traps.Count == 0)
                    return "未部署陷阱";

                var armedTraps = traps.Count(t => t.GetComp<CompExplosive>()?.wickStarted != true);
                var disarmedTraps = traps.Count - armedTraps;

                return $"陷阱部署: {armedTraps} 个有效, {disarmedTraps} 个需要重置";
            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to analyze traps: {ex.Message}");
                return "陷阱分析失败";
            }
        }

        private List<string> IdentifySecurityThreats(Map map)
        {
            var threats = new List<string>();

            try
            {
                // 检查入侵者
                var enemies = map.mapPawns.AllPawns
                    .Where(p => p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
                    .Count();

                if (enemies > 0)
                {
                    threats.Add($"发现 {enemies} 个敌对目标");
                }

                // 检查火灾风险
                var flammableBuildings = map.listerBuildings.allBuildingsColonist
                    .Count(b => b.def.building?.Flammable == true);

                if (flammableBuildings > 10)
                {
                    threats.Add("火灾风险较高");
                }

                // 检查电力安全
                var powerGens = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.TryGetComp<CompPowerPlant>() != null)
                    .Count();

                if (powerGens < 2)
                {
                    threats.Add("电力供应单点故障风险");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[SecurityAnalyzer] Failed to identify threats: {ex.Message}");
                threats.Add("威胁识别失败");
            }

            return threats;
        }

        private List<string> GenerateSecurityRecommendations(SecurityReport report)
        {
            var recommendations = new List<string>();

            if (report.DefenseLevel == DefenseLevel.Minimal || report.DefenseLevel == DefenseLevel.Basic)
            {
                recommendations.Add("优先加强基础防护设施");
                recommendations.Add("增加武器装备储备");
            }

            if (report.WallIntegrity.DamagedSections > 0)
            {
                recommendations.Add("修复受损墙体结构");
            }

            if (report.WeaponStatus.TotalWeapons < 10)
            {
                recommendations.Add("扩充武器库存");
            }

            if (report.SecurityThreats.Any(t => t.Contains("敌对")))
            {
                recommendations.Add("立即进入警戒状态");
            }

            return recommendations;
        }

        #endregion

        #region 辅助方法

        private SecurityReport CreateEmptyReport()
        {
            return new SecurityReport
            {
                DefenseLevel = DefenseLevel.Unknown,
                WeaponStatus = new WeaponStatusReport { Status = "无法获取" },
                WallIntegrity = new WallIntegrityReport { OverallIntegrity = "无法评估" },
                GuardPostsCoverage = new List<string> { "数据不可用" },
                TrapEffectiveness = "数据不可用",
                SecurityThreats = new List<string> { "威胁评估失败" },
                LastUpdated = DateTime.Now
            };
        }

        // 其他辅助方法的实现...
        private List<WallStructureInfo> AnalyzeWallStructures(Map map) => new List<WallStructureInfo>();
        private List<GateInfo> AnalyzeGates(Map map) => new List<GateInfo>();
        private List<TurretInfo> AnalyzeTurrets(Map map) => new List<TurretInfo>();
        private List<BarrierInfo> AnalyzeBarriers(Map map) => new List<BarrierInfo>();
        private List<string> IdentifyWeakPoints(Map map) => new List<string>();
        private List<string> FindCoverageGaps(Map map) => new List<string>();
        private string EvaluateCombatReadiness(Map map) => "待评估";
        private string EvaluateWeaponAvailability(Map map) => "待评估";
        private string EvaluateMedicalSupport(Map map) => "待评估";
        private string EvaluateCommunication(Map map) => "待评估";
        private int EstimateResponseTime(Map map) => 60;
        private string CalculateOverallRating(ThreatResponseCapability capability) => "B级";

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 安全报告
    /// </summary>
    public class SecurityReport
    {
        public DefenseLevel DefenseLevel { get; set; }
        public WeaponStatusReport WeaponStatus { get; set; } = new WeaponStatusReport();
        public WallIntegrityReport WallIntegrity { get; set; } = new WallIntegrityReport();
        public List<string> GuardPostsCoverage { get; set; } = new List<string>();
        public string TrapEffectiveness { get; set; }
        public List<string> SecurityThreats { get; set; } = new List<string>();
        public List<string> RecommendedActions { get; set; } = new List<string>();
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 防御等级
    /// </summary>
    public enum DefenseLevel
    {
        Unknown,
        Minimal,    // 最低防御
        Basic,      // 基础防御
        Adequate,   // 充足防御
        Strong,     // 强力防御
        Fortress    // 要塞级防御
    }

    /// <summary>
    /// 武器状态报告
    /// </summary>
    public class WeaponStatusReport
    {
        public int TotalWeapons { get; set; }
        public int MeleeWeapons { get; set; }
        public int RangedWeapons { get; set; }
        public Dictionary<QualityCategory, int> WeaponsByQuality { get; set; } = new Dictionary<QualityCategory, int>();
        public string Status { get; set; }
    }

    /// <summary>
    /// 墙体完整性报告
    /// </summary>
    public class WallIntegrityReport
    {
        public int TotalWallSections { get; set; }
        public int DamagedSections { get; set; }
        public int WeakMaterials { get; set; }
        public string OverallIntegrity { get; set; }
    }

    /// <summary>
    /// 防御工事报告
    /// </summary>
    public class DefenseStructureReport
    {
        public List<WallStructureInfo> Walls { get; set; } = new List<WallStructureInfo>();
        public List<GateInfo> Gates { get; set; } = new List<GateInfo>();
        public List<TurretInfo> Turrets { get; set; } = new List<TurretInfo>();
        public List<BarrierInfo> Barriers { get; set; } = new List<BarrierInfo>();
        public List<string> WeakPoints { get; set; } = new List<string>();
        public List<string> CoverageGaps { get; set; } = new List<string>();
    }

    /// <summary>
    /// 威胁响应能力
    /// </summary>
    public class ThreatResponseCapability
    {
        public string CombatReadiness { get; set; }
        public string WeaponAvailability { get; set; }
        public string MedicalSupport { get; set; }
        public string CommunicationSystems { get; set; }
        public int ResponseTime { get; set; } // 响应时间（秒）
        public string OverallRating { get; set; }
    }

    // 其他数据模型的占位符
    public class WallStructureInfo { }
    public class GateInfo { }
    public class TurretInfo { }
    public class BarrierInfo { }

    #endregion
}
