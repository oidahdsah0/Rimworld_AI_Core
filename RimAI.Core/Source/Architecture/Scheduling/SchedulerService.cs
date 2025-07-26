using System;
using System.Collections.Concurrent;    // 引入线程安全集合
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using Verse;    // 引入Verse，以继承GameComponent

namespace RimAI.Core.Architecture.Scheduling
{
    // 继承自Verse的GameComponent，以实现调度功能
    public class SchedulerService : GameComponent, ISchedulerService
    {
        // 使用线程安全队列，存储需要执行的操作
        private static readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        // GameComponent的构造函数，RimWorld会在游戏加载时调用
        public SchedulerService(Game game) { }

        // 核心：RimWorld每一帧都会调用该方法！
        public override void GameComponentUpdate()
        {
            // 从队列中取出一个动作并执行，直到队列为空
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        // --- ISchedulerService 接口实现 ---
        public void ScheduleOnMainThread(Action action)
        {
            _mainThreadActions.Enqueue(action);
        }

        public Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func)
        {
            // TaskCompletionSource 是连接回调和Task的桥梁
            var tcs = new TaskCompletionSource<T>();

            // 我们要调度到主线程的动作是..
            Action action = () =>
            {
                try
                {
                    // 在主线程上执行函数
                    var result = func();
                    // 把结果设置给 TaskCompletionSource，使Task完成
                    tcs.SetResult(result);
                }
                catch (Exception e)
                {
                    // 如果发生异常，设置Task为失败状态
                    tcs.SetException(e);
                }
            };

            // 把Action放入队列
            ScheduleOnMainThread(action);

            return tcs.Task;
        }
    }
}