namespace RimAI.Core.Contracts.Config
{
    /// <summary>
    /// 工具调用模式：Classic 返回全量工具；TopK 返回按向量检索的窄子集。
    /// 默认 Classic；当 Embedding 关闭或不可用时，TopK 不可选。
    /// </summary>
    public enum ToolCallMode
    {
        Classic = 0,
        TopK = 1
    }
}


