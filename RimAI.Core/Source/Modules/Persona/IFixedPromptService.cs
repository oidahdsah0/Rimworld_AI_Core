using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 固定提示词服务（V2）。
    /// 主存以 pawnId → text 存储，支持 convKey 覆盖层（特定会话上下文）。
    /// </summary>
    internal interface IFixedPromptService
    {
        // 主存（按 PawnId）
        string GetByPawn(string pawnId);
        void UpsertByPawn(string pawnId, string text);
        bool DeleteByPawn(string pawnId);
        IReadOnlyDictionary<string, string> GetAllByPawn();

        // 覆盖层（按 convKey；覆盖优先）
        string GetConvKeyOverride(string convKey);
        void UpsertConvKeyOverride(string convKey, string text);
        bool DeleteConvKeyOverride(string convKey);
        IReadOnlyDictionary<string, string> GetAllConvKeyOverrides();

        // 快照（持久化，仅主存）
        IReadOnlyDictionary<string, string> ExportSnapshot();
        void ImportSnapshot(IReadOnlyDictionary<string, string> snapshot);
    }
}


