using System;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Tests.Fakes
{
    // 简易调度器：直接在调用线程执行 Action
    public class FakeSchedulerService : ISchedulerService
    {
        public void ScheduleOnMainThread(Action action) => action();

        public Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func) => Task.FromResult(func());
    }
}