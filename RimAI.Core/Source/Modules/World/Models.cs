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
}


