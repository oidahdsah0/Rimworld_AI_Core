using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;

#nullable enable
using RimAI.Framework.Contracts;
using System.Linq;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 简化版 PromptFactory，示例聚合世界刻数上下文。
    /// </summary>
    public class PromptFactoryService : IPromptFactoryService
    {
        private readonly IWorldDataService _worldDataService;
        private readonly IHistoryService _historyService;

        public PromptFactoryService(IWorldDataService worldDataService, IHistoryService historyService)
        {
            _worldDataService = worldDataService;
            _historyService = historyService;
        }

        public async Task<UnifiedChatRequest> BuildPromptAsync(string userInput, string personaSystemPrompt, IEnumerable<string>? participants = null)
        {
            var tick = await _worldDataService.GetCurrentGameTickAsync();

            var messages = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = personaSystemPrompt },
                new ChatMessage { Role = "system", Content = $"当前游戏刻数: {tick}." }
            };

            // 注入历史上下文
            if (participants != null)
            {
                var hist = await _historyService.GetHistoryAsync(participants, 20);
                foreach (var entry in hist.Mainline.Concat(hist.Background))
                {
                    messages.Add(new ChatMessage { Role = entry.Role, Content = entry.Content });
                }
            }

            messages.Add(new ChatMessage { Role = "user", Content = userInput });

            return new UnifiedChatRequest { Messages = messages };
        }
    }
}