using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetMedicalOverviewTool : IRimAITool
    {
        public string Name => "get_medical_overview";
        public string Description => "Colony medical health check: summary, bleeding/infections/operations, and per-pawn health details.";
        public string DisplayName => "健康检查";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
