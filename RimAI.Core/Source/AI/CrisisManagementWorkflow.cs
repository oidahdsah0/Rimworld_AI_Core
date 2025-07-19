using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Analysis;
using RimAI.Core.Officers;
using RimAI.Core.Services;
using Verse;

namespace RimAI.Core.AI
{
    /// <summary>
    /// 智能危机管理工作流 - 自动化处理殖民地危机
    /// </summary>
    public class CrisisManagementWorkflow
    {
        private static CrisisManagementWorkflow _instance;
        public static CrisisManagementWorkflow Instance => _instance ??= new CrisisManagementWorkflow();

        private readonly IColonyAnalyzer _analyzer;
        private readonly AutomationService _automationService;
        private readonly List<IAIOfficer> _officers;
        private readonly ICacheService _cacheService;

        private bool _isActive = false;
        private CancellationTokenSource _workflowCts;

        // 工作流状态
        public bool IsActive => _isActive;
        public DateTime LastExecution { get; private set; }
        public string LastResult { get; private set; }
        public List<CrisisEvent> ActiveCrises { get; private set; } = new List<CrisisEvent>();

        // 事件通知
        public event Action<CrisisEvent> CrisisDetected;
        public event Action<CrisisEvent, string> CrisisResolved;
        public event Action<Exception> WorkflowError;

        private CrisisManagementWorkflow()
        {
            _analyzer = ColonyAnalyzer.Instance;
            _automationService = AutomationService.Instance;
            _cacheService = CacheService.Instance;
            
            // 初始化官员团队
            _officers = new List<IAIOfficer>
            {
                LogisticsOfficer.Instance,
                // 可以添加更多官员
            };
        }

        #region 公共接口

        /// <summary>
        /// 启动危机管理工作流
        /// </summary>
        public async Task<bool> StartWorkflowAsync()
        {
            try
            {
                if (_isActive)
                {
                    Log.Warning("[CrisisManagement] Workflow already active");
                    return false;
                }

                _workflowCts = new CancellationTokenSource();
                _isActive = true;

                Log.Message("[CrisisManagement] Starting crisis management workflow");

                // 注册自动化任务
                RegisterAutomationTasks();

                // 启动主循环
                _ = Task.Run(() => MainWorkflowLoop(_workflowCts.Token));

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CrisisManagement] Failed to start workflow: {ex.Message}");
                WorkflowError?.Invoke(ex);
                return false;
            }
        }

        /// <summary>
        /// 停止危机管理工作流
        /// </summary>
        public void StopWorkflow()
        {
            try
            {
                if (!_isActive) return;

                Log.Message("[CrisisManagement] Stopping crisis management workflow");

                _workflowCts?.Cancel();
                _isActive = false;
                
                // 清理自动化任务
                CleanupAutomationTasks();

                Log.Message("[CrisisManagement] Workflow stopped");
            }
            catch (Exception ex)
            {
                Log.Error($"[CrisisManagement] Error stopping workflow: {ex.Message}");
                WorkflowError?.Invoke(ex);
            }
        }

        /// <summary>
        /// 手动触发危机评估
        /// </summary>
        public async Task<CrisisAssessmentResult> PerformCrisisAssessmentAsync()
        {
            try
            {
                Log.Message("[CrisisManagement] Performing manual crisis assessment");

                var assessment = await ExecuteCrisisAssessmentAsync(_workflowCts?.Token ?? CancellationToken.None);
                
                LastExecution = DateTime.Now;
                LastResult = $"评估完成: 发现 {assessment.DetectedCrises.Count} 个危机";

                return assessment;
            }
            catch (Exception ex)
            {
                Log.Error($"[CrisisManagement] Crisis assessment failed: {ex.Message}");
                LastResult = $"评估失败: {ex.Message}";
                WorkflowError?.Invoke(ex);
                
                return new CrisisAssessmentResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 获取工作流状态
        /// </summary>
        public WorkflowStatus GetStatus()
        {
            return new WorkflowStatus
            {
                IsActive = _isActive,
                LastExecution = LastExecution,
                LastResult = LastResult,
                ActiveCrisesCount = ActiveCrises.Count,
                RegisteredOfficers = _officers.Count,
                UpTime = _isActive && LastExecution != default ? DateTime.Now - LastExecution : TimeSpan.Zero
            };
        }

        #endregion

        #region 核心工作流逻辑

        private async Task MainWorkflowLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        // 执行危机评估
                        var assessment = await ExecuteCrisisAssessmentAsync(cancellationToken);
                        
                        // 处理新发现的危机
                        await ProcessNewCrises(assessment.DetectedCrises, cancellationToken);
                        
                        // 更新已有危机状态
                        await UpdateExistingCrises(cancellationToken);
                        
                        // 执行响应行动
                        await ExecuteResponseActions(cancellationToken);

                        LastExecution = DateTime.Now;
                        LastResult = $"循环完成: {ActiveCrises.Count} 个活跃危机";

                        // 等待下一次循环 (每分钟执行一次)
                        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CrisisManagement] Error in workflow loop: {ex.Message}");
                        WorkflowError?.Invoke(ex);
                        
                        // 出错后等待较长时间再重试
                        await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message("[CrisisManagement] Workflow loop cancelled");
            }
            finally
            {
                _isActive = false;
            }
        }

        private async Task<CrisisAssessmentResult> ExecuteCrisisAssessmentAsync(CancellationToken cancellationToken)
        {
            var result = new CrisisAssessmentResult { Success = true };
            var detectedCrises = new List<CrisisEvent>();

            try
            {
                // 分析殖民地状态
                var colonyStatus = _analyzer.AnalyzeCurrentStatus();
                var threats = _analyzer.IdentifyThreats();
                var resourceReport = _analyzer.GenerateResourceReport();

                // 检测资源危机
                foreach (var shortage in resourceReport.CriticalShortages)
                {
                    var crisis = new CrisisEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = CrisisType.ResourceShortage,
                        Severity = CrisisSeverity.High,
                        Title = $"{shortage} 严重短缺",
                        Description = $"关键资源 {shortage} 出现严重短缺，需要立即采取行动",
                        DetectedAt = DateTime.Now,
                        Status = CrisisStatus.Active,
                        Context = new Dictionary<string, object>
                        {
                            ["ResourceName"] = shortage,
                            ["ResourceReport"] = resourceReport
                        }
                    };
                    
                    detectedCrises.Add(crisis);
                }

                // 检测威胁危机
                var criticalThreats = threats.Where(t => t.Level == ThreatLevel.Critical || t.Level == ThreatLevel.High);
                foreach (var threat in criticalThreats)
                {
                    var crisis = new CrisisEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = CrisisType.ExternalThreat,
                        Severity = threat.Level == ThreatLevel.Critical ? CrisisSeverity.Critical : CrisisSeverity.High,
                        Title = threat.Description,
                        Description = $"检测到 {threat.Type} 威胁: {threat.Description}",
                        DetectedAt = DateTime.Now,
                        Status = CrisisStatus.Active,
                        Context = new Dictionary<string, object>
                        {
                            ["ThreatInfo"] = threat
                        }
                    };
                    
                    detectedCrises.Add(crisis);
                }

                // 检测健康危机
                if (colonyStatus.Colonists.Count(c => !c.IsAvailable) > colonyStatus.ColonistCount / 2)
                {
                    var crisis = new CrisisEvent
                    {
                        Id = Guid.NewGuid().ToString(),
                        Type = CrisisType.Medical,
                        Severity = CrisisSeverity.Critical,
                        Title = "大量殖民者无法工作",
                        Description = "超过半数殖民者因伤病或其他原因无法正常工作",
                        DetectedAt = DateTime.Now,
                        Status = CrisisStatus.Active,
                        Context = new Dictionary<string, object>
                        {
                            ["UnavailableCount"] = colonyStatus.Colonists.Count(c => !c.IsAvailable),
                            ["TotalCount"] = colonyStatus.ColonistCount
                        }
                    };
                    
                    detectedCrises.Add(crisis);
                }

                result.DetectedCrises = detectedCrises;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task ProcessNewCrises(List<CrisisEvent> newCrises, CancellationToken cancellationToken)
        {
            foreach (var crisis in newCrises)
            {
                // 检查是否是已知危机
                var existing = ActiveCrises.FirstOrDefault(c => c.Type == crisis.Type && c.Title == crisis.Title);
                if (existing != null)
                {
                    // 更新现有危机
                    existing.LastUpdated = DateTime.Now;
                    continue;
                }

                // 添加新危机
                ActiveCrises.Add(crisis);
                
                // 通知危机检测
                CrisisDetected?.Invoke(crisis);
                
                // 生成响应计划
                crisis.ResponsePlan = await GenerateResponsePlan(crisis, cancellationToken);
                
                Log.Message($"[CrisisManagement] New crisis detected: {crisis.Title}");
                
                // 发送游戏内消息
                var messageType = crisis.Severity == CrisisSeverity.Critical ? 
                    MessageTypeDefOf.ThreatBig : MessageTypeDefOf.CautionInput;
                
                Messages.Message($"危机警报: {crisis.Title}", messageType);
            }
        }

        private async Task UpdateExistingCrises(CancellationToken cancellationToken)
        {
            var crisesToRemove = new List<CrisisEvent>();

            foreach (var crisis in ActiveCrises.ToList())
            {
                try
                {
                    // 检查危机是否仍然存在
                    var stillActive = await IsCrisisStillActive(crisis, cancellationToken);
                    
                    if (!stillActive)
                    {
                        crisis.Status = CrisisStatus.Resolved;
                        crisis.ResolvedAt = DateTime.Now;
                        crisesToRemove.Add(crisis);
                        
                        CrisisResolved?.Invoke(crisis, "危机已自然解决");
                        Log.Message($"[CrisisManagement] Crisis resolved: {crisis.Title}");
                    }
                    else
                    {
                        crisis.LastUpdated = DateTime.Now;
                        
                        // 如果危机持续时间过长，升级严重程度
                        if (crisis.DetectedAt < DateTime.Now.AddHours(-2) && 
                            crisis.Severity != CrisisSeverity.Critical)
                        {
                            crisis.Severity = CrisisSeverity.Critical;
                            Log.Warning($"[CrisisManagement] Crisis escalated: {crisis.Title}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CrisisManagement] Error updating crisis '{crisis.Title}': {ex.Message}");
                }
            }

            // 移除已解决的危机
            foreach (var crisis in crisesToRemove)
            {
                ActiveCrises.Remove(crisis);
            }
        }

        private async Task ExecuteResponseActions(CancellationToken cancellationToken)
        {
            foreach (var crisis in ActiveCrises.Where(c => c.Status == CrisisStatus.Active))
            {
                try
                {
                    if (crisis.ResponsePlan?.Actions?.Any() == true)
                    {
                        foreach (var action in crisis.ResponsePlan.Actions.Where(a => !a.IsExecuted))
                        {
                            try
                            {
                                await ExecuteResponseAction(crisis, action, cancellationToken);
                                action.IsExecuted = true;
                                action.ExecutedAt = DateTime.Now;
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[CrisisManagement] Failed to execute action '{action.Title}': {ex.Message}");
                                action.Error = ex.Message;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CrisisManagement] Error executing response actions for '{crisis.Title}': {ex.Message}");
                }
            }
        }

        #endregion

        #region 辅助方法

        private async Task<ResponsePlan> GenerateResponsePlan(CrisisEvent crisis, CancellationToken cancellationToken)
        {
            try
            {
                var plan = new ResponsePlan
                {
                    CrisisId = crisis.Id,
                    GeneratedAt = DateTime.Now,
                    Actions = new List<ResponseAction>()
                };

                switch (crisis.Type)
                {
                    case CrisisType.ResourceShortage:
                        plan.Actions.Add(new ResponseAction
                        {
                            Title = "资源紧急采购",
                            Description = "寻找贸易机会或安排生产",
                            Priority = ActionPriority.High,
                            ActionType = "ResourceAcquisition"
                        });
                        break;

                    case CrisisType.ExternalThreat:
                        plan.Actions.Add(new ResponseAction
                        {
                            Title = "激活防御措施",
                            Description = "准备殖民者进入战斗状态",
                            Priority = ActionPriority.Critical,
                            ActionType = "DefenseActivation"
                        });
                        break;

                    case CrisisType.Medical:
                        plan.Actions.Add(new ResponseAction
                        {
                            Title = "医疗紧急响应",
                            Description = "优先处理受伤殖民者",
                            Priority = ActionPriority.High,
                            ActionType = "MedicalResponse"
                        });
                        break;
                }

                return plan;
            }
            catch (Exception ex)
            {
                Log.Error($"[CrisisManagement] Failed to generate response plan: {ex.Message}");
                return new ResponsePlan { CrisisId = crisis.Id, GeneratedAt = DateTime.Now };
            }
        }

        private async Task<bool> IsCrisisStillActive(CrisisEvent crisis, CancellationToken cancellationToken)
        {
            // 这里可以实现具体的危机检查逻辑
            // 简化版本：假设30分钟后危机可能自然解决
            return DateTime.Now - crisis.DetectedAt < TimeSpan.FromMinutes(30);
        }

        private async Task ExecuteResponseAction(CrisisEvent crisis, ResponseAction action, CancellationToken cancellationToken)
        {
            // 这里实现具体的响应行动
            Log.Message($"[CrisisManagement] Executing action: {action.Title}");
            
            // 根据行动类型执行不同的逻辑
            switch (action.ActionType)
            {
                case "ResourceAcquisition":
                    // 实现资源获取逻辑
                    break;
                case "DefenseActivation":
                    // 实现防御激活逻辑
                    break;
                case "MedicalResponse":
                    // 实现医疗响应逻辑
                    break;
            }
        }

        private void RegisterAutomationTasks()
        {
            // 注册危机监控任务
            var monitoringTask = new AutomationTask
            {
                Id = "crisis_monitoring",
                Name = "危机监控",
                Description = "定期监控殖民地危机状况",
                Priority = TaskPriority.High,
                Schedule = new TaskSchedule
                {
                    Type = ScheduleType.Recurring,
                    Interval = TimeSpan.FromMinutes(2)
                },
                ExecutionDelegate = async () =>
                {
                    if (!_isActive) return "工作流未激活";
                    
                    var assessment = await PerformCrisisAssessmentAsync();
                    return assessment.Success ? 
                        $"监控完成: {assessment.DetectedCrises.Count} 个新危机" : 
                        $"监控失败: {assessment.ErrorMessage}";
                }
            };

            _automationService.RegisterTask(monitoringTask);
        }

        private void CleanupAutomationTasks()
        {
            _automationService.UnregisterTask("crisis_monitoring");
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 危机事件
    /// </summary>
    public class CrisisEvent
    {
        public string Id { get; set; }
        public CrisisType Type { get; set; }
        public CrisisSeverity Severity { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; }
        public DateTime LastUpdated { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public CrisisStatus Status { get; set; }
        public Dictionary<string, object> Context { get; set; } = new Dictionary<string, object>();
        public ResponsePlan ResponsePlan { get; set; }
    }

    /// <summary>
    /// 危机类型
    /// </summary>
    public enum CrisisType
    {
        ResourceShortage,  // 资源短缺
        ExternalThreat,    // 外部威胁
        Medical,           // 医疗危机
        Infrastructure,    // 基础设施故障
        Social,           // 社会问题
        Environmental     // 环境问题
    }

    /// <summary>
    /// 危机严重程度
    /// </summary>
    public enum CrisisSeverity
    {
        Low,      // 低
        Medium,   // 中等
        High,     // 高
        Critical  // 危急
    }

    /// <summary>
    /// 危机状态
    /// </summary>
    public enum CrisisStatus
    {
        Active,      // 活跃
        Monitoring,  // 监控中
        Resolving,   // 解决中
        Resolved,    // 已解决
        Escalated    // 已升级
    }

    /// <summary>
    /// 响应计划
    /// </summary>
    public class ResponsePlan
    {
        public string CrisisId { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<ResponseAction> Actions { get; set; } = new List<ResponseAction>();
    }

    /// <summary>
    /// 响应行动
    /// </summary>
    public class ResponseAction
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public ActionPriority Priority { get; set; }
        public string ActionType { get; set; }
        public bool IsExecuted { get; set; }
        public DateTime? ExecutedAt { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// 行动优先级
    /// </summary>
    public enum ActionPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// 危机评估结果
    /// </summary>
    public class CrisisAssessmentResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public List<CrisisEvent> DetectedCrises { get; set; } = new List<CrisisEvent>();
    }

    /// <summary>
    /// 工作流状态
    /// </summary>
    public class WorkflowStatus
    {
        public bool IsActive { get; set; }
        public DateTime LastExecution { get; set; }
        public string LastResult { get; set; }
        public int ActiveCrisesCount { get; set; }
        public int RegisteredOfficers { get; set; }
        public TimeSpan UpTime { get; set; }
    }

    #endregion
}
