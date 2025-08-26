using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class GetPowerStatusTool : IRimAITool
	{
		public string Name => "get_power_status";
		public string Description => "Get colony power status: generation, consumption, net and battery reserve days.";
		public string DisplayName => "电力设施概览";
		public int Level => 1;
		public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

		public string BuildToolJson()
		{
			var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
			return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
		}
	}
}
