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

        public bool IsStreamingAvailable => RimAIAPI.IsStreamingEnabled;
        public bool IsInitialized => RimAIAPI.IsInitialized;

        private LLMService() { }

        public async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("RimAI Framework is not initialized");
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

        public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default) where T : class
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("RimAI Framework is not initialized");
            }

            var jsonService = RimAIAPI.GetJsonService();
            if (jsonService == null)
            {
                throw new InvalidOperationException("JSON service is not available");
            }

            try
            {
                var response = await jsonService.SendJsonRequestAsync<T>(prompt, options, cancellationToken);
                
                if (response.Success)
                {
                    return response.Data;
                }
                else
                {
                    Log.Warning($"[LLMService] JSON request returned no data: {response.ErrorMessage}");
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message("[LLMService] JSON request was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[LLMService] JSON request failed: {ex.Message}");
                throw;
            }
        }

        public async Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMOptions options = null, CancellationToken cancellationToken = default)
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

        /// <summary>
        /// 测试连接状态
        /// </summary>
        public async Task<(bool success, string message)> TestConnectionAsync()
        {
            if (!IsInitialized)
            {
                return (false, "Framework not initialized");
            }

            try
            {
                return await RimAIAPI.TestConnectionAsync();
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// 获取当前设置信息
        /// </summary>
        public string GetCurrentSettings()
        {
            if (!IsInitialized)
            {
                return "Framework not initialized";
            }

            var settings = RimAIAPI.CurrentSettings;
            if (settings == null)
            {
                return "No settings available";
            }

            return $"Provider: {settings.ProviderType}, Streaming: {IsStreamingAvailable}";
        }

        /// <summary>
        /// 创建带有默认安全规则的选项
        /// </summary>
        public LLMOptions CreateSafeOptions(float temperature = 0.7f, bool forceStreaming = false)
        {
            if (forceStreaming && IsStreamingAvailable)
            {
                return RimAIAPI.Options.Streaming(temperature: temperature);
            }
            else if (temperature != 0.7f)
            {
                return new LLMOptions { Temperature = temperature };
            }
            
            return null; // 使用默认设置
        }
    }
}
