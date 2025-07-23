using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Analysis;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Core.Prompts;
using RimAI.Core.Services;
using RimAI.Framework.LLM.Models;
using Verse;
using System.Linq;

namespace RimAI.Core.Officers.Base
{
    /// <summary>
    /// AI官员基类 - 提供通用功能和统一接口
    /// </summary>
    public abstract class OfficerBase : IAIOfficer
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract OfficerRole Role { get; }
        public abstract string IconPath { get; }

        protected readonly ILLMService _llmService;
        private CancellationTokenSource _cancellationTokenSource;

        // Use IsInitialized from the ILLMService interface
        public bool IsAvailable => _llmService?.IsInitialized ?? false;

        protected OfficerBase()
        {
            _llmService = CoreServices.LLMService;
        }

        public virtual async Task<string> ProvideAdviceAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
            {
                return "AI service is not available.";
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                return await ExecuteAdviceRequest(_cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                return "Advice request was canceled.";
            }
            catch (System.Exception ex)
            {
                Verse.Log.Error($"[OfficerBase] Error providing advice: {ex}");
                return "An unexpected error occurred while processing the advice request.";
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public void CancelCurrentOperation()
        {
            _cancellationTokenSource?.Cancel();
        }

        public virtual Task<string> GetAdviceAsync(string topic, CancellationToken cancellationToken = default)
        {
            Log.Warning($"[OfficerBase] GetAdviceAsync(string topic) was called on {Name}, but this officer has not implemented a topic-specific advice method. Falling back to default advice.");
            return ProvideAdviceAsync(cancellationToken);
        }

        public string GetStatus()
        {
            return IsAvailable ? "Ready" : "Unavailable";
        }

        protected abstract Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken);
    }
}
