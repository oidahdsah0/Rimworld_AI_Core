using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Source.Modules.LLM
{
	internal sealed class LLMService : ILLMService
	{
		private readonly IConfigurationService _configurationService;
		// P2: 由内部 CoreConfig.LLM 提供的运行时参数（默认值见 CoreConfig）
		private int _defaultTimeoutMs = 15000;
		private int _hbTimeoutMs = 15000;
		private int _logEveryN = 20;
		private int _retryMaxAttempts = 3;
		private int _retryBaseDelayMs = 400;
		private int _circuitWindowMs = 60000;
		private int _circuitCooldownMs = 60000;
		private double _circuitErrorThreshold = 0.5;

		public LLMService(IConfigurationService configurationService)
		{
			_configurationService = configurationService;
			// 加载一次配置，并监听热重载
			LoadLlmConfig(_configurationService);
			_configurationService.OnConfigurationChanged += _ => LoadLlmConfig(_configurationService);
		}

		private void LoadLlmConfig(IConfigurationService configurationService)
		{
			// 仅 Core 内部：向下转型以读取内部 CoreConfig.LLM 节点
			var impl = configurationService as Source.Infrastructure.Configuration.ConfigurationService;
			var llm = impl?.GetLlmConfig();
			if (llm == null) return;
			_defaultTimeoutMs = llm.DefaultTimeoutMs;
			_hbTimeoutMs = llm.Stream?.HeartbeatTimeoutMs ?? _hbTimeoutMs;
			_logEveryN = llm.Stream?.LogEveryNChunks ?? _logEveryN;
			_retryMaxAttempts = llm.Retry?.MaxAttempts ?? _retryMaxAttempts;
			_retryBaseDelayMs = llm.Retry?.BaseDelayMs ?? _retryBaseDelayMs;
			_circuitWindowMs = llm.CircuitBreaker?.WindowMs ?? _circuitWindowMs;
			_circuitCooldownMs = llm.CircuitBreaker?.CooldownMs ?? _circuitCooldownMs;
			_circuitErrorThreshold = llm.CircuitBreaker?.ErrorThreshold ?? _circuitErrorThreshold;
		}

		public Task<Result<UnifiedChatResponse>> GetResponseAsync(string conversationId, string systemPrompt, string userText, CancellationToken cancellationToken = default)
		{
			var req = new UnifiedChatRequest
			{
				ConversationId = conversationId,
				Messages = new List<ChatMessage>
				{
					new ChatMessage { Role = "system", Content = systemPrompt ?? string.Empty },
					new ChatMessage { Role = "user", Content = userText ?? string.Empty }
				}
			};
			return GetResponseAsync(req, cancellationToken);
		}

        public Task<Result<UnifiedChatResponse>> GetResponseAsync(string conversationId, string systemPrompt, string userText, bool jsonMode, CancellationToken cancellationToken = default)
		{
			var req = new UnifiedChatRequest
			{
				ConversationId = conversationId,
				Messages = new List<ChatMessage>
				{
					new ChatMessage { Role = "system", Content = systemPrompt ?? string.Empty },
					new ChatMessage { Role = "user", Content = userText ?? string.Empty }
				},
                ForceJsonOutput = jsonMode,
                Stream = false
			};
			return GetResponseAsync(req, cancellationToken);
		}

		public Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
		{
			if (request == null || string.IsNullOrWhiteSpace(request.ConversationId) || request.Messages == null || request.Messages.Count == 0)
			{
				return Task.FromResult(Result<UnifiedChatResponse>.Failure("Invalid request: ConversationId/Messages required."));
			}
			// 明确后台非流式
			request.Stream = false;
			return CallWithRetryAsync(request.ConversationId, ct => RimAIApi.GetCompletionAsync(request, ct), cancellationToken);
		}

		public Task<Result<UnifiedChatResponse>> GetResponseAsync(string conversationId, string systemPrompt, string userText, IReadOnlyList<string> toolsJson, bool jsonMode, CancellationToken cancellationToken = default)
		{
			// 优先路径：使用 Framework 的便捷方法，显式传入 ToolDefinition 列表，提升兼容性
			try
			{
				var messages = new List<ChatMessage>
				{
					new ChatMessage { Role = "system", Content = systemPrompt ?? string.Empty },
					new ChatMessage { Role = "user", Content = userText ?? string.Empty }
				};
				var toolList = new List<RimAI.Framework.Contracts.ToolDefinition>();
				if (toolsJson != null)
				{
					foreach (var s in toolsJson)
					{
						var tdef = TryConvertToolJsonToDefinition(s);
						if (tdef != null) toolList.Add(tdef);
					}
				}
				if (toolList.Count > 0)
				{
					System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P2.LLM] Using GetCompletionWithToolsAsync tools={toolList.Count}");
					return CallWithRetryAsync(conversationId, ct => RimAIApi.GetCompletionWithToolsAsync(messages, toolList, conversationId, ct), cancellationToken);
				}
			}
			catch { }

			// 回退路径：无可用工具时，构造标准非流式请求（不做旧版字段注入）
			System.Diagnostics.Debug.WriteLine("[RimAI.Core][P2.LLM] No valid tools provided; falling back to plain completion.");
			var req = new UnifiedChatRequest
			{
				ConversationId = conversationId,
				Messages = new List<ChatMessage>
				{
					new ChatMessage { Role = "system", Content = systemPrompt ?? string.Empty },
					new ChatMessage { Role = "user", Content = userText ?? string.Empty }
				},
				ForceJsonOutput = jsonMode,
				Stream = false
			};
			return GetResponseAsync(req, cancellationToken);
		}

		public Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, System.Collections.Generic.IReadOnlyList<string> toolsJson, bool jsonMode, CancellationToken cancellationToken = default)
		{
			if (request == null)
			{
				return Task.FromResult(Result<UnifiedChatResponse>.Failure("Invalid request"));
			}
			// 确保非流式与 JSON 模式按需开启
			request.Stream = false;
			request.ForceJsonOutput = jsonMode;

			var toolList = new List<RimAI.Framework.Contracts.ToolDefinition>();
			try
			{
				if (toolsJson != null)
				{
					foreach (var s in toolsJson)
					{
						var tdef = TryConvertToolJsonToDefinition(s);
						if (tdef != null) toolList.Add(tdef);
					}
				}
			}
			catch { }

			if (toolList.Count > 0 && request.Messages != null)
			{
				System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P2.LLM] Using GetCompletionWithToolsAsync tools={toolList.Count}");
				return CallWithRetryAsync(request.ConversationId, ct => RimAIApi.GetCompletionWithToolsAsync(request.Messages, toolList, request.ConversationId, ct), cancellationToken);
			}

			return GetResponseAsync(request, cancellationToken);
		}

		// 统一且稳健的工具 JSON → ToolDefinition 转换器
		private static RimAI.Framework.Contracts.ToolDefinition TryConvertToolJsonToDefinition(string json)
		{
			if (string.IsNullOrWhiteSpace(json)) return null;
			try
			{
				var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
				Newtonsoft.Json.Linq.JToken fn = jo["function"];
				string name = null;
				string desc = null;
				Newtonsoft.Json.Linq.JToken parameters = null;
				if (fn != null && fn.Type == Newtonsoft.Json.Linq.JTokenType.Object)
				{
					name = fn.Value<string>("name") ?? fn.Value<string>("Name");
					desc = fn.Value<string>("description") ?? fn.Value<string>("Description");
					parameters = fn["parameters"] ?? fn["Parameters"] ?? fn["ParametersSchema"];
				}
				else
				{
					name = jo.Value<string>("name") ?? jo.Value<string>("Name");
					desc = jo.Value<string>("description") ?? jo.Value<string>("Description");
					parameters = jo["parameters"] ?? jo["Parameters"] ?? jo["ParametersSchema"];
				}
				if (string.IsNullOrWhiteSpace(name)) return null;
				// 标准化 parameters
				if (parameters == null || parameters.Type == Newtonsoft.Json.Linq.JTokenType.Null)
				{
					parameters = new Newtonsoft.Json.Linq.JObject
					{
						["type"] = "object",
						["properties"] = new Newtonsoft.Json.Linq.JObject(),
						["required"] = new Newtonsoft.Json.Linq.JArray()
					};
				}
				var fnObj = new Newtonsoft.Json.Linq.JObject
				{
					["name"] = name,
					["parameters"] = parameters
				};
				if (!string.IsNullOrWhiteSpace(desc)) fnObj["description"] = desc;
				return new RimAI.Framework.Contracts.ToolDefinition { Type = "function", Function = fnObj };
			}
			catch { return null; }
		}

		public Task<Result<UnifiedEmbeddingResponse>> GetEmbeddingsAsync(string input, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				return Task.FromResult(Result<UnifiedEmbeddingResponse>.Failure("Input required"));
			}
			var req = new UnifiedEmbeddingRequest { Inputs = new List<string> { input } };
			return GetEmbeddingsAsync(req, cancellationToken);
		}

		public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(string conversationId, string systemPrompt, string userText, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var req = new UnifiedChatRequest
			{
				ConversationId = conversationId,
				Messages = new List<ChatMessage>
				{
					new ChatMessage { Role = "system", Content = systemPrompt ?? string.Empty },
					new ChatMessage { Role = "user", Content = userText ?? string.Empty }
				},
				Stream = true
			};
			await foreach (var r in StreamResponseAsync(req, cancellationToken).ConfigureAwait(false))
			{
				yield return r;
			}
		}

		private Task<Result<UnifiedChatResponse>> CallWithRetryAsync(string circuitKeySuffix, Func<CancellationToken, Task<Result<UnifiedChatResponse>>> action, CancellationToken cancellationToken)
		{
			using var cts = CreateLinkedCtsWithDefaultTimeout(cancellationToken, _defaultTimeoutMs);
			var circuitKey = "chat:" + (circuitKeySuffix ?? "-");
			if (!LlmPolicies.IsAllowedByCircuit(circuitKey, _circuitWindowMs, _circuitCooldownMs, _circuitErrorThreshold))
			{
				return Task.FromResult(Result<UnifiedChatResponse>.Failure("CircuitOpen"));
			}
			return LlmPolicies.ExecuteWithRetryAsync<Result<UnifiedChatResponse>>(async ct =>
			{
				var result = await action(ct).ConfigureAwait(false);
				LlmPolicies.RecordResult(circuitKey, result.IsSuccess);
				return result;
			}, maxAttempts: _retryMaxAttempts, baseDelayMs: _retryBaseDelayMs, isTransientFailure: r => r.IsFailure, cancellationToken: cts.Token);
		}


		public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			if (request == null || string.IsNullOrWhiteSpace(request.ConversationId) || request.Messages == null || request.Messages.Count == 0)
			{
				yield return Result<UnifiedChatChunk>.Failure("Invalid request: ConversationId/Messages required.");
				yield break;
			}
			// 明确标识 Streaming，若底层 Provider 依据该标志优化首包行为
			request.Stream = true;
			var circuitKey = "stream:" + (request.ConversationId ?? "-");
			if (!LlmPolicies.IsAllowedByCircuit(circuitKey, _circuitWindowMs, _circuitCooldownMs, _circuitErrorThreshold))
			{
				yield return Result<UnifiedChatChunk>.Failure("CircuitOpen");
				yield break;
			}

			var hbTimeoutMs = _hbTimeoutMs;
			var logEveryN = _logEveryN;
			var chunks = 0;
			for (int attempt = 1; attempt <= _retryMaxAttempts; attempt++)
			{
				DateTime start = DateTime.UtcNow;
				DateTime last = start;
				bool firstChunkReceived = false;
				bool wasCancelled = false;
				bool failBeforeFirstChunk = false;
				Result<UnifiedChatChunk> earlyError = null;
				// Watchdog：对“首包”与“分片间”统一用心跳超时控制，避免 UI 长时间卡死。
				// 使用与调用方 token 链接的 CTS，并在每次收到分片后重新设定 CancelAfter。
				using var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				if (hbTimeoutMs > 0) watchdogCts.CancelAfter(hbTimeoutMs);
				var stream = RimAIApi.StreamCompletionAsync(request, watchdogCts.Token);
				await using var enumerator = stream.GetAsyncEnumerator(watchdogCts.Token);
				// 移除未使用的首包 watchdog 以保持代码简洁
				while (true)
				{
					bool hasNext;
					try
					{
						hasNext = await enumerator.MoveNextAsync();
					}
					catch (OperationCanceledException)
					{
						wasCancelled = true;
						break;
					}

					if (!hasNext)
					{
						break;
					}

					var result = enumerator.Current;
					if (!result.IsSuccess)
					{
						if (!firstChunkReceived && attempt < _retryMaxAttempts)
						{
							failBeforeFirstChunk = true;
							break;
						}
						LlmPolicies.RecordResult(circuitKey, false);
						earlyError = result;
						break;
					}

					// 成功分片
					firstChunkReceived = true;
					if ((DateTime.UtcNow - last).TotalMilliseconds > hbTimeoutMs)
					{
						LlmPolicies.RecordResult(circuitKey, false);
						earlyError = Result<UnifiedChatChunk>.Failure("HeartbeatTimeout");
						break;
					}
					chunks++;
					if (logEveryN > 0 && (chunks % logEveryN == 0))
					{
						// Keep frequent progress logs out of in-game console to avoid spam; remain as debug
						System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P2.LLM] stream progress conv={LlmLogging.HashConversationId(request.ConversationId)} chunks={chunks}");
					}
					last = DateTime.UtcNow;
					// 重置心跳计时（下一分片超时）
					try { if (hbTimeoutMs > 0) watchdogCts.CancelAfter(hbTimeoutMs); } catch { }
					yield return result;
					if (result.Value != null && result.Value.FinishReason != null)
					{
						LlmPolicies.RecordResult(circuitKey, true);
					}
				}

				// 统一在循环外根据状态做 yield/重试/退出，避免在 catch 中 yield
				if (wasCancelled)
				{
					// 用户主动取消：不计为失败，直接返回取消信号
					yield return Result<UnifiedChatChunk>.Failure("TimeoutOrCancelled");
					yield break;
				}

				if (earlyError != null)
				{
					yield return earlyError;
					yield break;
				}

				// 若本次未收到任何分片且仍可重试，则退避后重试
				if (failBeforeFirstChunk && attempt < _retryMaxAttempts)
				{
					var delay = (int)(_retryBaseDelayMs * Math.Pow(2, attempt - 1));
					try { await Task.Delay(delay, cancellationToken).ConfigureAwait(false); } catch { }
					continue;
				}

				break; // 正常结束流
			}
		}

		public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(string conversationId, string systemPrompt, System.Collections.Generic.IReadOnlyList<(string role, string content)> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
		{
			var req = new UnifiedChatRequest
			{
				ConversationId = conversationId,
				Messages = new List<ChatMessage>()
			};
			if (!string.IsNullOrEmpty(systemPrompt)) req.Messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
			if (messages != null)
			{
				foreach (var (role, content) in messages)
				{
					req.Messages.Add(new ChatMessage { Role = role, Content = content ?? string.Empty });
				}
			}
			req.Stream = true;
			await foreach (var r in StreamResponseAsync(req, cancellationToken).ConfigureAwait(false))
			{
				yield return r;
			}
		}

		public async Task<List<Result<UnifiedChatResponse>>> GetResponsesAsync(List<UnifiedChatRequest> requests, CancellationToken cancellationToken = default)
		{
            if (requests == null || requests.Count == 0)
			{
                return new List<Result<UnifiedChatResponse>> { Result<UnifiedChatResponse>.Failure("Empty requests") };
			}
			using var cts = CreateLinkedCtsWithDefaultTimeout(cancellationToken, _defaultTimeoutMs);
			try
			{
				var maxConcurrent = _configurationService is Source.Infrastructure.Configuration.ConfigurationService impl && impl.GetLlmConfig()?.Batch?.MaxConcurrent > 0
					? impl.GetLlmConfig().Batch.MaxConcurrent
					: 4;
				var gate = new System.Threading.SemaphoreSlim(maxConcurrent);
				var tasks = requests.Select(async req =>
				{
					await gate.WaitAsync(cts.Token).ConfigureAwait(false);
					try { return await GetResponseAsync(req, cts.Token).ConfigureAwait(false); }
					finally { gate.Release(); }
				});
				return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P2.LLM] GetResponsesAsync failed: {ex.Message}");
				return requests.Select(_ => Result<UnifiedChatResponse>.Failure(ex.Message)).ToList();
			}
		}

		public async Task<Result<UnifiedEmbeddingResponse>> GetEmbeddingsAsync(UnifiedEmbeddingRequest request, CancellationToken cancellationToken = default)
		{
            if (request == null || request.Inputs == null || request.Inputs.Count == 0)
			{
                return Result<UnifiedEmbeddingResponse>.Failure("Invalid request: Inputs required.");
			}
			using var cts = CreateLinkedCtsWithDefaultTimeout(cancellationToken, _defaultTimeoutMs);
			var circuitKey = "embed:" + (request.Inputs?[0]?.GetHashCode().ToString() ?? "-");
			if (!LlmPolicies.IsAllowedByCircuit(circuitKey, _circuitWindowMs, _circuitCooldownMs, _circuitErrorThreshold))
			{
                return Result<UnifiedEmbeddingResponse>.Failure("CircuitOpen");
			}
			try
			{
				var result = await LlmPolicies.ExecuteWithRetryAsync<Result<UnifiedEmbeddingResponse>>(async ct =>
				{
					return await RimAIApi.GetEmbeddingsAsync(request, ct).ConfigureAwait(false);
				}, maxAttempts: _retryMaxAttempts, baseDelayMs: _retryBaseDelayMs, isTransientFailure: r => r.IsFailure, cancellationToken: cts.Token).ConfigureAwait(false);
				LlmPolicies.RecordResult(circuitKey, result.IsSuccess);
				return result;
			}
			catch (OperationCanceledException)
			{
				LlmPolicies.RecordResult(circuitKey, false);
                return Result<UnifiedEmbeddingResponse>.Failure("TimeoutOrCancelled");
			}
			catch (Exception ex)
			{
				LlmPolicies.RecordResult(circuitKey, false);
				System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P2.LLM] GetEmbeddingsAsync failed: {ex.Message}");
                return Result<UnifiedEmbeddingResponse>.Failure(ex.Message);
			}
		}

        public Task<Result<bool>> InvalidateConversationCacheAsync(string conversationId, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(conversationId))
			{
                return Task.FromResult(Result<bool>.Failure("ConversationId required"));
			}
			return RimAIApi.InvalidateConversationCacheAsync(conversationId, cancellationToken);
		}

		public bool IsEmbeddingEnabled()
		{
			try { return RimAIApi.IsEmbeddingEnabled(); } catch { return false; }
		}

		private static CancellationTokenSource CreateLinkedCtsWithDefaultTimeout(CancellationToken callerToken, int defaultTimeoutMs)
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
			cts.CancelAfter(defaultTimeoutMs > 0 ? defaultTimeoutMs : 15000);
			return cts;
		}
	}
}


