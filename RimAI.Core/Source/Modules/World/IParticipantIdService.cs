namespace RimAI.Core.Modules.World
{
    /// <summary>
    /// 参与者稳定 ID 与显示名解析服务（内部接口）。
    /// 负责将 Verse/RimWorld 对象映射为稳定 ID，并提供显示名回退。
    /// </summary>
    internal interface IParticipantIdService
    {
        /// <summary>
        /// 从 Verse/RimWorld 对象生成稳定 ID（如 pawn:<loadId>）。
        /// </summary>
        string FromVerseObject(object verseObj);

        /// <summary>
        /// 获取当前玩家的稳定 ID（M1 占位实现可返回 "__PLAYER__"）。
        /// </summary>
        string GetPlayerId();

        /// <summary>
        /// 基于 Persona 名称与版本生成稳定 ID（persona:<name>#<rev>）。
        /// </summary>
        string ForPersona(string name, int rev);

        /// <summary>
        /// 解析显示名；若无法解析则回退为原始 ID。
        /// </summary>
        string GetDisplayName(string id);

        /// <summary>
        /// 导出当前玩家 ID 以便持久化（格式：player:&lt;saveInstanceId&gt;）。
        /// </summary>
        string ExportPlayerId();

        /// <summary>
        /// 从持久化层导入玩家 ID；若为空，将在首次访问时延迟生成。
        /// </summary>
        void ImportPlayerId(string playerId);
    }
}


