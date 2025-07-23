using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Core.Officers.Base;
using Verse;

namespace RimAI.Core.Officers
{
    public class Governor : OfficerBase
    {
        public override string Name => "总督";
        public override string Description => "殖民地的首席AI决策官，负责宏观战略和处理玩家的直接查询。";
        public override OfficerRole Role => OfficerRole.Governor;
        public override string IconPath => "UI/Icons/Governor";

        private readonly IColonyAnalyzer _analyzer;
        private readonly IPromptFactoryService _promptFactory;
        private readonly IHistoryService _historyService;

        public Governor()
        {
            _analyzer = CoreServices.Analyzer; // Corrected to Analyzer
            _promptFactory = CoreServices.PromptFactory;
            _historyService = CoreServices.History;
        }

        protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
        {
            var promptConfig = new PromptBuildConfig
            {
                CurrentParticipants = new List<string> { "Player", "Governor" },
                SystemPrompt = "You are the Governor, a wise and cautious leader for the colony, providing a general overview.",
                Scene = new SceneContext { Situation = "Reviewing the general status of the colony." },
                HistoryLimit = 5
            };
            
            var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
            
            // In a real scenario, this payload would be sent to LLMService
            var promptText = string.Join("\n", payload.Messages.Select(m => $"{m.Role} ({m.Name ?? "System"}): {m.Content}"));
            
            return await Task.FromResult(promptText);
        }

        public async Task<string> HandleUserQueryAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            var promptConfig = new PromptBuildConfig
            {
                CurrentParticipants = new List<string> { "Player", "Governor" },
                SystemPrompt = "You are the Governor, responding to a specific query from the player.",
                Scene = new SceneContext { Situation = $"The player asks: '{userQuery}'" },
                HistoryLimit = 10
            };
            
            var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
            payload.Messages.Add(new ChatMessage { Role = "user", Content = userQuery, Name = "Player" });

            var promptText = string.Join("\n", payload.Messages.Select(m => $"{m.Role} ({m.Name ?? "System"}): {m.Content}"));

            return await Task.FromResult(promptText);
        }
    }
}
