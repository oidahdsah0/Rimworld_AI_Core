using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;
using UnityEngine;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class LlmBatchTestButton : IDebugPanelButton
    {
        public string Label => "LLM Batch Test";

        public void Execute(DebugPanelContext ctx)
        {
            var prompts = new List<string>
            {
                "Hello!",
                "你好！",
                "¡Hola!",
                "Bonjour!",
                "こんにちは！"
            };
            var requests = new List<UnifiedChatRequest>();
            foreach (var p in prompts)
            {
                var r = new UnifiedChatRequest
                {
                    Stream = false,
                    Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = p } }
                };
                r.ConversationId = $"debug:batch:mode:chat:{ctx.ComputeShortHash(p)}";
                requests.Add(r);
            }

            Task.Run(async () =>
            {
                try
                {
                    ctx.AppendOutput("Batch requesting...");
                    var results = await RimAI.Framework.API.RimAIApi.GetCompletionsAsync(requests);
                    for (int i = 0; i < results.Count; i++)
                    {
                        var res = results[i];
                        if (res.IsSuccess)
                            ctx.AppendOutput($"[{i}] {prompts[i]} -> {res.Value.Message.Content}");
                        else
                            ctx.AppendOutput($"[{i}] {prompts[i]} -> Error: {res.Error}");
                    }
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"LLM Batch Test failed: {ex.Message}");
                }
            });
        }
    }
}


