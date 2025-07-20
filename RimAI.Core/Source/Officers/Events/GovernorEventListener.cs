using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Officers.Events
{
    /// <summary>
    /// Governor事件监听器 - 展示企业级事件处理的示例
    /// 这个类演示了如何通过EventBus进行解耦的事件处理
    /// </summary>
    public class GovernorEventListener : IEventHandler<GovernorAdviceEvent>
    {
        public async Task HandleAsync(IEvent eventData, CancellationToken cancellationToken = default)
        {
            if (eventData is GovernorAdviceEvent governorEvent)
            {
                await HandleAsync(governorEvent, cancellationToken);
            }
        }

        public async Task HandleAsync(GovernorAdviceEvent eventArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                // 🎯 企业级事件处理示例 
                Log.Message($"[GovernorEventListener] 接收到总督建议事件:");
                Log.Message($"  - 用户查询: {eventArgs.UserQuery}");
                Log.Message($"  - 建议成功: {eventArgs.WasSuccessful}");
                Log.Message($"  - 殖民地状态: {eventArgs.ColonyStatus}");
                Log.Message($"  - 时间戳: {eventArgs.Timestamp}");
                
                // 这里可以添加各种企业级逻辑：
                // - 记录到数据库
                // - 触发其他业务流程
                // - 发送通知
                // - 更新统计数据等

                if (eventArgs.WasSuccessful)
                {
                    Log.Message("[GovernorEventListener] ✅ 总督建议处理成功，事件已记录");
                }
                else
                {
                    // 🎯 修复：不要在失败事件中输出 Warning，这会导致递归错误
                    Log.Message("[GovernorEventListener] ℹ️ 总督建议处理失败（这是正常的错误恢复流程）");
                }

                await Task.CompletedTask; // 模拟异步处理
            }
            catch (OperationCanceledException)
            {
                // 🎯 修复：正确处理取消异常
                Log.Message("[GovernorEventListener] 事件处理被取消");
            }
            catch (System.Exception ex)
            {
                // 🎯 修复：更详细的错误信息，但不要再次触发事件
                Log.Message($"[GovernorEventListener] 事件处理异常: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
