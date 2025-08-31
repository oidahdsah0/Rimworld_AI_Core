using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    // Level 2 utility tool: randomly adjust goodwill with a random eligible faction during server inspections
    internal sealed class AiDiplomatTool : IRimAITool, IResearchGatedTool
    {
        public string Name => "ai_diplomat";
    public string DisplayName => "tool.display.ai_diplomat";
        public int Level => 2;
        public string Description => "During periodic server inspections, if an AI terminal is powered, randomly select an eligible faction and adjust goodwill by a small delta (-5..+15).";

        // Research gate: requires Level 2 AI research completed
        public System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames => new[] { "RimAI_AI_Level2" };

        public string ParametersJson => JsonConvert.SerializeObject(new
        {
            type = "object",
            properties = new
            {
                // 由上游注入的已判定服务器等级（pawn 视为 1，服务器取其等级）
                server_level = new { type = "integer", minimum = 1, maximum = 3, description = "Pre-resolved max tool level from caller (1..3)." }
            },
            required = new string[] { }
        });

        public string BuildToolJson()
        {
            var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}


