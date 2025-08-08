using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RimAI.Core.Infrastructure
{
    /// <summary>
    /// 主线程调度服务 (P3)。
    /// 负责将后台线程提交的操作安全地调度到 RimWorld 主线程执行。
    /// <para>实现基于 <see cref="ConcurrentQueue{T}"/> 与在主线程 <c>GameComponent.Update</c> 中的泵出。</para>
    /// </summary>
    public interface ISchedulerService
    {
        /// <summary>
        /// 将一个无返回值的操作安排到主线程执行。
        /// </summary>
        /// <param name="action">要执行的操作。</param>
        /// <returns>可等待任务，用于获取异常或等待完成。</returns>
        Task ScheduleOnMainThreadAsync(Action action);

        /// <summary>
        /// 将一个带返回值的函数安排到主线程执行，并返回执行结果。
        /// </summary>
        /// <typeparam name="T">返回值类型。</typeparam>
        /// <param name="func">要执行的函数。</param>
        /// <returns>包含执行结果的任务。</returns>
        Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func);
    }

    /// <summary>
    /// <see cref="ISchedulerService"/> 的默认实现。
    /// </summary>
    internal sealed class SchedulerService : ISchedulerService
    {
        private readonly ConcurrentQueue<Action> _queue = new();

        /// <inheritdoc />
        public Task ScheduleOnMainThreadAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null!);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <inheritdoc />
        public Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Enqueue(() =>
            {
                try
                {
                    var result = func();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// 由主线程 <c>GameComponent.Update</c> 调用，泵出并执行所有排队任务。
        /// </summary>
        internal void Pump()
        {
            while (_queue.TryDequeue(out var work))
            {
                work();
            }
        }
    }
}