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
        public async Task HandleAsync(GovernorAdviceEvent eventArgs, CancellationToken cancellationToken = default)
        {
            try
            {
                // 🎯 企业级事件处理示例 
                Log.Message($"[GovernorEventListener] 接收到总督建议事件:");
                Log.Message($"  - 用户查询: {eventArgs.UserQuery}");
                Log.Message($"  - 建议成功: {eventArgs.IsSuccessful}");
                Log.Message($"  - 殖民地状态: {eventArgs.ColonyStatus}");
                Log.Message($"  - 时间戳: {eventArgs.Timestamp}");
                
                // 这里可以添加各种企业级逻辑：
                // - 记录到数据库
                // - 触发其他业务流程
                // - 发送通知
                // - 更新统计数据等

                if (eventArgs.IsSuccessful)
                {
                    Log.Message("[GovernorEventListener] ✅ 总督建议处理成功，事件已记录");
                }
                else
                {
                    Log.Warning("[GovernorEventListener] ⚠️ 总督建议处理失败，需要关注");
                }

                await Task.CompletedTask; // 模拟异步处理
            }
            catch (System.Exception ex)
            {
                Log.Error($"[GovernorEventListener] 事件处理失败: {ex.Message}");
            }
        }
    }
}
