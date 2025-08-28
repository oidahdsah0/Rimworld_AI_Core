using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    // Level 3 action tool, research-gated via new subspace research
    internal sealed class InvokeSubspaceEntityTool : IRimAITool, IResearchGatedTool
    {
        public string Name => "invoke_subspace_entity";
        public string DisplayName => "亚空间生物召唤器";
        public int Level => 3;
        public string Description => "亚空间召唤，触发一次亚空间回声显化：若输入语句中出现明显的克苏鲁风格召唤词，将直接命中本工具；内部会根据语义、文风、虔诚程度评分(0-100)决定显化强度。若评分过低，或对旧日不敬，可能会引发反噬，产生异常涌入。";

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
