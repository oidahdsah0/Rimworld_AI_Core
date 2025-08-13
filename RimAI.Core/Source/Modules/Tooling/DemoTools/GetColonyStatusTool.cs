using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class GetColonyStatusTool : IRimAITool
	{
		public string Name => "get_colony_status";
		public string Description => "Get an overview of the player's colony (name, population, wealth).";
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new { },
			required = new string[] { }
		});

		public string BuildToolJson()
		{
			var json = JsonConvert.SerializeObject(new
			{
				Function = new
				{
					Name = Name,
					Description = Description,
					ParametersJsonSchema = ParametersJson
				}
			});
			return json;
		}
	}
}


