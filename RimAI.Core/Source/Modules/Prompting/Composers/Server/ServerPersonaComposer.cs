using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    internal sealed class ServerPersonaComposer : IPromptComposer
    {
        private readonly PromptScope _scope;
        public ServerPersonaComposer(PromptScope scope) { _scope = scope; }
        public PromptScope Scope => _scope;
        public int Order => 20;
        public string Id => "server_persona";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var serverSvc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
                var presetMgr = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerPromptPresetManager>();
                var locale = ctx?.Locale ?? "en";

                // 猜测 entityId
                string entityId = null;
                try
                {
                    var pid = ctx?.Request?.ParticipantIds ?? Array.Empty<string>();
                    foreach (var p in pid)
                    {
                        if (string.IsNullOrWhiteSpace(p)) continue;
                        if (p.StartsWith("thing:")) { entityId = p; break; }
                        if (p.StartsWith("server:")) { var id = p.Substring(7); if (int.TryParse(id, out var n)) { entityId = $"thing:{n}"; break; } }
                    }
                }
                catch { }
                if (string.IsNullOrWhiteSpace(entityId)) return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });

                var rec = serverSvc?.Get(entityId);
                if (rec == null) return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });
                var preset = presetMgr.GetAsync(locale).GetAwaiter().GetResult();

                var title = loc?.Get(locale, "ui.server.persona.title", "[Server Persona]") ?? "[Server Persona]";
                string personaText = null;
                if (!string.IsNullOrWhiteSpace(rec.BaseServerPersonaOverride)) personaText = rec.BaseServerPersonaOverride;
                else if (!string.IsNullOrWhiteSpace(rec.BaseServerPersonaPresetKey))
                {
                    var opt = preset?.ServerPersonaOptions?.FirstOrDefault(o => string.Equals(o.key, rec.BaseServerPersonaPresetKey, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(opt.text)) personaText = opt.text;
                }
                else if (!string.IsNullOrWhiteSpace(preset?.BaseServerPersonaText)) personaText = preset.BaseServerPersonaText;

                if (!string.IsNullOrWhiteSpace(personaText))
                {
                    lines.Add(title);
                    lines.Add(personaText);
                }
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });
        }
    }
}
