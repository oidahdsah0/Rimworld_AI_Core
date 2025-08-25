using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetWeatherStatusTool : IRimAITool
    {
        public string Name => "get_weather_status";
        public string Description => "Get simple meteorological analysis: time, weather, temperature trend, wind/precipitation, conditions and advisories.";
        public string DisplayName => "气象分析";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
