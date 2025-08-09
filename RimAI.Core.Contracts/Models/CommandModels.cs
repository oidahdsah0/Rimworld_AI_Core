namespace RimAI.Core.Contracts.Models
{
    /// <summary>
    /// 通用命令结果（对外 DTO）。
    /// </summary>
    public sealed class CommandResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        /// <summary>
        /// 可选的序列化数据（JSON 文本）。避免对外暴露内部类型。
        /// </summary>
        public string DataJson { get; set; } = string.Empty;
    }
}


