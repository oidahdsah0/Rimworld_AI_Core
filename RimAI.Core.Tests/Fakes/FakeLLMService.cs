using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using RimAI.Framework.Contracts;
using System.Runtime.CompilerServices;

namespace RimAI.Core.Tests.Fakes
{
    internal class FakeLLMService : ILLMService
    {
        private readonly bool _returnToolCall;
        private readonly string _streamText;
        public FakeLLMService(bool returnToolCall, string streamText = "OK")
        {
            _returnToolCall = returnToolCall;
            _streamText = streamText;
        }

        public Task<Result<UnifiedChatResponse>> SendChatAsync(UnifiedChatRequest request)
            => Task.FromResult(Result<UnifiedChatResponse>.Failure("not used"));

        public Task<Result<UnifiedChatResponse>> SendChatWithToolsAsync(List<ChatMessage> messages, List<ToolDefinition> tools)
        {
            if (_returnToolCall && tools.Any())
            {
                var tc = new ToolCall
                {
                    Id = "1",
                    FunctionName = tools.First().Function["name"].ToString(),
                    Arguments = "{\"text\":\"hello\"}"
                };
                var resp = new UnifiedChatResponse
                {
                    FinishReason = "tool_calls",
                    Message = new ChatMessage { ToolCalls = new List<ToolCall> { tc } }
                };
                return Task.FromResult(Result<UnifiedChatResponse>.Success(resp));
            }
            else
            {
                var resp = new UnifiedChatResponse { FinishReason = "stop", Message = new ChatMessage { Content = "no tool" } };
                return Task.FromResult(Result<UnifiedChatResponse>.Success(resp));
            }
        }

        public IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request)
        {
            return StreamImpl();
            async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamImpl(System.Threading.CancellationToken ct = default)
            {
                foreach (var ch in _streamText)
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = ch.ToString() });
                    await Task.Yield();
                }
                yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { FinishReason = "stop", ContentDelta = null });
            }
        }

        // 旧接口已移除
    }
}
