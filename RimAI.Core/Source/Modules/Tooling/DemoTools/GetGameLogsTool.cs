using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class GetGameLogsTool : IRimAITool
	{
		public string Name => "get_game_logs";
		public string Description => "Get the latest N in-game log entries as plain text with game time.";
		public string DisplayName => "游戏日志浏览器";
		public int Level => 2;
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new { count = new { type = "integer", minimum = 1, maximum = 200, description = "number of latest log entries" } },
			required = new[] { "count" }
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


