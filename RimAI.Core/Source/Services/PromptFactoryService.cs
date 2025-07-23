using System.Text;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using Verse;
using RimWorld; // Import RimWorld namespace for GenDate and Quadrum

namespace RimAI.Core.Services
{
    public class PromptFactoryService : IPromptFactoryService
    {
        public async Task<PromptPayload> BuildStructuredPromptAsync(PromptBuildConfig config)
        {
            var payload = new PromptPayload();
            var historyService = CoreServices.History;
            var playerStableId = CoreServices.PlayerStableId;
            var promptBuilder = CoreServices.PromptBuilder; // 新增

            if (historyService == null || playerStableId == null || promptBuilder == null) // 新增
            {
                Log.Error("Core services (History, PlayerId, or PromptBuilder) not available. PromptFactory cannot continue."); // 新增
                return payload;
            }

            // 1. Build and Add System Prompt
            string systemPromptContent = null;
            if (!string.IsNullOrEmpty(config.SystemPromptTemplateId))
            {
                systemPromptContent = promptBuilder.BuildPrompt(config.SystemPromptTemplateId, config.TemplateContext);
            }
            else if (!string.IsNullOrEmpty(config.SystemPrompt))
            {
                systemPromptContent = config.SystemPrompt;
            }
            
            if (!string.IsNullOrEmpty(systemPromptContent))
            {
                payload.Messages.Add(new ChatMessage { Role = "system", Content = systemPromptContent });
            }

            // 2. Get Historical Context
            var historicalContext = historyService.GetHistoricalContextFor(config.CurrentParticipants, config.HistoryLimit);

            // 3. Assemble Ancillary History
            if (historicalContext.AncillaryHistory.Count > 0)
            {
                var ancillaryText = new StringBuilder();
                ancillaryText.AppendLine("[背景参考资料：以下是您和其他人共同参与的相关对话摘要]");

                foreach (var entry in historicalContext.AncillaryHistory)
                {
                    var timestamp = FormatTimestamp(entry.GameTicksTimestamp);
                    ancillaryText.AppendLine($"{timestamp} | {entry.ParticipantId}: {entry.Content}");
                }
                
                payload.Messages.Add(new ChatMessage { Role = "system", Content = ancillaryText.ToString() });
            }

            // 4. Assemble Primary History
            foreach (var entry in historicalContext.PrimaryHistory)
            {
                var role = entry.ParticipantId == playerStableId ? "user" : "assistant";
                var timestamp = FormatTimestamp(entry.GameTicksTimestamp);
                payload.Messages.Add(new ChatMessage { Role = role, Content = $"{timestamp} | {entry.Content}", Name = entry.ParticipantId });
            }
            
            // This is an async shell for now. In the future, it might involve async calls to other services.
            await Task.CompletedTask;

            return payload;
        }

        private string FormatTimestamp(long ticks)
        {
            // We need a map's longitude to calculate date parts correctly.
            // We'll try to get it from the current map. If not available, we can't format the date.
            var map = Find.CurrentMap;
            if (map == null)
            {
                return $"[时间: Ticks {ticks}]"; // Fallback if no map is available
            }
            float longitude = Find.WorldGrid.LongLatOf(map.Tile).x;

            int day = GenDate.DayOfSeason(ticks, longitude);
            Quadrum quadrum = GenDate.Quadrum(ticks, longitude);
            int year = GenDate.Year(ticks, longitude);
            int hour = GenDate.HourOfDay(ticks, longitude);

            return $"[时间: {year}年{quadrum.Label()}{day}日, {hour}时]";
        }
    }
} 