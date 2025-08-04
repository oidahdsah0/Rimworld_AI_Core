using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// ICommandService 的默认实现。
    /// 所有实际写操作都被调度到主线程执行，确保线程安全。
    /// </summary>
    public class CommandService : ICommandService
    {
        private readonly ISchedulerService _scheduler;

        public CommandService(ISchedulerService scheduler)
        {
            _scheduler = scheduler;
        }

        /// <inheritdoc />
        public Task<CommandResult> ExecuteCommandAsync(string commandName, Dictionary<string, object> parameters)
        {
            // 通过调度器在主线程执行，返回 Task<CommandResult>
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    // TODO: 未来根据 commandName 路由到具体实现
                    Log.Message($"[RimAI.CommandService] Executing command '{commandName}'.");

                    // 临时示例：仅记录日志
                    return new CommandResult
                    {
                        IsSuccess = true,
                        Message = $"Command '{commandName}' executed (stub)."
                    };
                }
                catch (Exception ex)
                {
                    Log.Error($"[RimAI.CommandService] Command '{commandName}' failed: {ex.Message}");
                    return new CommandResult
                    {
                        IsSuccess = false,
                        Message = ex.Message
                    };
                }
            });
        }
    }
}