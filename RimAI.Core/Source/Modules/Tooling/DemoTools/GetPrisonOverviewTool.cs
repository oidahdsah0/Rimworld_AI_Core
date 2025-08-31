using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetPrisonOverviewTool : IRimAITool
    {
        public string Name => "get_prison_overview";
    public string Description => "Summarize prisoner overview: current inmates and status, recruitment mode, and risk cues for revolt or escape.";
        public string DisplayName => "囚犯概览";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
