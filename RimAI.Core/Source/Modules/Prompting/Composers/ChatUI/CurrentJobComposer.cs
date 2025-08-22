using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class CurrentJobComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 90; // 移入 Activities 区域（靠后）
		public string Id => "current_job";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var blocks = new List<ContextBlock>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var job = await world.GetCurrentJobLabelAsync(ctx?.Request?.PawnLoadId ?? 0, ct).ConfigureAwait(false);
				if (!string.IsNullOrWhiteSpace(job))
				{
					var title = ctx?.L?.Invoke("prompt.section.current_job", "[Current Job]") ?? "[Current Job]";
					blocks.Add(new ContextBlock { Title = title, Text = job });
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = blocks };
		}
	}
}


