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
	}
}


