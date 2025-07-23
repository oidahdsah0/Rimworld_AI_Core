using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.LLM.Models;
using RimAI.Core.Analysis;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// 殖民地分析器接口 - 负责收集和分析殖民地状态
    /// 现代化异步版本，支持完整的分析流程
    /// </summary>
    public interface IColonyAnalyzer
    {
        /// <summary>
        /// 异步分析整个殖民地状况
        /// </summary>
        Task<ColonyAnalysisResult> AnalyzeColonyAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 快速获取殖民地状态摘要
        /// </summary>
        Task<string> GetQuickStatusSummaryAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 获取特定领域的专门分析
        /// </summary>
        Task<T> GetSpecializedAnalysisAsync<T>(CancellationToken cancellationToken = default) where T : class;
    }

    /// <summary>
    /// AI提示词构建器接口 - 负责构建和管理提示词
    /// </summary>
    public interface IPromptBuilder
    {
        string BuildPrompt(string templateId, Dictionary<string, object> context);
        void RegisterTemplate(string id, PromptTemplate template);
        PromptTemplate GetTemplate(string id);
        bool TemplateExists(string id);
    }

    /// <summary>
    /// AI官员基础接口
    /// </summary>
    public interface IAIOfficer
    {
        string Name { get; }
        string Description { get; }
        string IconPath { get; }
        OfficerRole Role { get; }
        bool IsAvailable { get; }

        Task<string> ProvideAdviceAsync(CancellationToken cancellationToken = default);
        Task<string> GetAdviceAsync(string topic, CancellationToken cancellationToken = default);
        void CancelCurrentOperation();
        string GetStatus();
    }

    /// <summary>
    /// LLM服务接口 - 与Framework的抽象层
    /// </summary>
    public interface ILLMService
    {
        Task<LLMResponse> SendMessageAsync(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default);
        Task<T> SendJsonRequestAsync<T>(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default) where T : class;
        Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMRequestOptions options = null, CancellationToken cancellationToken = default);
        bool IsStreamingAvailable { get; }
        bool IsInitialized { get; }
    }

    /// <summary>
    /// 事件处理器接口
    /// </summary>
    public interface IEventHandler
    {
        Task HandleAsync(IEvent eventData, CancellationToken cancellationToken = default);
    }

    public interface IEventHandler<in TEvent> : IEventHandler where TEvent : IEvent
    {
        Task HandleAsync(TEvent eventData, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 事件总线接口
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
        void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
        Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : IEvent;
    }

    /// <summary>
    /// 缓存服务接口 - 统一的缓存抽象
    /// </summary>
    public interface ICacheService
    {
        /// <summary>
        /// 获取或创建缓存值
        /// </summary>
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);

        /// <summary>
        /// 移除缓存项
        /// </summary>
        void Remove(string key);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        void Clear();

        /// <summary>
        /// 检查缓存是否包含指定键
        /// </summary>
        bool Contains(string key);

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        Services.CacheStats GetStats();
    }

    /// <summary>
    /// 基础事件接口
    /// </summary>
    public interface IEvent
    {
        DateTime Timestamp { get; }
        string EventId { get; }
    }
}
