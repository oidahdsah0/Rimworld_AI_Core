using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    // Level 2 utility tool: randomly adjust goodwill with a random eligible faction during server inspections
    internal sealed class AiDiplomatTool : IRimAITool, IResearchGatedTool
    {
        public string Name => "ai_diplomat";
        public string DisplayName => "AI外交官";
        public int Level => 2;
        public string Description => "During periodic server inspections, if an AI terminal is powered, randomly select an eligible faction and adjust goodwill by a small delta (-5..+15).";

        // Research gate: requires Level 2 AI research completed
        public System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames => new[] { "RimAI_AI_Level2" };

        public string ParametersJson => JsonConvert.SerializeObject(new
        {
            type = "object",
            properties = new
            {
                server_id = new { type = "string", description = "Target AI server entity id (Lv2 or above). Format: thing:<id>" },
                // optional future extension: map_id
                map_id = new { type = "integer", description = "Optional map thingIdNumber. Defaults to current map.", @default = (int?)null }
            },
            required = new[] { "server_id" }
        });

        public string BuildToolJson()
        {
            var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}


