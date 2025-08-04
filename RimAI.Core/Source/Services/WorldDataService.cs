using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// IWorldDataService 的默认实现。
    /// 通过 ISchedulerService 保证所有 Verse API 调用发生在主线程。
    /// </summary>
    public class WorldDataService : IWorldDataService
    {
        private readonly ISchedulerService _scheduler;

        public WorldDataService(ISchedulerService scheduler)
        {
            _scheduler = scheduler;
        }

        /// <inheritdoc />
        public Task<int> GetCurrentGameTickAsync()
        {
            return _scheduler.ScheduleOnMainThreadAsync(() => Find.TickManager.TicksGame);
        }
    }
}