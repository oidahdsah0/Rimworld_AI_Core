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
            _eventBus.Subscribe<IEvent>(OnEventReceived);

            var interval = TimeSpan.FromMinutes(_configService.Current.EventAggregator.ProcessingIntervalMinutes);
            _timer = new Timer(ProcessBuffer, null, interval, interval);
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
                if (_eventBuffer.Count == 0 || _eventBuffer.All(e => e.Priority < EventPriority.High))
                {
                    if (_eventBuffer.Count < _configService.Current.EventAggregator.MaxBufferSize)
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
                    .OrderByDescending(e => e.Priority)
                    .ThenBy(e => e.Timestamp)
                    .ToList();

                var prompt = BuildPromptFromEvents(eventsToProcess);
                await _llmService.GetResponseAsync(prompt);
            }
            catch (Exception ex)
            {
                Core.Infrastructure.CoreServices.Logger.Warn($"[EventAggregator] processing error: {ex.Message}");
            }
            finally
            {
                try
                {
                    var cooldown = TimeSpan.FromMinutes(_configService.Current.EventAggregator.CooldownMinutes);
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

