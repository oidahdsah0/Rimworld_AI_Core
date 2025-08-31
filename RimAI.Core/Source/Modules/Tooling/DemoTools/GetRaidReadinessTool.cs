using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetRaidReadinessTool : IRimAITool
    {
        public string Name => "get_raid_readiness";
    public string Description => "Estimate current threat points, likely raid size, and risk tier based on wealth, population, combat animals, and storyteller factors.";
    public string DisplayName => "tool.display.get_raid_readiness";
        public int Level => 2;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
