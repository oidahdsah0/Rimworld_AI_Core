using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Tooling.Execution;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    // Level 3 action tool, research-gated via Communication
    internal sealed class SetForcedWeatherTool : IRimAITool, IResearchGatedTool
    {
        public string Name => "set_forced_weather";
        public string Description => "Force a specific weather on the current map for a short duration (1-3 in-game days).";
        public string DisplayName => "强制天气";
        public int Level => 3;

        // Minimal research gating; runtime will self-check antenna power, cooldown, biome compatibility
        public System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames => new[] { "RimAI_GW_Communication" };

        // Parameters schema with a fixed enum list for canonical weather names
        // Centralized here to guide the LLM while executor still does fuzzy matching.
        public string ParametersJson => JsonConvert.SerializeObject(new
        {
            type = "object",
            properties = new
            {
                server_id = new { type = "string", description = "Target AI server entity id (Lv3)." },
                weather_name = new
                {
                    type = "string",
                    description = "Canonical weather name to force.",
                    enum_ = WeatherControlConfig.AllowedWeathers // replaced below
                },
                // optional future extension: map_id
                map_id = new { type = "integer", description = "Optional map thingIdNumber. Defaults to current map.", @default = (int?)null }
            },
            required = new[] { "server_id", "weather_name" }
        }).Replace("\"enum_\"", "\"enum\"");

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
