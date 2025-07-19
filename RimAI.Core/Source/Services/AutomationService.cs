using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 自动化任务服务 - 管理和执行自动化任务
    /// </summary>
    public class AutomationService
    {
        private static AutomationService _instance;
        public static AutomationService Instance => _instance ??= new AutomationService();

        private readonly ConcurrentDictionary<string, AutomationTask> _tasks = new ConcurrentDictionary<string, AutomationTask>();
        private readonly Timer _executionTimer;
        private readonly object _executionLock = new object();
        private bool _isRunning = false;

        public event Action<AutomationTask> TaskCompleted;
        public event Action<AutomationTask, Exception> TaskFailed;

        private AutomationService()
        {
            // 每5秒检查一次任务
            _executionTimer = new Timer(ExecuteScheduledTasks, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        #region 公共接口

        /// <summary>
        /// 注册新的自动化任务
        /// </summary>
        public bool RegisterTask(AutomationTask task)
        {
            try
            {
                if (task == null || string.IsNullOrEmpty(task.Id))
                {
                    Log.Warning("[AutomationService] Invalid task provided");
                    return false;
                }

                task.Status = TaskStatus.Registered;
                task.RegisteredAt = DateTime.Now;

                var registered = _tasks.TryAdd(task.Id, task);
                
                if (registered)
                {
                    Log.Message($"[AutomationService] Task '{task.Name}' registered successfully");
                }
                else
                {
                    Log.Warning($"[AutomationService] Task '{task.Name}' already exists");
                }

                return registered;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutomationService] Failed to register task: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 取消注册任务
        /// </summary>
        public bool UnregisterTask(string taskId)
        {
            try
            {
                if (_tasks.TryRemove(taskId, out var task))
                {
                    task.Status = TaskStatus.Cancelled;
                    Log.Message($"[AutomationService] Task '{task.Name}' unregistered");
                    return true;
                }

                Log.Warning($"[AutomationService] Task '{taskId}' not found");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[AutomationService] Failed to unregister task '{taskId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 手动执行任务
        /// </summary>
        public async Task<bool> ExecuteTaskAsync(string taskId)
        {
            try
            {
                if (!_tasks.TryGetValue(taskId, out var task))
                {
                    Log.Warning($"[AutomationService] Task '{taskId}' not found");
                    return false;
                }

                return await ExecuteTaskAsync(task);
            }
            catch (Exception ex)
            {
                Log.Error($"[AutomationService] Failed to execute task '{taskId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取所有任务状态
        /// </summary>
        public List<TaskStatusInfo> GetTasksStatus()
        {
            try
            {
                return _tasks.Values.Select(task => new TaskStatusInfo
                {
                    Id = task.Id,
                    Name = task.Name,
                    Status = task.Status,
                    Priority = task.Priority,
                    LastExecuted = task.LastExecuted,
                    NextExecution = task.GetNextExecutionTime(),
                    ExecutionCount = task.ExecutionCount,
                    LastResult = task.LastResult
                }).ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[AutomationService] Failed to get tasks status: {ex.Message}");
                return new List<TaskStatusInfo>();
            }
        }

        /// <summary>
        /// 暂停/恢复服务
        /// </summary>
        public void ToggleService()
        {
            _isRunning = !_isRunning;
            Log.Message($"[AutomationService] Service {(_isRunning ? "resumed" : "paused")}");
        }

        /// <summary>
        /// 获取任务详情
        /// </summary>
        public AutomationTask GetTask(string taskId)
        {
            _tasks.TryGetValue(taskId, out var task);
            return task;
        }

        /// <summary>
        /// 清除已完成的任务
        /// </summary>
        public void CleanupCompletedTasks()
        {
            try
            {
                var completedTasks = _tasks.Values
                    .Where(t => t.Status == TaskStatus.Completed && 
                               t.Schedule?.Type != ScheduleType.Recurring)
                    .Select(t => t.Id)
                    .ToList();

                foreach (var taskId in completedTasks)
                {
                    _tasks.TryRemove(taskId, out _);
                }

                Log.Message($"[AutomationService] Cleaned up {completedTasks.Count} completed tasks");
            }
            catch (Exception ex)
            {
                Log.Error($"[AutomationService] Failed to cleanup tasks: {ex.Message}");
            }
        }

        #endregion

        #region 私有方法

        private void ExecuteScheduledTasks(object state)
        {
            if (!_isRunning) return;

            lock (_executionLock)
            {
                try
                {
                    var tasksToExecute = _tasks.Values
                        .Where(ShouldExecuteTask)
                        .OrderBy(t => t.Priority)
                        .ThenBy(t => t.GetNextExecutionTime())
                        .ToList();

                    foreach (var task in tasksToExecute)
                    {
                        _ = Task.Run(async () => await ExecuteTaskAsync(task));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[AutomationService] Error in scheduled execution: {ex.Message}");
                }
            }
        }

        private bool ShouldExecuteTask(AutomationTask task)
        {
            if (task.Status != TaskStatus.Registered && task.Status != TaskStatus.Ready)
                return false;

            if (task.Schedule == null)
                return false;

            var now = DateTime.Now;
            var nextExecutionTime = task.GetNextExecutionTime();

            return nextExecutionTime <= now;
        }

        private async Task<bool> ExecuteTaskAsync(AutomationTask task)
        {
            try
            {
                task.Status = TaskStatus.Running;
                task.LastExecuted = DateTime.Now;
                task.ExecutionCount++;

                Log.Message($"[AutomationService] Executing task '{task.Name}'");

                var result = await task.ExecuteAsync();
                
                task.LastResult = result;
                task.Status = task.Schedule?.Type == ScheduleType.Recurring ? 
                             TaskStatus.Ready : TaskStatus.Completed;

                TaskCompleted?.Invoke(task);

                Log.Message($"[AutomationService] Task '{task.Name}' completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                task.Status = TaskStatus.Failed;
                task.LastResult = $"Error: {ex.Message}";

                TaskFailed?.Invoke(task, ex);

                Log.Error($"[AutomationService] Task '{task.Name}' failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region 便捷方法

        /// <summary>
        /// 创建定期资源检查任务
        /// </summary>
        public AutomationTask CreateResourceCheckTask(string name, TimeSpan interval)
        {
            return new AutomationTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = "定期检查资源状况",
                Priority = TaskPriority.Medium,
                Schedule = new TaskSchedule
                {
                    Type = ScheduleType.Recurring,
                    Interval = interval
                },
                ExecutionDelegate = async () =>
                {
                    var analyzer = ColonyAnalyzer.Instance;
                    var report = analyzer.GenerateResourceReport();
                    
                    if (report.CriticalShortages.Any())
                    {
                        var message = $"资源警告: {string.Join(", ", report.CriticalShortages)} 严重短缺!";
                        Messages.Message(message, MessageTypeDefOf.ThreatBig);
                        return $"发现 {report.CriticalShortages.Count} 项关键短缺";
                    }

                    return "资源状况正常";
                }
            };
        }

        /// <summary>
        /// 创建威胁监控任务
        /// </summary>
        public AutomationTask CreateThreatMonitorTask(string name, TimeSpan interval)
        {
            return new AutomationTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = "监控潜在威胁",
                Priority = TaskPriority.High,
                Schedule = new TaskSchedule
                {
                    Type = ScheduleType.Recurring,
                    Interval = interval
                },
                ExecutionDelegate = async () =>
                {
                    var analyzer = ColonyAnalyzer.Instance;
                    var threats = analyzer.IdentifyThreats();
                    
                    var criticalThreats = threats.Where(t => t.Level == ThreatLevel.Critical || t.Level == ThreatLevel.High).ToList();
                    
                    if (criticalThreats.Any())
                    {
                        var message = $"威胁警报: 发现 {criticalThreats.Count} 个高威胁目标!";
                        Messages.Message(message, MessageTypeDefOf.ThreatBig);
                        return $"发现 {criticalThreats.Count} 个关键威胁";
                    }

                    return $"监控正常，发现 {threats.Count} 个一般威胁";
                }
            };
        }

        /// <summary>
        /// 创建自动保存任务
        /// </summary>
        public AutomationTask CreateAutoSaveTask(string name, TimeSpan interval)
        {
            return new AutomationTask
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Description = "自动保存游戏进度",
                Priority = TaskPriority.Low,
                Schedule = new TaskSchedule
                {
                    Type = ScheduleType.Recurring,
                    Interval = interval
                },
                ExecutionDelegate = async () =>
                {
                    try
                    {
                        LongEventHandler.QueueLongEvent(() =>
                        {
                            GameDataSaveLoader.SaveGame($"AutoSave_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
                        }, "Saving", false, null);
                        
                        return "自动保存完成";
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"自动保存失败: {ex.Message}");
                    }
                }
            };
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 自动化任务
    /// </summary>
    public class AutomationTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.Created;
        public TaskSchedule Schedule { get; set; }
        public DateTime RegisteredAt { get; set; }
        public DateTime? LastExecuted { get; set; }
        public int ExecutionCount { get; set; } = 0;
        public string LastResult { get; set; }

        // 执行委托
        public Func<Task<string>> ExecutionDelegate { get; set; }

        public DateTime GetNextExecutionTime()
        {
            if (Schedule == null) return DateTime.MaxValue;

            if (LastExecuted.HasValue && Schedule.Type == ScheduleType.Recurring)
            {
                return LastExecuted.Value.Add(Schedule.Interval);
            }

            if (Schedule.ScheduledTime.HasValue)
            {
                return Schedule.ScheduledTime.Value;
            }

            return RegisteredAt.Add(Schedule.Interval);
        }

        public async Task<string> ExecuteAsync()
        {
            if (ExecutionDelegate == null)
                throw new InvalidOperationException("No execution delegate defined");

            return await ExecutionDelegate();
        }
    }

    /// <summary>
    /// 任务优先级
    /// </summary>
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum TaskStatus
    {
        Created,
        Registered,
        Ready,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 任务调度配置
    /// </summary>
    public class TaskSchedule
    {
        public ScheduleType Type { get; set; }
        public TimeSpan Interval { get; set; }
        public DateTime? ScheduledTime { get; set; }
        public int MaxExecutions { get; set; } = -1; // -1 表示无限制
    }

    /// <summary>
    /// 调度类型
    /// </summary>
    public enum ScheduleType
    {
        Once,       // 一次性
        Recurring,  // 周期性
        Scheduled   // 指定时间
    }

    /// <summary>
    /// 任务状态信息
    /// </summary>
    public class TaskStatusInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public TaskStatus Status { get; set; }
        public TaskPriority Priority { get; set; }
        public DateTime? LastExecuted { get; set; }
        public DateTime NextExecution { get; set; }
        public int ExecutionCount { get; set; }
        public string LastResult { get; set; }
    }

    #endregion
}
