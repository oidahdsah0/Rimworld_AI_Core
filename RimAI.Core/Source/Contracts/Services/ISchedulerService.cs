using System;
using System.Threading.Tasks;

namespace RimAI.Core.Contracts.Services
{
    public interface ISchedulerService
    {
        void ScheduleOnMainThread(Action action);

        Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func);
    }
}