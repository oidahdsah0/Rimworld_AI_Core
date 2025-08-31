using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetTradeReadinessTool : IRimAITool
    {
        public string Name => "get_trade_readiness";
        public string DisplayName => "贸易就绪度";
        public int Level => 1;
    public string Description => "Summarize trade readiness: available silver, beacon and comms console status, and a manifest of tradables within beacon coverage.";
        public string ParametersJson => "{ }";

        public string BuildToolJson()
        {
            var dict = new Dictionary<string, object>
            {
                ["Name"] = Name,
                ["DisplayName"] = DisplayName,
                ["Level"] = Level,
                ["Description"] = Description,
                ["ParametersSchema"] = new { }
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
        }
    }
}
