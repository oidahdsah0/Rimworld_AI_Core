using System.Threading.Tasks;
using RimAI.Core.Infrastructure;
using Verse;
using RimWorld;

namespace RimAI.Core.Modules.World
{
    /// <summary>
    /// RimWorld 数据访问防腐层的初版实现（P3）。
    /// 当前仅暴露 <see cref="GetPlayerNameAsync"/> 一个示例方法。
    /// 通过 <see cref="ISchedulerService"/> 保证所有 RimWorld API 调用在主线程执行，
    /// 避免跨线程崩溃。
    /// </summary>
    public interface IWorldDataService
    {
        /// <summary>
        /// 异步获取玩家派系名称。
        /// </summary>
        Task<string> GetPlayerNameAsync();
    }

    internal sealed class WorldDataService : IWorldDataService
    {
        private readonly ISchedulerService _scheduler;

        public WorldDataService(ISchedulerService scheduler)
        {
            _scheduler = scheduler;
        }

        /// <inheritdoc />
        public Task<string> GetPlayerNameAsync()
        {
            // 使用调度器在主线程执行 RimWorld API 调用。
            return _scheduler.ScheduleOnMainThreadAsync(() => Faction.OfPlayer?.Name ?? string.Empty);
        }
    }
}