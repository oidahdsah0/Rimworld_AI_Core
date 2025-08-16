namespace RimAI.Core.Source.Modules.Tooling
{
    // 保留文件以避免潜在引用，但不再使用该 DTO（统一用 IRimAITool.BuildToolJson() → Framework.ToolDefinition）
    internal sealed class ToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ParametersJson { get; set; }
        public string ToolJson { get; set; }
    }
}


