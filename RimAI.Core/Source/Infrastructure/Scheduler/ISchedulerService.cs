using System;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Infrastructure.Scheduler
{
	/// <summary>
	/// 内部主线程调度器接口。所有 Verse 访问必须通过本接口主线程化。
	/// </summary>
	internal interface ISchedulerService
	{
		bool IsMainThread { get; }

		// fire-and-forget（主线程执行）
		void ScheduleOnMainThread(Action action, string name = null, CancellationToken ct = default);

		// 带返回值的主线程执行
		Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func, string name = null, CancellationToken ct = default);

		// 异步工作包装到主线程（避免长任务，请保持短小）
		Task ScheduleOnMainThreadAsync(Func<Task> func, string name = null, CancellationToken ct = default);

		// 主线程延迟（按 Tick）
		Task DelayOnMainThreadAsync(int ticks, CancellationToken ct = default);

		// 周期任务（按游戏 Tick 周期），返回取消句柄
		IDisposable SchedulePeriodic(string name, int everyTicks, Func<CancellationToken, Task> work, CancellationToken ct = default);
	}
}


