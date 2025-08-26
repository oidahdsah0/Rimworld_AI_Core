namespace RimAI.Core.Source.Modules.World
{
	internal sealed class AiServerSnapshot
	{
		public string ServerId { get; set; }
		public int TemperatureC { get; set; }
		public int LoadPercent { get; set; }
		public bool PowerOn { get; set; }
		public bool HasAlarm { get; set; }
	}

	internal sealed class PawnHealthSnapshot
	{
		public int PawnLoadId { get; set; }
		public float Consciousness { get; set; }     // 0..1
		public float Moving { get; set; }
		public float Manipulation { get; set; }
		public float Sight { get; set; }
		public float Hearing { get; set; }
		public float Talking { get; set; }
		public float Breathing { get; set; }
		public float BloodPumping { get; set; }
		public float BloodFiltration { get; set; }
		public float Metabolism { get; set; }
		public float AveragePercent { get; set; }    // 0..100
		public bool IsDead { get; set; }
		public System.Collections.Generic.IReadOnlyList<HediffItem> Hediffs { get; set; }
	}

	// P11 Prompting 快照（身份/特质/技能/信仰可用性）
	internal sealed class PawnPromptSnapshot
	{
		public Identity Id { get; set; }
		public Backstory Story { get; set; }
		public TraitsAndWork Traits { get; set; }
		public Skills Skills { get; set; }
		public bool IsIdeologyAvailable { get; set; }
	}

	internal sealed class Identity
	{
		public string Name { get; set; }
		public string Gender { get; set; }
		public int Age { get; set; }
		public string Race { get; set; }
		public string Belief { get; set; } // 仅在 DLC 可用时
	}

	internal sealed class Backstory
	{
		public string Childhood { get; set; }
		public string Adulthood { get; set; }
	}

	internal sealed class TraitsAndWork
	{
		public System.Collections.Generic.IReadOnlyList<string> Traits { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> WorkDisables { get; set; }
	}

	internal sealed class SkillItem
	{
		public string Name { get; set; }
		public int Level { get; set; }
		public string Passion { get; set; }
		public float Normalized { get; set; } // 0..1
	}

	internal sealed class Skills
	{
		public System.Collections.Generic.IReadOnlyList<SkillItem> Items { get; set; }
	}

	// P11 社交快照
	internal sealed class PawnSocialSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<SocialRelationItem> Relations { get; set; }
		public System.Collections.Generic.IReadOnlyList<SocialEventItem> RecentEvents { get; set; }
	}

	internal sealed class SocialRelationItem
	{
		public string RelationKind { get; set; }
		public string OtherName { get; set; }
		public string OtherEntityId { get; set; }
		public int Opinion { get; set; }
	}

	internal sealed class SocialEventItem
	{
		public System.DateTime TimestampUtc { get; set; }
		public string WithName { get; set; }
		public string WithEntityId { get; set; }
		public string InteractionKind { get; set; }
		public string Outcome { get; set; }
		public string GameTime { get; set; }
	}

	// P11+ 扩展：以小人为中心的环境矩阵快照（仅用于提示词上下文传输）
	internal sealed class EnvironmentMatrixSnapshot
	{
		public int PawnLoadId { get; set; }
		public int Radius { get; set; }                // 以格为单位，方阵边长 = 2*Radius+1
		public System.Collections.Generic.IReadOnlyList<string> Rows { get; set; } // 自上而下，每行固定长度
		public string Legend { get; set; }             // 符号说明（纯文本）
		public float BeautyAverage { get; set; }       // 区域平均美观度（可能为 0..?
		public System.Collections.Generic.IReadOnlyList<TerrainCountItem> TerrainCounts { get; set; }
	}

	internal sealed class TerrainCountItem
	{
		public string Terrain { get; set; }
		public int Count { get; set; }
	}

	internal sealed class ColonySnapshot
	{
		public string ColonyName { get; set; }
		public int ColonistCount { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> ColonistNames { get; set; }
		public System.Collections.Generic.IReadOnlyList<ColonistRecord> Colonists { get; set; }
	}

	internal sealed class ColonistRecord
	{
		public string Name { get; set; }
		public int Age { get; set; }
		public string Gender { get; set; }
		public string JobTitle { get; set; }
	}

	internal sealed class HediffItem
	{
		public string Label { get; set; }
		public string Part { get; set; }
		public float Severity { get; set; }
		public bool Permanent { get; set; }
		public string Category { get; set; } // Injury | Disease | Implant | MissingPart | Other
	}

	internal sealed class PawnUiStatusSnapshot
	{
		public string Weather { get; set; }
		public float OutdoorTempC { get; set; }
		public float Glow { get; set; }
		public string CurrentJob { get; set; }
		public System.Collections.Generic.IReadOnlyList<ApparelItem> Apparel { get; set; }
		public NeedsSnapshot Needs { get; set; }
		public System.Collections.Generic.IReadOnlyList<ThoughtItem> ThoughtMoodOffsets { get; set; }
	}

	internal sealed class ApparelItem
	{
		public string Label { get; set; }
		public string Quality { get; set; }
		public int DurabilityPercent { get; set; }
	}

	internal sealed class NeedsSnapshot
	{
		public float Food { get; set; }
		public float Rest { get; set; }
		public float Recreation { get; set; }
		public float Beauty { get; set; }
		public float Indoors { get; set; }
		public float Mood { get; set; }
	}

	internal sealed class ThoughtItem
	{
		public string Label { get; set; }
		public int MoodOffset { get; set; }
	}

	internal sealed class WeatherStatus
	{
		public string Weather { get; set; }
		public float OutdoorTempC { get; set; }
		public float Glow { get; set; }
	}

	// 气象分析（v1）：当前天气 + 气温短期趋势 + 风/降水 + 条件 + 建议
	internal sealed class WeatherAnalysisSnapshot
	{
		public TimeInfo Time { get; set; }
		public WeatherNow Weather { get; set; }
		public TemperatureTrend Temp { get; set; }
		public string[] ActiveConditions { get; set; }
		public bool GrowthSeasonNow { get; set; }
		public bool EnjoyableOutside { get; set; }
		public string[] Advisories { get; set; }
	}

	internal sealed class TimeInfo
	{
		public int HourOfDay { get; set; }
		public string Season { get; set; }
		public string Quadrum { get; set; }
		public int DayOfQuadrum { get; set; }
	}

	internal sealed class WeatherNow
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public float RainRate { get; set; } // 0..1
		public float SnowRate { get; set; } // 0..1
		public float WindSpeed { get; set; } // map.windManager.WindSpeed
	}

	internal sealed class TemperatureTrend
	{
		public float OutdoorNowC { get; set; }
		public float SeasonalNowC { get; set; }
		public System.Collections.Generic.IReadOnlyList<float> NextHoursC { get; set; } // 逐小时（默认 6 个点）
		public float MinNextC { get; set; }
		public float MaxNextC { get; set; }
		public string Trend { get; set; } // rising | falling | steady
	}

	// 游戏日志项（PlayLog 简化快照）
	internal sealed class GameLogItem
	{
		public string GameTime { get; set; }
		public string Text { get; set; }
	}

	// 电力状态
	internal sealed class BatteryStatus
	{
		public int Count { get; set; }
		public float StoredWd { get; set; }      // 电池当前存量（W-days）
		public float CapacityWd { get; set; }    // 电池总容量（W-days）
		public float DaysLeft { get; set; }      // 在净耗电为负时的可用天数；>=0 有效；<0 表示正在充电/不适用
	}

	internal sealed class PowerStatusSnapshot
	{
		public float GenW { get; set; }          // 总发电功率（W）
		public float ConsW { get; set; }         // 总用电功率（W）
		public float NetW { get; set; }          // 净功率（W）= GenW - ConsW
		public BatteryStatus Batteries { get; set; }
	}

	// 资源概览（通用 CountAsResource 项）
	internal sealed class ResourceItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Count { get; set; }
		public float DailyUse { get; set; } // 估算（基于人口/食物、医疗消耗等），可能为 0
		public float DaysLeft { get; set; } // Count / DailyUse；当 DailyUse=0 时记为 -1 表示不适用
	}

	internal sealed class ResourceOverviewSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<ResourceItem> Resources { get; set; }
	}

	// 物资：食品明细
	internal sealed class FoodItemInfo
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Count { get; set; }
		public float NutritionPer { get; set; }
		public float TotalNutrition { get; set; }
		public string Preferability { get; set; }
	}

	internal sealed class FoodInventorySnapshot
	{
		public float TotalNutrition { get; set; }
		public System.Collections.Generic.IReadOnlyList<FoodItemInfo> Items { get; set; }
	}

	// 物资：药品明细
	internal sealed class MedicineItemInfo
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Count { get; set; }
		public float Potency { get; set; }
	}

	internal sealed class MedicineInventorySnapshot
	{
		public int TotalCount { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicineItemInfo> Items { get; set; }
	}

	// 即时威胁信息
	internal sealed class ThreatSnapshot
	{
		public int HostilePawns { get; set; }
		public int Manhunters { get; set; }
		public int Mechanoids { get; set; }
		public float ThreatPoints { get; set; }
		public string DangerRating { get; set; }
		public float FireDanger { get; set; }
		public float LastBigThreatDaysAgo { get; set; }
	}

	// 仓储饱和度（v1）：各仓库/存放点的占用率
	internal sealed class StorageSaturationItem
	{
		public string Name { get; set; }
		public float UsedPct { get; set; } // 0..1
		public bool Critical { get; set; }
		public string Notes { get; set; }
	}

	internal sealed class StorageSaturationSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<StorageSaturationItem> Storages { get; set; }
	}

	// 研究选项（v1）：当前研究 + 可立即研究 + 关键受限项 + 殖民地研究能力
	internal sealed class ResearchOptionsSnapshot
	{
		public ResearchCurrentInfo Current { get; set; }
		public System.Collections.Generic.IReadOnlyList<ResearchOptionItem> AvailableNow { get; set; }
		public System.Collections.Generic.IReadOnlyList<ResearchLockedItem> LockedKey { get; set; }
		public ResearchColonyInfo Colony { get; set; }
	}

	internal sealed class ResearchCurrentInfo
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public float ProgressPct { get; set; } // 0..1
		public float WorkLeft { get; set; }    // 成本 - 已完成
		public float EtaDays { get; set; }     // 基于 colony.effectiveSpeed 的粗略估算；<0 表示未知
	}

	internal sealed class ResearchOptionItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public string Desc { get; set; }
		public float BaseCost { get; set; }
		public string TechLevel { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> Prereqs { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> Benches { get; set; }
		public int TechprintsNeeded { get; set; }
		public float RoughTimeDays { get; set; } // <0 表示无法估算
	}

	internal sealed class ResearchLockedItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> MissingPrereqs { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> BenchesMissing { get; set; }
		public int TechprintsMissing { get; set; }
		public string Note { get; set; }
	}

	internal sealed class ResearchColonyInfo
	{
		public int Researchers { get; set; }
		public float EffectiveSpeed { get; set; } // 研究点/天（粗估）
	}

	// 施工积压（v1）：蓝图/框架需要的材料缺口汇总
	internal sealed class ConstructionBacklogSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<ConstructionBuildItem> Builds { get; set; }
	}

	internal sealed class ConstructionBuildItem
	{
		public string Thing { get; set; }   // 目标对象（label）
		public string DefName { get; set; } // 目标 DefName（用于上游对齐，可为空）
		public int Count { get; set; }      // 实例数量
		public System.Collections.Generic.IReadOnlyList<ConstructionMissingItem> Missing { get; set; }
	}

	internal sealed class ConstructionMissingItem
	{
		public string Res { get; set; } // 资源 label/defName
		public int Qty { get; set; }
	}

	// 安防态势（v1）：炮塔/陷阱/覆盖与盲区 + 备注
	internal sealed class SecurityPostureSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<SecurityTurretItem> Turrets { get; set; }
		public System.Collections.Generic.IReadOnlyList<SecurityTrapItem> Traps { get; set; }
		public SecurityCoverageInfo Coverage { get; set; }
		public System.Collections.Generic.IReadOnlyList<SecurityGapItem> Gaps { get; set; }
		public string Note { get; set; }
	}

	internal sealed class SecurityTurretItem
	{
		public string Type { get; set; }
		public string Label { get; set; }
		public int X { get; set; }
		public int Z { get; set; }
		public float Range { get; set; }
		public float MinRange { get; set; }
		public bool LosRequired { get; set; }
		public bool FlyOverhead { get; set; }
		public float DpsScore { get; set; }
		public bool Powered { get; set; }
		public bool Manned { get; set; }
		public bool HoldFire { get; set; }
	}

	internal sealed class SecurityTrapItem
	{
		public string Type { get; set; }
		public string Label { get; set; }
		public int X { get; set; }
		public int Z { get; set; }
		public bool Resettable { get; set; }
	}

	internal sealed class SecurityCoverageInfo
	{
		public float AreaPct { get; set; }
		public float StrongPct { get; set; }
		public float AvgStack { get; set; }
		public float OverheadPct { get; set; }
		public System.Collections.Generic.IReadOnlyList<SecurityApproachItem> Approaches { get; set; }
	}

	internal sealed class SecurityApproachItem
	{
		public int EntryX { get; set; }
		public int EntryZ { get; set; }
		public float AvgFire { get; set; }
		public int MaxGapLen { get; set; }
		public float TrapDensity { get; set; }
	}

	internal sealed class SecurityGapItem
	{
		public int CenterX { get; set; }
		public int CenterZ { get; set; }
		public int MinX { get; set; }
		public int MinZ { get; set; }
		public int MaxX { get; set; }
		public int MaxZ { get; set; }
		public int Area { get; set; }
		public int DistToCore { get; set; }
		public string Reason { get; set; }
	}

	// 心情风险概览（v1）：分布与主因
	internal sealed class MoodRiskSnapshot
	{
		public float AvgPct { get; set; }           // 0..1
		public int MinorCount { get; set; }
		public int MajorCount { get; set; }
		public int ExtremeCount { get; set; }
		public int NearBreakCount { get; set; }     // 距轻度阈值近的数量
		public System.Collections.Generic.IReadOnlyList<MoodCauseItem> TopCauses { get; set; }
	}

	internal sealed class MoodCauseItem
	{
		public string Label { get; set; }
		public float TotalImpact { get; set; }      // 负面强度绝对值累加
		public int PawnsAffected { get; set; }
	}

	// 医疗健康检查（v1）：汇总 + 分组 + 全量明细（可裁剪）
	internal sealed class MedicalOverviewSnapshot
	{
		public MedicalSummary Summary { get; set; }
		public MedicalGroups Groups { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalPawnItem> Pawns { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> Advisories { get; set; }
	}

	internal sealed class MedicalSummary
	{
		public int TotalColonists { get; set; }
		public int PatientsNeedingTend { get; set; }
		public int BleedingCount { get; set; }
		public int InfectionCount { get; set; }
		public int OperationsPending { get; set; }
		public int LifeThreatCount { get; set; }
		public float AvgHealthPct { get; set; } // 0..1（SummaryHealthPercent）
		public float AvgPainPct { get; set; }   // 0..1（hediffSet.PainTotal）
		public int RiskScore { get; set; }      // 0..100 轻量评分
	}

	internal sealed class MedicalGroups
	{
		public System.Collections.Generic.IReadOnlyList<MedicalBleedingItem> Bleeding { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalInfectionItem> Infections { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalOperationItem> Operations { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalLifeThreatItem> LifeThreats { get; set; }
	}

	internal sealed class MedicalBleedingItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public float BleedRate { get; set; }     // 每日总流血率（pawn.health.hediffSet.BleedRateTotal）
		public float TimeToDeathHours { get; set; } // 依据 HealthUtility.TicksUntilDeathDueToBloodLoss 估算（<0 表示不适用）
		public bool Tended { get; set; }
		public bool InMedicalBed { get; set; }
	}

	internal sealed class MedicalInfectionItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public string Disease { get; set; }
		public float Severity { get; set; }
		public float Immunity { get; set; }
		public float Delta { get; set; }         // Immunity - Severity
		public bool Tended { get; set; }
	}

	internal sealed class MedicalOperationItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public string BillLabel { get; set; }
		public string PartLabel { get; set; }
		public bool RequiresMedicine { get; set; }
		public bool UsesGlitter { get; set; }
		public bool Anesthetize { get; set; }
		public string RiskHint { get; set; }
	}

	internal sealed class MedicalLifeThreatItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public string Hediff { get; set; }
		public float Severity { get; set; }
		public string Reason { get; set; }
	}

	internal sealed class MedicalPawnItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public string Map { get; set; }
		public bool IsDowned { get; set; }
		public bool InMedicalBed { get; set; }
		public bool NeedsTending { get; set; }
		public float HealthPct { get; set; }    // 0..1
		public float PainPct { get; set; }      // 0..1
		public float BleedRate { get; set; }
		public MedicalCapacities Capacities { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalPartItem> Parts { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalHediffItem> Hediffs { get; set; }
		public System.Collections.Generic.IReadOnlyList<MedicalOperationItem> ScheduledOps { get; set; }
	}

	internal sealed class MedicalCapacities
	{
		public float Consciousness { get; set; }
		public float Moving { get; set; }
		public float Manipulation { get; set; }
		public float Sight { get; set; }
		public float Hearing { get; set; }
		public float Talking { get; set; }
		public float Breathing { get; set; }
		public float BloodPumping { get; set; }
		public float BloodFiltration { get; set; }
	}

	internal sealed class MedicalPartItem
	{
		public string PartLabel { get; set; }
		public string Tag { get; set; }
		public bool IsMissing { get; set; }
		public float HpPct { get; set; }
		public string ProstheticTier { get; set; } // none|prosthetic|bionic|archotech|other
	}

	internal sealed class MedicalHediffItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public string Category { get; set; }    // injury|disease|implant|missing|addiction|temperature|other
		public string StageLabel { get; set; }
		public float Severity { get; set; }
		public bool IsBleeding { get; set; }
		public bool IsTendable { get; set; }
		public bool Tended { get; set; }
		public float TendQuality { get; set; }
		public int TendTicksLeft { get; set; }
		public bool IsInfection { get; set; }
		public float Immunity { get; set; }
		public bool IsPermanent { get; set; }
		public bool IsAddiction { get; set; }
		public bool IsDisease { get; set; }
		public bool IsTemperature { get; set; }
		public string OnPartLabel { get; set; }
	}

	// 野生动物机会（v1）：按物种聚合狩猎机会与风险/收益信号
	internal sealed class WildlifeOpportunitiesSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<WildlifeSpeciesGroup> Species { get; set; }
		public string Note { get; set; }
	}

	internal sealed class WildlifeSpeciesGroup
	{
		public string Species { get; set; }      // 物种中文标签
		public string DefName { get; set; }      // 物种 DefName
		public int Count { get; set; }           // 地图上存活数量
		public bool Predator { get; set; }
		public bool HerdAnimal { get; set; }
		public bool PackAnimal { get; set; }
		public bool IsInsect { get; set; }
		public bool Explosive { get; set; }      // 如爆炸羊/爆炸鼠
		public float ManhunterOnDamageChance { get; set; } // 单体受伤复仇几率（0..1）
		public float AvgBodySize { get; set; }
		public float AvgWildness { get; set; }   // 0..1
		public float MeatPer { get; set; }
		public float LeatherPer { get; set; }
		public string LeatherDef { get; set; }
		public float TotalMeat { get; set; }
		public float TotalLeather { get; set; }
		public bool SeasonOk { get; set; }
		public string SuggestedApproach { get; set; } // 简短建议：远程/避免近战/先清群等
		public string[] Notes { get; set; }
	}

	// 贸易就绪度（v1）：银币、信标/控制台状态、可交易物资清单（信标覆盖范围内）
	internal sealed class TradeReadinessSnapshot
	{
		public int Silver { get; set; }
		public TradeBeaconInfo Beacons { get; set; }
		public TradeCommsInfo Comms { get; set; }
		public System.Collections.Generic.IReadOnlyList<TradeGoodsItem> Goods { get; set; }
	}

	internal sealed class TradeBeaconInfo
	{
		public int Total { get; set; }
		public int Powered { get; set; }
		public int CoverageCells { get; set; }
		public int InRangeStacks { get; set; }
	}

	internal sealed class TradeCommsInfo
	{
		public bool HasConsole { get; set; }
		public bool UsableNow { get; set; }
	}

	internal sealed class TradeGoodsItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Qty { get; set; }
		public float TotalValue { get; set; }
	}

	// 牲畜管理（v1）：数量分布、训练概况、口粮压力
	internal sealed class AnimalManagementSnapshot
	{
		public AnimalCounts Counts { get; set; }
		public AnimalTrainingSummary Training { get; set; }
		public AnimalFoodStatus Food { get; set; }
	}

	internal sealed class AnimalCounts
	{
		public int Total { get; set; }
		public System.Collections.Generic.IReadOnlyList<SpeciesCountItem> Species { get; set; }
	}

	internal sealed class SpeciesCountItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Count { get; set; }
	}

	internal sealed class AnimalTrainingSummary
	{
		public TrainableStat Obedience { get; set; }
		public TrainableStat Release { get; set; }
		public TrainableStat Rescue { get; set; }
		public TrainableStat Haul { get; set; }
	}

	internal sealed class TrainableStat
	{
		public int Eligible { get; set; }
		public int Learned { get; set; }
	}

	internal sealed class AnimalFoodStatus
	{
		public float TotalNutrition { get; set; }
		public float DailyNeed { get; set; }
		public float Days { get; set; }
		public System.Collections.Generic.IReadOnlyList<FoodSourceItem> Sources { get; set; }
	}

	internal sealed class FoodSourceItem
	{
		public string DefName { get; set; }
		public string Label { get; set; }
		public int Count { get; set; }
		public float NutritionPer { get; set; }
		public float TotalNutrition { get; set; }
	}

	// 囚犯概览（v1）：数量、潜在可招募清单与风险提示
	internal sealed class PrisonOverviewSnapshot
	{
		public int Count { get; set; }
		public System.Collections.Generic.IReadOnlyList<PrisonerRecruitItem> Recruitables { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> Risks { get; set; }
	}

	internal sealed class PrisonerRecruitItem
	{
		public string Pawn { get; set; }
		public int PawnLoadId { get; set; }
		public string Mode { get; set; } // Recruit/ReduceResistance/Convert 等
	}

	// 警报摘要（v1）：聚合当前 RimWorld 警报并按严重度排序
	internal sealed class AlertDigestSnapshot
	{
		public System.Collections.Generic.IReadOnlyList<AlertItem> Alerts { get; set; }
	}

	internal sealed class AlertItem
	{
		public string Id { get; set; }      // 类型名或唯一键
		public string Label { get; set; }
		public string Severity { get; set; } // medium|high|critical
		public string Hint { get; set; }
	}

	// 袭击风险与战备评估（v1）：财富/人口/战兽与威胁点拆解 + 规模预估
	internal sealed class RaidReadinessSnapshot
	{
		public WealthInfo Wealth { get; set; }
		public ColonyCombatInfo Colony { get; set; }
		public AnimalCombatInfo Animals { get; set; }
		public MechCombatInfo Mechs { get; set; }
		public ThreatPointBreakdown Points { get; set; }
		public string RiskBand { get; set; } // low|medium|high|extreme
		public System.Collections.Generic.IReadOnlyList<RaidSizeEstimate> SizeEstimates { get; set; }
	}

	internal sealed class WealthInfo
	{
		public float Total { get; set; }
		public float Items { get; set; }
		public float Buildings { get; set; }
		public float Pawns { get; set; }
		public float PlayerWealthForStoryteller { get; set; }
	}

	internal sealed class ColonyCombatInfo
	{
		public int HumanCount { get; set; }
		public int ArmedCount { get; set; }
		public float AvgHealthPct { get; set; } // 0..1
	}

	internal sealed class AnimalCombatInfo
	{
		public int BattleReadyCount { get; set; }
		public float PointsContribution { get; set; } // 估算：Σ(0.08 * combatPower)
	}

	internal sealed class MechCombatInfo
	{
		public int Count { get; set; }
		public float CombatPowerSum { get; set; }
	}

	internal sealed class ThreatPointBreakdown
	{
		public float FinalPoints { get; set; }
		public float DifficultyScale { get; set; }
		public float AdaptationApplied { get; set; } // Mathf.Lerp(1, adaptation, adaptationEffectFactor)
		public float TimeFactor { get; set; } // 天数因子
		public float RandomFactorMin { get; set; }
		public float RandomFactorMax { get; set; }
		public int DaysSinceSettle { get; set; }
	}

	internal sealed class RaidSizeEstimate
	{
		public string Archetype { get; set; } // humanoids|mixed
		public int Min { get; set; }
		public int Max { get; set; }
	}
}


