using System.Collections.Generic;
using System.Threading.Tasks;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 定义所有写入游戏世界的安全指令执行入口。
    /// </summary>
    public interface ICommandService
    {
        /// <summary>
        /// 执行一个高层指令。
        /// </summary>
        /// <param name="commandName">指令唯一名称，如 "spawn_item"。</param>
        /// <param name="parameters">指令参数字典。</param>
        Task<CommandResult> ExecuteCommandAsync(string commandName, Dictionary<string, object> parameters);
    }

    /// <summary>
    /// 指令执行结果封装。
    /// </summary>
    public class CommandResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
    }
}