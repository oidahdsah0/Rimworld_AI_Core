using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// LLM服务包装器 - 提供对Framework API的统一访问
    /// </summary>
    public class LLMService : ILLMService
    {
        private static LLMService _instance;
        public static LLMService Instance => _instance ??= new LLMService();

        public bool IsStreamingAvailable 
        { 
            get
            {
                try 
                {
                    return RimAIAPI.IsStreamingEnabled;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[LLMService] Failed to check streaming availability: {ex.Message}");
                    return false;
                }
            }
        }
        
        public bool IsInitialized 
        { 
            get
            {
                try 
                {
                    return RimAIAPI.IsInitialized;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[LLMService] Failed to check initialization: {ex.Message}");
                    return false;
                }
            }
        }

        private LLMService() { }

        public async Task<string> SendMessageAsync(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                Log.Warning("[LLMService] RimAI Framework is not initialized - this may be expected if Framework mod is not loaded");
                throw new InvalidOperationException("RimAI Framework is not initialized - please ensure the RimAI Framework mod is loaded and enabled");
            }

            try
            {
                var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
                return response ?? string.Empty;
            }
            catch (OperationCanceledException)
            {
                Log.Message("[LLMService] Request was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMService] Request failed: {ex.Message}");
                throw;
            }
        }

        public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default) where T : class
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("RimAI Framework is not initialized");
            }

            try
            {
                // 简化实现，直接调用基础API然后反序列化
                var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
                if (string.IsNullOrEmpty(response))
                {
                    return null;
                }

                // 尝试反序列化JSON响应
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response);
            }
            catch (OperationCanceledException)
            {
                Log.Message("[LLMService] JSON request was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMService] JSON request failed: {ex.Message}");
                return null;
            }
        }

        public async Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("RimAI Framework is not initialized");
            }

            if (!IsStreamingAvailable)
            {
                Log.Warning("[LLMService] Streaming is not available, falling back to standard request");
                var response = await SendMessageAsync(prompt, options, cancellationToken);
                onChunk?.Invoke(response);
                return;
            }

            try
            {
                await RimAIAPI.SendStreamingMessageAsync(prompt, onChunk, options, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Log.Message("[LLMService] Streaming request was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMService] Streaming request failed: {ex.Message}");
                throw;
            }
        }
    }
}
