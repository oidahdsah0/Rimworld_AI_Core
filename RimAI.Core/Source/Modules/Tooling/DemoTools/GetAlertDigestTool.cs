using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetAlertDigestTool : IRimAITool
    {
        public string Name => "get_alert_digest";
    public string Description => "Aggregate current RimWorld alerts, sort them by severity, and return a concise summary.";
        public string DisplayName => "警报摘要";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
