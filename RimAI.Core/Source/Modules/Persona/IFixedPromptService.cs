using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 固定提示词服务（M3 内存 MVP）。按参与者 ID 存储/读取固定提示词文本。
    /// </summary>
    internal interface IFixedPromptService
    {
        // 基于 convKey 的访问（推荐）
        string Get(string convKey, string participantId);
        void Upsert(string convKey, string participantId, string text);
        bool Delete(string convKey, string participantId);
        IReadOnlyDictionary<string, string> GetAll(string convKey);

        // 兼容旧签名（全局作用域，不建议使用）
        string Get(string participantId);
        void Upsert(string participantId, string text);
        bool Delete(string participantId);
        IReadOnlyDictionary<string, string> GetAll();

        // 快照（持久化）
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ExportSnapshot();
        void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> snapshot);
    }
}


