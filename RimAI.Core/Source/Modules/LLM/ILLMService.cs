using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
 

namespace RimAI.Core.Source.Modules.LLM
{
	/// <summary>
	/// Core 内部唯一的 LLM 网关接口。统一承载 Chat/Embedding/缓存失效。
	/// 后台/服务路径一律使用非流式；流式仅供 UI/Debug 使用。
	/// </summary>
	internal interface ILLMService
	{
		// 便捷重载 — 供 UI/Debug 使用，避免上层引用 Framework DTO
		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>> GetResponseAsync(
			string conversationId,
			string systemPrompt,
			string userText,
			CancellationToken cancellationToken = default);

		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>> GetResponseAsync(
			string conversationId,
			string systemPrompt,
			string userText,
			bool jsonMode,
			CancellationToken cancellationToken = default);

		IAsyncEnumerable<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatChunk>> StreamResponseAsync(
			string conversationId,
			string systemPrompt,
			string userText,
			CancellationToken cancellationToken = default);

		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedEmbeddingResponse>> GetEmbeddingsAsync(
			string input,
			CancellationToken cancellationToken = default);

		// Chat — 非流式
		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>> GetResponseAsync(
			RimAI.Framework.Contracts.UnifiedChatRequest request,
			CancellationToken cancellationToken = default);

		// Chat — 非流式，messages 数组 + tools 列表（新 API）
		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>> GetResponseAsync(
			RimAI.Framework.Contracts.UnifiedChatRequest request,
			System.Collections.Generic.IReadOnlyList<string> toolsJson,
			bool jsonMode,
			CancellationToken cancellationToken = default);

		// Chat — 非流式，附工具列表（隐藏 Framework 细节给上游）
		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>> GetResponseAsync(
			string conversationId,
			string systemPrompt,
			string userText,
			System.Collections.Generic.IReadOnlyList<string> toolsJson,
			bool jsonMode,
			CancellationToken cancellationToken = default);

		// Chat — 流式（仅 UI/Debug 面板使用）
		IAsyncEnumerable<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatChunk>> StreamResponseAsync(
			RimAI.Framework.Contracts.UnifiedChatRequest request,
			CancellationToken cancellationToken = default);

		// Chat — 流式（系统+多轮 messages 数组）
		IAsyncEnumerable<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatChunk>> StreamResponseAsync(
			string conversationId,
			string systemPrompt,
			System.Collections.Generic.IReadOnlyList<(string role, string content)> messages,
			CancellationToken cancellationToken = default);

		// Chat — 批量
		Task<System.Collections.Generic.List<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedChatResponse>>> GetResponsesAsync(
			System.Collections.Generic.List<RimAI.Framework.Contracts.UnifiedChatRequest> requests,
			CancellationToken cancellationToken = default);

		// Embedding — 非流式
		Task<RimAI.Framework.Contracts.Result<RimAI.Framework.Contracts.UnifiedEmbeddingResponse>> GetEmbeddingsAsync(
			RimAI.Framework.Contracts.UnifiedEmbeddingRequest request,
			CancellationToken cancellationToken = default);

		// 会话缓存失效（透传到 Framework）
		Task<RimAI.Framework.Contracts.Result<bool>> InvalidateConversationCacheAsync(
			string conversationId,
			CancellationToken cancellationToken = default);
	}
}


