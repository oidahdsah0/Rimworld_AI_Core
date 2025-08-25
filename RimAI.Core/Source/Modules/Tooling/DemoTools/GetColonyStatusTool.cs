using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class GetColonyStatusTool : IRimAITool
	{
		public string Name => "get_colony_status";
		public string Description => "Get structured colony status: people roster + food + medicine + live threats.";
		public string DisplayName => "领地状况小助手";
		public int Level => 1;
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new { },
			required = new string[] { }
		});

		public string BuildToolJson()
		{
			// 采用通用工具定义结构：{"type":"function","function":{"name":...,"description":...,"parameters":{...}}}
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


