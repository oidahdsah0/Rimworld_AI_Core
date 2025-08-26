using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetRaidReadinessTool : IRimAITool
    {
        public string Name => "get_raid_readiness";
        public string Description => "根据财富/人口/战兽和故事讲述者因子估算当前威胁点与袭击规模、风险等级。";
        public string DisplayName => "袭击战备评估";
        public int Level => 2;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
