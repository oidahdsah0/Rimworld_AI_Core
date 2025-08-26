using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetTradeReadinessTool : IRimAITool
    {
        public string Name => "get_trade_readiness";
        public string DisplayName => "贸易就绪度";
        public int Level => 1;
        public string Description => "统计可交易银币、信标与通讯台状态，以及信标覆盖范围内的可交易物资清单。";
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
