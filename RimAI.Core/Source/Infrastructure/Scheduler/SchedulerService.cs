using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;

namespace RimAI.Core.Source.Infrastructure.Scheduler
{
	internal sealed class SchedulerService : ISchedulerService
	{
		private abstract class WorkItem
		{
			public string Name;
			public CancellationToken Ct;
			// Optional: where this item was scheduled from (simplified callsite summary)
			public string Origin;
			public abstract void Execute();
		}

		private sealed class ActionWorkItem : WorkItem
		{
			private readonly Action _action;
			public ActionWorkItem(string name, Action action, CancellationToken ct)
			{
				Name = name ?? "Action";
				_action = action;
				Ct = ct;
			}
			public override void Execute()
			{
				if (Ct.IsCancellationRequested) return;
				_action();
			}
		}

		private sealed class FuncWorkItem<T> : WorkItem
		{
			private readonly Func<T> _func;
			private readonly TaskCompletionSource<T> _tcs;
			public FuncWorkItem(string name, Func<T> func, TaskCompletionSource<T> tcs, CancellationToken ct)
			{
				Name = name ?? ($"Func<{typeof(T).Name}>");
				_func = func;
				_tcs = tcs;
				Ct = ct;
			}
			public override void Execute()
			{
				if (Ct.IsCancellationRequested) { _tcs.TrySetCanceled(Ct); return; }
				try { _tcs.TrySetResult(_func()); }
				catch (Exception ex) { _tcs.TrySetException(ex); }
			}
		}

		private sealed class FuncTaskWorkItem : WorkItem
		{
			private readonly Func<Task> _func;
			private readonly TaskCompletionSource<bool> _tcs;
			public FuncTaskWorkItem(string name, Func<Task> func, TaskCompletionSource<bool> tcs, CancellationToken ct)
			{
				Name = name ?? "FuncTask";
				_func = func;
				_tcs = tcs;
				Ct = ct;
			}
			public override void Execute()
			{
				if (Ct.IsCancellationRequested) { _tcs.TrySetCanceled(Ct); return; }
				ExecuteAsync().ConfigureAwait(false);
			}
			private async Task ExecuteAsync()
			{
				try { await _func().ConfigureAwait(false); _tcs.TrySetResult(true); }
				catch (OperationCanceledException oce) { _tcs.TrySetCanceled(oce.CancellationToken); }
				catch (Exception ex) { _tcs.TrySetException(ex); }
			}
		}

		private sealed class DelayItem
		{
			public int TargetTick;
			public int RelativeTicks;
			public bool Initialized;
			public TaskCompletionSource<bool> Tcs;
			public CancellationToken Ct;
		}

		private sealed class PeriodicTaskItem : IDisposable
		{
			public string Name;
			public int EveryTicks;
			public int LastRunTick;
			public Func<CancellationToken, Task> Work;
			public CancellationTokenSource LinkedCts;
			public int InitialDelayTicks;
			public int FirstRunAtTick;
			public bool FirstInitialized;
			public void Dispose()
			{
				try { LinkedCts?.Cancel(); } catch { }
				try { LinkedCts?.Dispose(); } catch { }
			}
		}

		private readonly ConcurrentQueue<WorkItem> _queue = new();
		private readonly List<DelayItem> _delays = new();
		private readonly List<PeriodicTaskItem> _periodics = new();
		private readonly object _periodicLock = new();
		private readonly object _delayLock = new();
		private int _mainThreadId = -1;
		private readonly ConfigurationService _configService;

		// metrics
		private int _lastFrameProcessed;
		private double _lastFrameMs;
		private int _longTaskCount;
		private long _totalProcessed;
		// simple rate-limit for long task warnings: task name -> last warn tick
		private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _lastWarnTickByTask = new();

		public SchedulerService(IConfigurationService cfg)
		{
			// 仅内部使用：获取内部配置访问器
			_configService = cfg as ConfigurationService ?? throw new InvalidOperationException("Scheduler requires ConfigurationService");
		}

		public bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

		internal void BindMainThread(int mainThreadId)
		{
			_mainThreadId = mainThreadId;
		}

		public void ScheduleOnMainThread(Action action, string name = null, CancellationToken ct = default)
		{
			if (ct.IsCancellationRequested) return;
			var wi = new ActionWorkItem(name, action, ct);
			wi.Origin = _maybeCaptureOrigin(name);
			_queue.Enqueue(wi);
		}

		public Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func, string name = null, CancellationToken ct = default)
		{
			var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return tcs.Task; }
			var wi = new FuncWorkItem<T>(name, func, tcs, ct);
			// Capture origin for targeted diagnostics
			wi.Origin = _maybeCaptureOrigin(name);
			_queue.Enqueue(wi);
			return tcs.Task;
		}

		public Task ScheduleOnMainThreadAsync(Func<Task> func, string name = null, CancellationToken ct = default)
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return tcs.Task; }
			var wi = new FuncTaskWorkItem(name, func, tcs, ct) { Origin = _maybeCaptureOrigin(name) };
			_queue.Enqueue(wi);
			return tcs.Task;
		}

		public Task DelayOnMainThreadAsync(int ticks, CancellationToken ct = default)
		{
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			if (ct.IsCancellationRequested) { tcs.TrySetCanceled(ct); return tcs.Task; }
			lock (_delayLock)
			{
				_delays.Add(new DelayItem { RelativeTicks = ticks, Initialized = false, TargetTick = 0, Tcs = tcs, Ct = ct });
			}
			return tcs.Task;
		}

		public IDisposable SchedulePeriodic(string name, int everyTicks, Func<CancellationToken, Task> work, CancellationToken ct = default, int initialDelayTicks = 0)
		{
			var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var p = new PeriodicTaskItem { Name = name, EveryTicks = everyTicks, LastRunTick = 0, Work = work, LinkedCts = linked, InitialDelayTicks = Math.Max(0, initialDelayTicks), FirstRunAtTick = 0, FirstInitialized = false };
			lock (_periodicLock) { _periodics.Add(p); }
			return p;
		}

		internal void ProcessFrame(int currentTick, Action<string> logInfo, Action<string> logWarn, Action<string> logError)
		{
			var s = _configService; // internal access
			var sc = s.GetSchedulerConfig();
			int maxTasks = sc.MaxTasksPerUpdate;
			double budgetMs = sc.MaxBudgetMsPerUpdate;
			int longWarnMs = sc.LongTaskWarnMs;
			int maxQueueLen = sc.MaxQueueLength;
			bool warnOnBudgetExceeded = false; // suppress noisy frame budget warnings by default

			// process delay completions
			lock (_delayLock)
			{
				for (int i = _delays.Count - 1; i >= 0; i--)
				{
					var d = _delays[i];
					if (!d.Initialized)
					{
						d.TargetTick = currentTick + Math.Max(0, d.RelativeTicks);
						d.Initialized = true;
					}
					if (d.Ct.IsCancellationRequested) { d.Tcs.TrySetCanceled(d.Ct); _delays.RemoveAt(i); continue; }
					if (currentTick >= d.TargetTick) { d.Tcs.TrySetResult(true); _delays.RemoveAt(i); }
				}
			}

			// schedule periodic tasks
			lock (_periodicLock)
			{
				for (int i = 0; i < _periodics.Count; i++)
				{
					var p = _periodics[i];
					if (p.LinkedCts.IsCancellationRequested) continue;
					// 首次：若设置了 InitialDelayTicks，则在注册时记录 FirstRunAtTick = currentTick + InitialDelayTicks
					if (p.LastRunTick == 0)
					{
						if (!p.FirstInitialized)
						{
							p.FirstRunAtTick = currentTick + Math.Max(0, p.InitialDelayTicks);
							p.FirstInitialized = true;
						}
						if (currentTick >= p.FirstRunAtTick)
						{
							p.LastRunTick = currentTick;
							var localP = p; // capture
							_queue.Enqueue(new FuncTaskWorkItem(p.Name ?? "Periodic", () => localP.Work(localP.LinkedCts.Token), new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), localP.LinkedCts.Token));

						}
					}
					else if (currentTick - p.LastRunTick >= p.EveryTicks)
					{
						p.LastRunTick = currentTick;
						var localP = p; // capture
						_queue.Enqueue(new FuncTaskWorkItem(p.Name ?? "Periodic", () => localP.Work(localP.LinkedCts.Token), new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), localP.LinkedCts.Token));

					}
				}
			}

			var swFrame = Stopwatch.StartNew();
			int processed = 0;
			int queueLen = _queue.Count;
			int queueLenStart = queueLen;
			string lastItemName = null;
			long lastItemMs = 0;
			if (queueLen > maxQueueLen)
			{
				logWarn?.Invoke($"[RimAI.Core][P3][Scheduler] Queue length {queueLen} > MaxQueueLength={maxQueueLen}");
			}

			while (processed < maxTasks && _queue.TryDequeue(out var item))
			{
				try
				{
					var swTask = Stopwatch.StartNew();
					item.Execute();
					swTask.Stop();
					lastItemName = item?.Name;
					lastItemMs = swTask.ElapsedMilliseconds;
					if (swTask.ElapsedMilliseconds > longWarnMs)
					{
						// rate-limit: only warn again for the same task name if >= 250 ticks passed
						int lastWarnTick = 0; _lastWarnTickByTask.TryGetValue(item?.Name ?? "(unknown)", out lastWarnTick);
						if (currentTick - lastWarnTick >= 250)
						{
							_lastWarnTickByTask[item?.Name ?? "(unknown)"] = currentTick;
							_interlockedLongTaskInc();
							var origin = (item as WorkItem)?.Origin;
							if (!string.IsNullOrEmpty(origin))
							{
								logWarn?.Invoke($"[RimAI.Core][P3][Scheduler] Long task '{item.Name}' took {swTask.ElapsedMilliseconds} ms; origin={origin}");
							}
							else
							{
								logWarn?.Invoke($"[RimAI.Core][P3][Scheduler] Long task '{item.Name}' took {swTask.ElapsedMilliseconds} ms");
							}
						}
					}
				}
				catch (OperationCanceledException)
				{
					// ignore
				}
				catch (Exception ex)
				{
					logError?.Invoke($"[RimAI.Core][P3][Scheduler] Task '{item.Name}' failed: {ex}");
				}
				processed++;
				_interlockedTotalProcessedInc();
				if (swFrame.Elapsed.TotalMilliseconds > budgetMs)
				{
					var qNow = _queue.Count;
					if (warnOnBudgetExceeded)
					{
						logWarn?.Invoke(
							$"[RimAI.Core][P3][Scheduler] Frame budget exceeded: {swFrame.Elapsed.TotalMilliseconds:F3} ms (budget={budgetMs} ms); " +
							$"processed={processed}/{maxTasks}, queue(start={queueLenStart}, now={qNow}); last='{lastItemName ?? "(unknown)"}' took {lastItemMs} ms"
						);
					}
					break;
				}
			}

			swFrame.Stop();
			_lastFrameProcessed = processed;
			_lastFrameMs = swFrame.Elapsed.TotalMilliseconds;
		}

		private string _maybeCaptureOrigin(string name)
		{
			try
			{
				if (string.IsNullOrEmpty(name)) return null;
				// Only capture for this noisy task to keep overhead minimal
				if (!string.Equals(name, "GetPoweredAiServerThingIds", StringComparison.Ordinal)) return null;
				var st = new StackTrace(1, false); // skip this helper's caller
				var parts = new System.Collections.Generic.List<string>(2);
				for (int i = 0; i < st.FrameCount && parts.Count < 2; i++)
				{
					var f = st.GetFrame(i);
					var m = f?.GetMethod();
					var dt = m?.DeclaringType;
					var full = dt?.FullName ?? string.Empty;
					if (string.IsNullOrEmpty(full)) continue;
					// skip internal scheduler frames
					if (full.Contains("Infrastructure.Scheduler")) continue;
					var typeName = dt.Name;
					var methodName = m.Name;
					parts.Add(typeName + "." + methodName);
				}
				if (parts.Count == 0) return null;
				return string.Join(" <= ", parts);
			}
			catch { return null; }
		}

		public SchedulerSnapshot GetSnapshot()
		{
			return new SchedulerSnapshot
			{
				QueueLength = _queue.Count,
				LastFrameProcessed = _lastFrameProcessed,
				LastFrameMs = _lastFrameMs,
				LongTaskCount = _longTaskCount,
				TotalProcessed = _totalProcessed
			};
		}

		private void _interlockedLongTaskInc() => Interlocked.Increment(ref _longTaskCount);
		private void _interlockedTotalProcessedInc() => Interlocked.Increment(ref _totalProcessed);
	}

	internal sealed class SchedulerSnapshot
	{
		public int QueueLength { get; set; }
		public int LastFrameProcessed { get; set; }
		public double LastFrameMs { get; set; }
		public int LongTaskCount { get; set; }
		public long TotalProcessed { get; set; }
	}
}


