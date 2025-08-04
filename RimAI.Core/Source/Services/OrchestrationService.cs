using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Contracts.Services;
using RimAI.Framework.Contracts;
using System.Linq;

#nullable enable
#pragma warning disable CS8425

namespace RimAI.Core.Services
{
    /// <summary>
    /// OrchestrationService 实现五步 Function Calling 工作流。
    /// * 目前实现单工具调用路径；多工具链未来可扩展。
    /// </summary>
    public class OrchestrationService : IOrchestrationService
    {
        private readonly ILLMService _llm;
        private readonly IPromptFactoryService _promptFactory;
        private readonly IToolRegistryService _toolRegistry;
        private readonly IHistoryService _history;

        public OrchestrationService(ILLMService llm,
                                     IPromptFactoryService promptFactory,
                                     IToolRegistryService toolRegistry,
                                     IHistoryService history)
        {
            _llm = llm;
            _promptFactory = promptFactory;
            _toolRegistry = toolRegistry;
            _history = history;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string userInput, string personaSystemPrompt, IEnumerable<string>? participants = null, CancellationToken ct = default)
        {
            // 1. 组装初始 Prompt
            var request = await _promptFactory.BuildPromptAsync(userInput, personaSystemPrompt, participants);

            // 2. 获取工具列表 & 让 LLM 决策
            var toolSchemas = _toolRegistry.GetAllToolSchemas();
            UnifiedChatResponse? decisionResp = null;
            if (toolSchemas.Any())
            {
                var decisionResult = await _llm.SendChatWithToolsAsync(request.Messages, toolSchemas);
                if (decisionResult.IsSuccess)
                {
                    decisionResp = decisionResult.Value;
                }
                else
                {
                    // 若失败，直接走常规回答
                    await foreach (var chunk in _llm.StreamResponseAsync(request).WithCancellation(ct))
                    {
                        yield return chunk;
                    }
                    yield break;
                }
            }

            // 3. 判断是否需要工具调用
            if (decisionResp != null && decisionResp.FinishReason == "tool_calls" && decisionResp.Message.ToolCalls?.Any() == true)
            {
                var toolCall = decisionResp.Message.ToolCalls.First();
                var argsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(toolCall.Arguments);

                // 3.1 安全执行工具
                object toolResult;
                try
                {
                    toolResult = await _toolRegistry.ExecuteToolAsync(toolCall.FunctionName, argsDict);
                }
                catch (System.Exception ex)
                {
                    // 工具失败 -> 智能反馈路径：将错误信息注入
                    toolResult = new { error = ex.Message };
                }

                // 3.2 重新组装 Prompt 注入结果
                var followMessages = new List<ChatMessage>(request.Messages)
                {
                    new ChatMessage
                    {
                        Role = "system",
                        Content = $"工具 {toolCall.FunctionName} 执行结果：{JsonConvert.SerializeObject(toolResult)}"
                    }
                };
                var followRequest = new UnifiedChatRequest { Messages = followMessages };

                // 4. 流式返回最终答复
                var sb = new StringBuilder();
                await foreach (var chunk in _llm.StreamResponseAsync(followRequest).WithCancellation(ct))
                {
                    if (chunk.IsSuccess && chunk.Value.ContentDelta != null)
                    {
                        sb.Append(chunk.Value.ContentDelta);
                    }
                    yield return chunk;
                }

                // 5. 记录历史（最终结果唯一性）
                if (participants != null)
                {
                    await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "user", Content = userInput });
                    await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "assistant", Content = sb.ToString() });
                }
            }
            else // 无工具调用，直接流式回答
            {
                var sb = new StringBuilder();
                await foreach (var chunk in _llm.StreamResponseAsync(request).WithCancellation(ct))
                {
                    if (chunk.IsSuccess && chunk.Value.ContentDelta != null)
                        sb.Append(chunk.Value.ContentDelta);
                    yield return chunk;
                }
                if (participants != null)
                {
                    await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "user", Content = userInput });
                    await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "assistant", Content = sb.ToString() });
                }
            }
        }
    }
}
