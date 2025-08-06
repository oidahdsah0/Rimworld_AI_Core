using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Modules.LLM;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// P5 阶段 OrchestrationService 完整五步最小实现。
    /// </summary>
    internal sealed class OrchestrationService : IOrchestrationService
    {
        private readonly ILLMService _llm;
        private readonly IToolRegistryService _tools;

        public OrchestrationService(ILLMService llm, IToolRegistryService tools)
        {
            _llm = llm;
            _tools = tools;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string query, string personaSystemPrompt = "")
        {
            // Step 0: 构造 tools definition 列表供 LLM 决策
            var toolSchemas = _tools.GetAllToolSchemas();
            var toolDefinitions = toolSchemas.Select(s => new ToolDefinition
            {
                Type = "function",
                Function = new JObject
                {
                    ["name"] = s.Name,
                    ["parameters"] = JObject.Parse(string.IsNullOrWhiteSpace(s.Arguments) ? "{}" : s.Arguments)
                }
            }).ToList();

            // Step 1: 发送用户问题 + tools 给 LLM 决策
            var initMessages = BuildBaseMessages(personaSystemPrompt, query);
            var initReq = new UnifiedChatRequest
            {
                Stream = false,
                Tools  = toolDefinitions,
                Messages = initMessages
            };

            var decisionRes = await _llm.GetResponseAsync(initReq);
            if (!decisionRes.IsSuccess)
            {
                yield return Result<UnifiedChatChunk>.Failure(decisionRes.Error);
                yield break;
            }

            var call = decisionRes.Value.Message.ToolCalls?.FirstOrDefault();
            if (call == null || string.IsNullOrWhiteSpace(call.Function?.Name))
            {
                yield return Result<UnifiedChatChunk>.Failure("LLM 未返回有效的 tool_calls。");
                yield break;
            }

            Dictionary<string, object> argsDict = new();
            string parseError = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(call.Function.Arguments) && call.Function.Arguments != "{}")
                {
                    var jObj = JObject.Parse(call.Function.Arguments);
                    argsDict = jObj.ToObject<Dictionary<string, object>>();
                }
            }
            catch (Exception ex)
            {
                parseError = $"解析 tool 参数失败: {ex.Message}";
            }

            // 在 try-catch 块外处理解析错误
            if (parseError != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(parseError);
                yield break;
            }

            object toolResult = null;
            IAsyncEnumerable<Result<UnifiedChatChunk>> errorStream = null;
            
            try
            {
                toolResult = await _tools.ExecuteToolAsync(call.Function.Name, argsDict);
            }
            catch (Exception ex)
            {
                // Step 4a: 工具执行失败，构造友好提示词
                var errMessages = BuildBaseMessages(personaSystemPrompt, query);
                errMessages.Add(new ChatMessage { Role = "assistant", Content = $"调用工具 {call.Function.Name} 失败: {ex.Message}" });
                var errReq = new UnifiedChatRequest { Stream = true, Messages = errMessages };
                errorStream = _llm.StreamResponseAsync(errReq);
            }

            // 在 try-catch 块外处理错误流
            if (errorStream != null)
            {
                await foreach (var chunk in errorStream)
                    yield return chunk;
                yield break;
            }

            // Step 3: 构造跟进请求，将工具结果交给 LLM 转自然语言
            var followMessages = BuildBaseMessages(personaSystemPrompt, query);
            followMessages.Add(new ChatMessage { Role = "assistant", ToolCalls = new List<ToolCall> { call } });
            followMessages.Add(new ChatMessage { Role = "tool", ToolCallId = call.Id, Content = JsonConvert.SerializeObject(toolResult, Formatting.None) });

            var followReq = new UnifiedChatRequest
            {
                Stream = true,
                Tools = toolDefinitions,
                Messages = followMessages
            };

            await foreach (var chunk in _llm.StreamResponseAsync(followReq))
                yield return chunk;
        }

        private static List<ChatMessage> BuildBaseMessages(string personaPrompt, string userQuery)
        {
            var msgs = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(personaPrompt))
            {
                msgs.Add(new ChatMessage { Role = "system", Content = personaPrompt });
            }
            msgs.Add(new ChatMessage { Role = "user", Content = userQuery });
            return msgs;
        }
    }
}