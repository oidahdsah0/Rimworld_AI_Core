using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class TerrainGroupCountTool : IRimAITool
	{
		public string Name => "get_terrain_group_counts";
		public string Description => "Count terrain groups within radius N centered at (x,z), grouped by terrain label.";
		public string DisplayName => "tool.display.get_terrain_group_counts";
		public int Level => 4;
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new
			{
				x = new { type = "integer", description = "Center cell X" },
				z = new { type = "integer", description = "Center cell Z" },
				radius = new { type = "integer", minimum = 0, description = "Radius in cells" }
			},
			required = new[] { "x", "z", "radius" }
		});

		public string BuildToolJson()
		{
			var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
			var json = JsonConvert.SerializeObject(new
			{
				type = "function",
				function = new
				{
					name = Name,
					description = Description,
					parameters = parameters
				}
			});
			return json;
		}
	}
}


