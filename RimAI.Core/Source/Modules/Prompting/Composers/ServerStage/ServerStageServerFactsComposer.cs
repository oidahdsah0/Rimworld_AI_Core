using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ServerStage
{
	internal sealed class ServerStageServerFactsComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ServerStage;
		public int Order => 20;
		public string Id => "server_stage_facts";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			var blocks = new List<ContextBlock>();
			try
			{
				var serverSvc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var locale = ctx?.Locale;
				var participants = ctx?.Request?.ParticipantIds?.Where(id => id != null && id.StartsWith("thing:"))?.ToList() ?? new List<string>();
				foreach (var sid in participants)
				{
					try
					{
						var pack = await serverSvc.BuildPromptAsync(sid, locale, ct).ConfigureAwait(false);
						if (pack?.SystemLines != null && pack.SystemLines.Count > 0) lines.AddRange(pack.SystemLines);
						if (pack?.ContextBlocks != null && pack.ContextBlocks.Count > 0) blocks.AddRange(pack.ContextBlocks);
					}
					catch { }
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = blocks };
		}
	}
}



