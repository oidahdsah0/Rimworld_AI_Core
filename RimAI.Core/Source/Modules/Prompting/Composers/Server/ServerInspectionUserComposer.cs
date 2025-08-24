using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    // Supplies the user payload for inspection scope using localization
    internal sealed class ServerInspectionUserComposer : IPromptComposer, IProvidesUserPayload
    {
        public PromptScope Scope => PromptScope.ServerInspection;
        public int Order => 1500; // order irrelevant for user payload
        public string Id => "server_inspection_user";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            // Provide a context block to carry the raw tool result, keeping system clean.
            var blocks = new List<ContextBlock>();
            try
            {
                var eb = ctx?.Request?.ExternalBlocks;
                if (eb != null && eb.Count > 0)
                {
                    var last = eb[eb.Count - 1];
                    if (last != null)
                    {
                        blocks.Add(new ContextBlock { Title = last.Title, Text = last.Text });
                    }
                }
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks });
        }

        public Task<string> BuildUserPayloadAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var locale = ctx?.Locale ?? (loc?.GetDefaultLocale() ?? "en");
                // Derive app/tool name from last external block title if available, else fallback
                string app = null;
                try
                {
                    var eb = ctx?.Request?.ExternalBlocks;
                    if (eb != null && eb.Count > 0)
                    {
                        var last = eb[eb.Count - 1];
                        var t = last?.Title;
                        if (!string.IsNullOrWhiteSpace(t)) app = t.Replace("议题：", string.Empty).Replace("Tool Result", string.Empty).Trim();
                    }
                }
                catch { }
                if (string.IsNullOrWhiteSpace(app)) app = "工具";
                // Build user prompt; include a concise mention that the raw JSON is attached below
                var user = loc?.Format(locale, "ui.server.inspection.user", new Dictionary<string,string>{{"app", app},{"result","JSON"}}, "You invoked {app}; its JSON will follow below. Please write an in-character inspection record. The result is: {result}")
                           ?? $"You invoked {app}; its JSON will follow below. Please write an in-character inspection record. The result is: JSON";
                return Task.FromResult(user);
            }
            catch { return Task.FromResult(string.Empty); }
        }
    }
}
