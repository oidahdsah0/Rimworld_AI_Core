using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Modules.World;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.History;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Settings;
using InfraConfig = RimAI.Core.Infrastructure.Configuration.IConfigurationService;
using CoreSvc = RimAI.Core.Infrastructure.CoreServices;
using RimAI.Core.Contracts.Eventing;
using Newtonsoft.Json;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 提示词组装服务（输入驱动）。统一基于模板与 Composer 组装 system 提示。
    /// </summary>
    internal sealed class PromptAssemblyService : IPromptAssemblyService
    {
        private readonly InfraConfig _config;
        private readonly RimAI.Core.Modules.Prompting.IPromptComposer _composer;
        private readonly RimAI.Core.Modules.Prompting.IPromptTemplateService _templates;

        public PromptAssemblyService(
            InfraConfig config,
            RimAI.Core.Modules.Prompting.IPromptComposer composer,
            RimAI.Core.Modules.Prompting.IPromptTemplateService templates)
        {
            _config = config;
            _composer = composer;
            _templates = templates;
        }
        public Task<string> ComposeSystemPromptAsync(PromptAssemblyInput input, CancellationToken ct = default)
        {
            // 守卫
            input ??= new PromptAssemblyInput();
            var cfg = _config?.Current;
            var histCfg = cfg?.History ?? new HistoryConfig();
            var localeToUse = input.Locale ?? _templates.ResolveLocale();

            // 选择模板
            var templateKey = input.Mode == PromptMode.Command
                ? (cfg?.Prompt?.TemplateCommandKey ?? "command")
                : (input.Mode == PromptMode.Stage ? (cfg?.Prompt?.TemplateChatKey ?? "chat") : (cfg?.Prompt?.TemplateChatKey ?? "chat"));

            _composer.Begin(templateKey, localeToUse);

            // 依序注入段
            if (input.Beliefs != null)
            {
                var b = input.Beliefs;
                var parts = new List<string>(4);
                if (!string.IsNullOrWhiteSpace(b.Worldview)) parts.Add("- 世界观: " + b.Worldview);
                if (!string.IsNullOrWhiteSpace(b.Values)) parts.Add("- 价值观: " + b.Values);
                if (!string.IsNullOrWhiteSpace(b.CodeOfConduct)) parts.Add("- 行为准则: " + b.CodeOfConduct);
                if (!string.IsNullOrWhiteSpace(b.TraitsText)) parts.Add("- 人格特质: " + b.TraitsText);
                if (parts.Count > 0) _composer.Add("persona", string.Join("\n", parts));
            }
            if (!string.IsNullOrWhiteSpace(input.PersonaSystemPrompt))
                _composer.Add("persona", input.PersonaSystemPrompt);

            if (input.BiographyParagraphs != null && input.BiographyParagraphs.Count > 0)
                _composer.AddRange("biography", input.BiographyParagraphs);

            if (!string.IsNullOrWhiteSpace(input.FixedPromptOverride))
                _composer.Add("fixed_prompts", input.FixedPromptOverride);

            if (input.RecapSegments != null && input.RecapSegments.Count > 0)
                _composer.AddRange("recap", input.RecapSegments);

            if (input.HistorySnippets != null && input.HistorySnippets.Count > 0)
                _composer.AddRange("recent_history", input.HistorySnippets);

            if (input.WorldFacts != null && input.WorldFacts.Count > 0)
                _composer.AddRange("world", input.WorldFacts);

            if (input.StageHistory != null && input.StageHistory.Count > 0)
                _composer.AddRange("stage_history", input.StageHistory);

            if (input.ToolResults != null && input.ToolResults.Count > 0)
                _composer.AddRange("tool_results", input.ToolResults);

            if (input.Extras != null && input.Extras.Count > 0)
                _composer.AddRange("extras", input.Extras);

            var maxChars = Math.Max(1000, input.MaxPromptChars ?? histCfg.MaxPromptChars);
            var output = _composer.Build(maxChars, out var audit);
            try
            {
                CoreSvc.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                {
                    Source = nameof(PromptAssemblyService),
                    Stage = "PromptAudit",
                    Message = templateKey,
                    PayloadJson = JsonConvert.SerializeObject(audit)
                });
            }
            catch { }
            return Task.FromResult(output ?? string.Empty);
        }
    }
}


