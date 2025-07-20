using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Analysis;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Prompts;
using RimAI.Core.Services;
using RimAI.Framework.LLM.Models;
using Verse;

namespace RimAI.Core.Officers.Base
{
    /// <summary>
    /// AI官员基类 - 提供通用功能和统一接口
    /// </summary>
    public abstract class OfficerBase : IAIOfficer
    {
        protected readonly IPromptBuilder _promptBuilder;
        protected readonly ILLMService _llmService;
        protected readonly ICacheService _cacheService;
        protected readonly IColonyAnalyzer _analyzer;

        private CancellationTokenSource _currentOperationCts;
        protected readonly object _operationLock = new object();

        // 抽象属性 - 子类必须实现
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract string IconPath { get; }
        public abstract OfficerRole Role { get; }

        // 模板相关属性 - 子类可以重写
        protected virtual string QuickAdviceTemplateId => GetDefaultTemplateId("quick");
        protected virtual string DetailedAdviceTemplateId => GetDefaultTemplateId("detailed");
        protected virtual string StreamingTemplateId => GetDefaultTemplateId("streaming");

        protected OfficerBase()
        {
            _analyzer = ColonyAnalyzer.Instance;
            _promptBuilder = PromptBuilder.Instance;
            _llmService = LLMService.Instance;
            _cacheService = CacheService.Instance;
        }

        #region 公共接口实现

        public virtual bool IsAvailable
        {
            get
            {
                try
                {
                    return _llmService.IsInitialized && Find.CurrentMap != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public virtual async Task<string> GetAdviceAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return GetUnavailableMessage();
            }

            // 创建操作令牌
            using var operationCts = CreateOperationToken(cancellationToken);

            try
            {
                // 使用缓存键
                var cacheKey = GenerateCacheKey("advice");
                
                return await _cacheService.GetOrCreateAsync(
                    cacheKey,
                    () => ExecuteAdviceRequest(operationCts.Token),
                    TimeSpan.FromMinutes(2) // 建议缓存2分钟
                );
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[{Name}] Advice request was cancelled");
                return "建议请求已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] Failed to get advice: {ex.Message}");
                return GetErrorMessage(ex.Message);
            }
        }

        public virtual async Task<T> GetStructuredAdviceAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            if (!IsAvailable)
            {
                return null;
            }

            using var operationCts = CreateOperationToken(cancellationToken);

            try
            {
                var context = await BuildContextAsync(operationCts.Token);
                var prompt = _promptBuilder.BuildPrompt(DetailedAdviceTemplateId, context);
                var options = CreateLLMOptions(temperature: 0.6f, forceJson: true);

                var result = await _llmService.SendJsonRequestAsync<T>(prompt, options, operationCts.Token);
                
                Log.Message($"[{Name}] Structured advice generated");
                return result;
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[{Name}] Structured advice request was cancelled");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] Failed to get structured advice: {ex.Message}");
                return null;
            }
        }

        public virtual async Task<string> GetStreamingAdviceAsync(Action<string> onPartialResponse, CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                var unavailableMsg = GetUnavailableMessage();
                onPartialResponse?.Invoke(unavailableMsg);
                return unavailableMsg;
            }

            if (!_llmService.IsStreamingAvailable)
            {
                Log.Warning($"[{Name}] Streaming not available, falling back to standard advice");
                var standardAdvice = await GetAdviceAsync(cancellationToken);
                onPartialResponse?.Invoke(standardAdvice);
                return standardAdvice;
            }

            using var operationCts = CreateOperationToken(cancellationToken);

            try
            {
                var context = await BuildContextAsync(operationCts.Token);
                var prompt = _promptBuilder.BuildPrompt(StreamingTemplateId, context);
                var options = CreateLLMOptions(temperature: 0.8f, forceStreaming: true);

                var fullResponse = "";
                
                await _llmService.SendStreamingMessageAsync(
                    prompt,
                    chunk =>
                    {
                        if (!operationCts.Token.IsCancellationRequested)
                        {
                            fullResponse += chunk;
                            onPartialResponse?.Invoke(fullResponse);
                        }
                    },
                    options,
                    operationCts.Token
                );

                Log.Message($"[{Name}] Streaming advice completed");
                return fullResponse;
            }
            catch (OperationCanceledException)
            {
                var cancelledMsg = "流式建议已取消";
                onPartialResponse?.Invoke(cancelledMsg);
                return cancelledMsg;
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] Failed to get streaming advice: {ex.Message}");
                var errorMsg = GetErrorMessage(ex.Message);
                onPartialResponse?.Invoke(errorMsg);
                return errorMsg;
            }
        }

        public virtual void CancelCurrentOperation()
        {
            lock (_operationLock)
            {
                if (_currentOperationCts != null && !_currentOperationCts.IsCancellationRequested)
                {
                    _currentOperationCts.Cancel();
                    Log.Message($"[{Name}] Current operation cancelled by user");
                }
            }
        }

        public virtual string GetStatusInfo()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"🤖 {Name} 状态报告");
            status.AppendLine($"角色: {GetRoleDescription()}");
            status.AppendLine($"可用性: {(IsAvailable ? "✅ 就绪" : "❌ 不可用")}");
            
            if (IsAvailable)
            {
                status.AppendLine($"LLM服务: {(_llmService.IsStreamingAvailable ? "🚀 支持流式" : "📝 标准模式")}");
                
                // 添加专业状态信息
                var professionalStatus = GetProfessionalStatus();
                if (!string.IsNullOrEmpty(professionalStatus))
                {
                    status.AppendLine($"专业状态: {professionalStatus}");
                }
            }
            
            return status.ToString();
        }

        #endregion

        #region 抽象和虚拟方法 - 子类可以重写

        /// <summary>
        /// 构建上下文信息 - 子类必须实现
        /// </summary>
        protected abstract Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取专业状态信息 - 子类可以重写
        /// </summary>
        protected virtual string GetProfessionalStatus()
        {
            return null;
        }

        /// <summary>
        /// 创建LLM选项 - 子类可以重写
        /// </summary>
        protected virtual LLMRequestOptions CreateLLMOptions(float temperature = 0.7f, bool forceStreaming = false, bool forceJson = false)
        {
            if (forceJson)
            {
                return new LLMRequestOptions 
                { 
                    Temperature = temperature,
                    // 可以在这里添加JSON相关的选项
                };
            }
            else if (forceStreaming && _llmService.IsStreamingAvailable)
            {
                return new LLMRequestOptions { Temperature = temperature };
            }
            else
            {
                return new LLMRequestOptions { Temperature = temperature };
            }
        }

        /// <summary>
        /// 处理错误响应 - 子类可以重写
        /// </summary>
        protected virtual string GetErrorMessage(string error)
        {
            return $"❌ {Name}服务暂时不可用: {error}";
        }

        /// <summary>
        /// 处理不可用状态 - 子类可以重写
        /// </summary>
        protected virtual string GetUnavailableMessage()
        {
            return $"❌ {Name}当前不可用 - 请检查AI框架连接和地图状态";
        }

        #endregion

        #region 辅助方法

        private async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
        {
            var context = await BuildContextAsync(cancellationToken);
            var prompt = _promptBuilder.BuildPrompt(QuickAdviceTemplateId, context);
            var options = CreateLLMOptions();

            var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
            
            Log.Message($"[{Name}] Advice generated successfully");
            return response ?? "无法生成建议";
        }

        private CancellationTokenSource CreateOperationToken(CancellationToken cancellationToken)
        {
            lock (_operationLock)
            {
                // 取消之前的操作
                _currentOperationCts?.Cancel();
                _currentOperationCts?.Dispose();
                
                // 创建新的操作令牌
                _currentOperationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                return _currentOperationCts;
            }
        }

        private string GenerateCacheKey(string operation)
        {
            var mapId = Find.CurrentMap?.uniqueID ?? 0;
            var tick = Find.TickManager.TicksGame;
            
            // 每10分钟更新一次缓存
            var timeSegment = tick / (GenTicks.TicksPerRealSecond * 600);
            
            return $"{Role}_{operation}_{mapId}_{timeSegment}";
        }

        private string GetDefaultTemplateId(string variant)
        {
            var rolePrefix = Role.ToString().ToLower();
            return $"{rolePrefix}.{variant}";
        }

        private string GetRoleDescription()
        {
            return Role switch
            {
                OfficerRole.Governor => "总督 - 整体管理决策",
                OfficerRole.Military => "军事官 - 防务与战斗",
                OfficerRole.Logistics => "后勤官 - 资源与建设",
                OfficerRole.Medical => "医疗官 - 健康与医疗",
                OfficerRole.Research => "科研官 - 研究与技术",
                OfficerRole.Diplomat => "外交官 - 对外关系",
                OfficerRole.Security => "安全官 - 内部秩序",
                OfficerRole.Economy => "经济官 - 贸易与财务",
                _ => "未知角色"
            };
        }

        #endregion

        #region 资源清理

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_operationLock)
                {
                    _currentOperationCts?.Cancel();
                    _currentOperationCts?.Dispose();
                    _currentOperationCts = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~OfficerBase()
        {
            Dispose(false);
        }

        #endregion
    }
}
