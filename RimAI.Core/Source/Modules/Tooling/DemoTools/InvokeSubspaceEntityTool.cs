using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    // Level 3 action tool, research-gated via new subspace research
    internal sealed class InvokeSubspaceEntityTool : IRimAITool, IResearchGatedTool
    {
        public string Name => "invoke_subspace_entity";
    public string DisplayName => "tool.display.invoke_subspace_entity";
        public int Level => 3;
    public string Description => "Subspace invocation that manifests a one-time subspace echo. If the input line contains explicit eldritch/cosmic-horror summoning cues, this tool is directly invoked. Internally, it scores semantics, tone, and piety (0–100) to determine manifestation intensity. Low scores—or irreverence toward the Old Ones—may backfire, causing anomalous incursions.";

        // New research gate
        public System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames => new[] { "RimAI_Subspace_Gravitic_Penetration" };

        // Parameters schema: orchestrator usually passes llm_score; direct-hit path may omit and executor will compute from context in future, but keep required here
        public string ParametersJson => JsonConvert.SerializeObject(new
        {
            type = "object",
            properties = new
            {
                // 由上游注入的已判定服务器等级（pawn=1；server=其等级）
                server_level = new { type = "integer", minimum = 1, maximum = 3, description = "Pre-resolved max tool level from caller (1..3)." },
                llm_score = new { type = "integer", minimum = 0, maximum = 100, description = "由 LLM 打分的强度 (0-100)。" }
            },
            required = new[] { "llm_score" }
        });

        public string BuildToolJson()
        {
            var parameters = Newtonsoft.Json.JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
