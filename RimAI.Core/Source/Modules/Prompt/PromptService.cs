using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Orchestration;
using RimAI.Core.Modules.Prompting;
using RimAI.Core.Settings;
using CoreSvc = RimAI.Core.Infrastructure.CoreServices;
using RimAI.Core.Contracts.Eventing;
using Newtonsoft.Json;

namespace RimAI.Core.Modules.Prompt
{
    /// <summary>
    /// 统一提示词服务：封装模板解析（PromptTemplateService）与拼装（PromptComposer），
    /// 暴露唯一 Compose 入口，用于生成 system 提示并发出审计事件。
    /// </summary>
    internal sealed class PromptService : IPromptService
    {
        private readonly IConfigurationService _config;
        private readonly IPromptTemplateService _templates;
        private readonly IPromptComposer _composer;

        public PromptService(IConfigurationService config, IPromptTemplateService templates, IPromptComposer composer)
        {
            _config = config;
            _templates = templates;
            _composer = composer;
        }

        public string ResolveLocale() => _templates.ResolveLocale();

        public Task<string> ComposeSystemPromptAsync(PromptAssemblyInput input, CancellationToken ct = default)
        {
            input ??= new PromptAssemblyInput();
            var cfg = _config?.Current;
            var histCfg = cfg?.History ?? new HistoryConfig();
            var localeToUse = input.Locale ?? _templates.ResolveLocale();

            var templateKey = input.Mode == PromptMode.Command
                ? (cfg?.Prompt?.TemplateCommandKey ?? "command")
                : (input.Mode == PromptMode.Stage ? (cfg?.Prompt?.TemplateChatKey ?? "chat") : (cfg?.Prompt?.TemplateChatKey ?? "chat"));

            _composer.Begin(templateKey, localeToUse);

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
            if (!string.IsNullOrWhiteSpace(input.PersonaSystemPrompt)) _composer.Add("persona", input.PersonaSystemPrompt);
            if (input.BiographyParagraphs?.Count > 0) _composer.AddRange("biography", input.BiographyParagraphs);
            if (!string.IsNullOrWhiteSpace(input.FixedPromptOverride)) _composer.Add("fixed_prompts", input.FixedPromptOverride);
            if (input.RecapSegments?.Count > 0) _composer.AddRange("recap", input.RecapSegments);
            if (input.HistorySnippets?.Count > 0) _composer.AddRange("recent_history", input.HistorySnippets);
            if (input.WorldFacts?.Count > 0) _composer.AddRange("world", input.WorldFacts);
            if (input.StageHistory?.Count > 0) _composer.AddRange("stage_history", input.StageHistory);
            if (input.ToolResults?.Count > 0) _composer.AddRange("tool_results", input.ToolResults);
            if (input.Extras?.Count > 0) _composer.AddRange("extras", input.Extras);

            var maxChars = Math.Max(1000, input.MaxPromptChars ?? histCfg.MaxPromptChars);
            var output = _composer.Build(maxChars, out var audit);
            try
            {
                CoreSvc.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                {
                    Source = nameof(PromptService),
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


