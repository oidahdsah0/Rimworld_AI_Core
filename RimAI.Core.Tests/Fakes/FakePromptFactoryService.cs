using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Tests.Fakes
{
    /// <summary>
    /// 返回最简 UnifiedChatRequest：system + user。
    /// </summary>
    internal class FakePromptFactoryService : IPromptFactoryService
    {
        public Task<UnifiedChatRequest> BuildPromptAsync(string userInput, string personaSystemPrompt, IEnumerable<string>? participants = null)
        {
            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = personaSystemPrompt },
                new ChatMessage { Role = "user", Content = userInput }
            };
            return Task.FromResult(new UnifiedChatRequest { Messages = messages });
        }
    }
}
