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
}


