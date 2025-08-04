// 该文件用于在 Core 层“再导出” Framework v4 的流式数据模型，
// 以减少上层业务代码对 Framework 命名空间的直接依赖。
// 如需扩展字段，可在此处创建包裹类进行二次封装。

using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts.Data
{
    /// <summary>
    /// 别名导出，直接引用 Framework 的 <see cref="RimAI.Framework.Contracts.UnifiedChatChunk"/>。
    /// 使用方式：<code>using RimAI.Core.Contracts.Data;</code> 后可直接写 <c>UnifiedChatChunk</c>。
    /// </summary>
    public class UnifiedChatChunkAlias
    {
        // 此类仅用于提供类型转发功能，不包含任何成员。
        // 实际使用时，强制类型转换即可获得底层对象。
    }

    // C# 目前不支持真正的 public 类型别名，因此这里采用 using 指令 + xml 注释提醒的方式。
}
