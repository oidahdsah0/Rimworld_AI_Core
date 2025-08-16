using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class GetPawnHealthTool : IRimAITool
	{
		public string Name => "get_pawn_health";
		public string Description => "Get a specific pawn's health snapshot (10 capacities average and death state).";
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new
			{
				pawn_id = new { type = "integer", description = "Pawn loadId (thingIDNumber)" }
			},
			required = new[] { "pawn_id" }
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


