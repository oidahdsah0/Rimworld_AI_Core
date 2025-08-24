using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    internal sealed class ServerIdentityComposer : IPromptComposer
    {
        private readonly PromptScope _scope;
        public ServerIdentityComposer(PromptScope scope) { _scope = scope; }
        public PromptScope Scope => _scope;
        public int Order => 10;
        public string Id => "server_identity";

        public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var world = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
                var serverSvc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
                var locale = ctx?.Locale ?? "en";

                // 解析服务器 entityId：从 participants 猜测
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
                int level = rec?.Level ?? 1;
                string serial = rec?.SerialHex12 ?? string.Empty;
                // 名称：简化为模板 AI Server L{level}
                var name = loc?.Get(locale, "RimAI.Common.AIServer", "AI Server") ?? "AI Server";
                var nameWithLv = $"{name} L{level}";

                var title = loc?.Get(locale, "ui.server.identity.title", "[Server Identity]") ?? "[Server Identity]";
                var line = (loc?.Format(locale, "ui.server.identity.line",
                    new Dictionary<string, string>
                    {
                        {"name", nameWithLv},
                        {"sn", string.IsNullOrWhiteSpace(serial) ? "-" : serial},
                        {"level", level.ToString()}
                    },
                    $"Name: {nameWithLv}; SN: {serial}; Level: L{level}") ?? $"Name: {nameWithLv}; SN: {serial}; Level: L{level}");
                lines.Add(title);
                lines.Add(line);
            }
            catch { }
            return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });
        }
    }
}
