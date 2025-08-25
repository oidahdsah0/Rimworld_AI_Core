using Newtonsoft.Json;
using RimAI.Core.Source.Modules.World;
using System;
using System.Globalization;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class BeautyAverageTool : IRimAITool
	{
		public string Name => "get_beauty_average";
		public string Description => "Compute the average beauty value in a square area of radius N centered at (x,z).";
		public string DisplayName => "美观平均";
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


