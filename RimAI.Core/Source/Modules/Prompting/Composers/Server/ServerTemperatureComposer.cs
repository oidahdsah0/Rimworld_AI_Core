using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Server
{
    internal sealed class ServerTemperatureComposer : IPromptComposer
    {
        private readonly PromptScope _scope;
        public ServerTemperatureComposer(PromptScope scope) { _scope = scope; }
        public PromptScope Scope => _scope;
        public int Order => 30;
        public string Id => "server_temperature";

        public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
        {
            var lines = new List<string>();
            try
            {
                var loc = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
                var world = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
                var presets = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerPromptPresetManager>();
                var locale = ctx?.Locale ?? "en";
                var preset = await presets.GetAsync(locale, ct).ConfigureAwait(false);

                // 猜测 entityId（thing:<id>）
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
                if (string.IsNullOrWhiteSpace(entityId)) return new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() };

                // 读取服务器温度（当前 WorldDataService 返回固定 37；后续 M2 会扩展）
                var snap = await world.GetAiServerSnapshotAsync(entityId, ct).ConfigureAwait(false);
                int t = snap?.TemperatureC ?? 37;
                var title = loc?.Get(locale, "ui.server.env.temp.title", "[Server Room Temperature]") ?? "[Server Room Temperature]";
                string line = null;
                if (t < 30) line = preset?.Env?.temp_low;
                else if (t < 70) line = preset?.Env?.temp_mid;
                else line = preset?.Env?.temp_high;
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(title);
                    lines.Add(line);
                }
            }
            catch { }
            return new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() };
        }
    }
}
