using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.LLM;

namespace RimAI.Core.Modules.Eventing
{
    public class EventAggregatorService : IEventAggregatorService, IDisposable
    {
        private readonly IEventBus _eventBus;
        private readonly ILLMService _llmService;
        private readonly IConfigurationService _configService;

        private readonly List<IEvent> _eventBuffer = new List<IEvent>();
        private readonly object _bufferLock = new object();
        private Timer _timer;
        private bool _isCoolingDown = false;
        private int _isProcessing = 0;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public EventAggregatorService(IEventBus eventBus, ILLMService llmService, IConfigurationService configService)
        {
            _eventBus = eventBus;
            _llmService = llmService;
            _configService = configService;
        }

        public void Initialize()
        {
            try
            {
                _eventBus?.Subscribe<IEvent>(OnEventReceived);
            }
            catch (Exception ex)
            {
                Core.Infrastructure.CoreServices.Logger.Warn($"[EventAggregator] subscribe failed: {ex.Message}");
            }

            try
            {
                var minutes = _configService?.Current?.EventAggregator?.ProcessingIntervalMinutes;
                if (minutes == null || minutes <= 0) minutes = 1.0;
                var interval = TimeSpan.FromMinutes(minutes.Value);
                _timer = new Timer(ProcessBuffer, null, interval, interval);
            }
            catch (Exception ex)
            {
                Core.Infrastructure.CoreServices.Logger.Warn($"[EventAggregator] timer init failed: {ex.Message}");
            }
        }

        private void OnEventReceived(IEvent @event)
        {
            lock (_bufferLock)
            {
                _eventBuffer.Add(@event);
            }

            if (@event.Priority == EventPriority.Critical)
            {
                Task.Run(() => ProcessBuffer(null));
            }
        }

        private async void ProcessBuffer(object state)
        {
            // 防止重入：若已在处理则直接返回
            if (Interlocked.Exchange(ref _isProcessing, 1) == 1) return;
            if (_isCoolingDown) { _isProcessing = 0; return; }

            List<IEvent> eventsToProcess;
            lock (_bufferLock)
            {
                if (_eventBuffer.Count == 0 || _eventBuffer.All(e => (e?.Priority ?? EventPriority.Low) < EventPriority.High))
                {
                    var max = _configService?.Current?.EventAggregator?.MaxBufferSize ?? 256;
                    if (_eventBuffer.Count < max)
                    {
                        _isProcessing = 0;
                        return;
                    }
                }

                eventsToProcess = new List<IEvent>(_eventBuffer);
                _eventBuffer.Clear();
            }

            if (eventsToProcess.Count == 0) { _isProcessing = 0; return; }

            _isCoolingDown = true;

            try
            {
                eventsToProcess = eventsToProcess
                    .Where(e => e != null)
                    .OrderByDescending(e => e.Priority)
                    .ThenBy(e => e.Timestamp)
                    .ToList();

                var prompt = BuildPromptFromEvents(eventsToProcess);
                var req = new RimAI.Framework.Contracts.UnifiedChatRequest
                {
                    Stream = false,
                    Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
                    {
                        new RimAI.Framework.Contracts.ChatMessage{ Role = "user", Content = prompt }
                    },
                    ConversationId = $"event:agg:{System.DateTime.UtcNow:yyyyMMddHHmm}:{eventsToProcess.Count}"
                };
                var res = await _llmService.GetResponseAsync(req);
                // 调用失败无需抛出，中断即可（冷却依然生效）
            }
            catch (Exception ex)
            {
                Core.Infrastructure.CoreServices.Logger.Warn($"[EventAggregator] processing error: {ex.Message}");
            }
            finally
            {
                try
                {
                    var mins = _configService?.Current?.EventAggregator?.CooldownMinutes;
                    if (mins == null || mins < 0) mins = 0.5; // 默认30秒冷却
                    var cooldown = TimeSpan.FromMinutes(mins.Value);
                    await Task.Delay(cooldown, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    // ignore
                }
                finally
                {
                    _isCoolingDown = false;
                    _isProcessing = 0;
                }
            }
        }

        private string BuildPromptFromEvents(List<IEvent> events)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Governor, the following events have occurred recently. Please provide a summary or assessment.");
            sb.AppendLine("---");

            foreach (var e in events)
            {
                sb.AppendLine($"- (Priority: {e.Priority}) {e.Describe()}");
            }
            
            sb.AppendLine("---");
            sb.AppendLine("What is your analysis?");

            return sb.ToString();
        }
        
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _timer?.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}

