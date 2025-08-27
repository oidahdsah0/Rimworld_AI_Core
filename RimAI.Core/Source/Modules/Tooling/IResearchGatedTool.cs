namespace RimAI.Core.Source.Modules.Tooling
{
    // 可选接口：工具可声明需要完成的研究项目（全部满足才可对 LLM 暴露）
    internal interface IResearchGatedTool
    {
        System.Collections.Generic.IReadOnlyList<string> RequiredResearchDefNames { get; }
    }
}
