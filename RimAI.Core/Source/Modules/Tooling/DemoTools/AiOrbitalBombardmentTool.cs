using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	// Level 2 action tool: spoof "orbital" bombardment via developer explosions near hostiles
	internal sealed class AiOrbitalBombardmentTool : IRimAITool, IResearchGatedTool
	{
		public string Name => "ai_orbital_bombardment";
		public string DisplayName => "旧卫星火炮破解";
		public int Level => 2;
		public string Description => "Spoof old orbital satellite: perform 5-15 random explosions near hostile pawns. Requires powered AI terminal and cooldown.";

		public System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames => new[] { "RimAI_GW_Communication" }; // 可改为专用研究键

		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new
			{
				server_level = new { type = "integer", minimum = 1, maximum = 3, description = "Pre-resolved server level from caller (1..3)." },
				radius = new { type = "integer", minimum = 3, maximum = 30, @default = 9, description = "Random offset radius around hostile cluster center." },
				max_strikes = new { type = "integer", minimum = 5, maximum = 15, @default = 9, description = "Number of explosions (5-15)." }
			},
			required = new string[] { }
		});

		public string BuildToolJson()
		{
			var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(ParametersJson);
			return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
		}
	}
}


