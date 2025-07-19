using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.LLM.Models;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// 殖民地分析器接口 - 负责收集和分析殖民地状态
    /// </summary>
    public interface IColonyAnalyzer
    {
        ColonyStatus AnalyzeCurrentStatus();
        List<ThreatInfo> IdentifyThreats();
        ResourceReport GenerateResourceReport();
        List<string> GetActiveEvents();
        string GetColonyOverview();
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
        
        Task<string> GetAdviceAsync(CancellationToken cancellationToken = default);
        Task<T> GetStructuredAdviceAsync<T>(CancellationToken cancellationToken = default) where T : class;
        Task<string> GetStreamingAdviceAsync(Action<string> onPartialResponse, CancellationToken cancellationToken = default);
        
        bool IsAvailable { get; }
        string GetStatusInfo();
        void CancelCurrentOperation();
    }

    /// <summary>
    /// LLM服务接口 - 与Framework的抽象层
    /// </summary>
    public interface ILLMService
    {
        Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default);
        Task<T> SendJsonRequestAsync<T>(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default) where T : class;
        Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMOptions options = null, CancellationToken cancellationToken = default);
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
    /// 缓存服务接口
    /// </summary>
    public interface ICacheService
    {
        Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
        void Remove(string key);
        void Clear();
        bool Contains(string key);
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
