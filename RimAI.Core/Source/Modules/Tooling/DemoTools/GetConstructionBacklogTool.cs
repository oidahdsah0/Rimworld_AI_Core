using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetConstructionBacklogTool : IRimAITool
    {
        public string Name => "get_construction_backlog";
        public string Description => "Summarize blueprint/frame backlog and estimated material gaps on current map.";
        public string DisplayName => "施工积压";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
