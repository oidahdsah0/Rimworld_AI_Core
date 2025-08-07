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
    public class EventAggregatorService : IEventAggregatorService
    {
        private readonly IEventBus _eventBus;
        private readonly ILLMService _llmService;
        private readonly IConfigurationService _configService;

        private readonly List<IEvent> _eventBuffer = new List<IEvent>();
        private readonly object _bufferLock = new object();
        private Timer _timer;
        private bool _isCoolingDown = false;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public EventAggregatorService(IEventBus eventBus, ILLMService llmService, IConfigurationService configService)
        {
            _eventBus = eventBus;
            _llmService = llmService;
            _configService = configService;
        }

        public void Initialize()
        {
            // Subscribe to all events. A more advanced implementation might allow filtering.
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

            // If a critical event comes in, process immediately regardless of the timer.
            if (@event.Priority == EventPriority.Critical)
            {
                Task.Run(() => ProcessBuffer(null));
            }
        }

        private async void ProcessBuffer(object state)
        {
            if (_isCoolingDown) return;

            List<IEvent> eventsToProcess;
            lock (_bufferLock)
            {
                if (_eventBuffer.Count == 0 || _eventBuffer.All(e => e.Priority < EventPriority.High))
                {
                    // If only low/medium priority events, wait for the next cycle unless buffer is huge
                    if (_eventBuffer.Count < _configService.Current.EventAggregator.MaxBufferSize)
                    {
                         return;
                    }
                }
                
                // Copy and clear the buffer for processing
                eventsToProcess = new List<IEvent>(_eventBuffer);
                _eventBuffer.Clear();
            }

            if (eventsToProcess.Count == 0) return;

            // Set cooldown flag immediately to prevent concurrent processing
            _isCoolingDown = true;

            try
            {
                // Sort by priority (descending) and then by time (ascending)
                eventsToProcess = eventsToProcess
                    .OrderByDescending(e => e.Priority)
                    .ThenBy(e => e.Timestamp)
                    .ToList();

                var prompt = BuildPromptFromEvents(eventsToProcess);

                // Assuming a simple fire-and-forget call for now.
                // A more robust implementation would handle the response.
                await _llmService.GetResponseAsync(prompt);
            }
            finally
            {
                // Start the cooldown timer. After it expires, reset the flag.
                var cooldown = TimeSpan.FromMinutes(_configService.Current.EventAggregator.CooldownMinutes);
                await Task.Delay(cooldown, _cancellationTokenSource.Token)
                    .ContinueWith(t => _isCoolingDown = false, TaskContinuationOptions.NotOnCanceled);
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
        
        // Proper disposal to stop the timer and cancel any pending tasks
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _timer?.Dispose();
            _cancellationTokenSource.Dispose();
        }
    }
}

