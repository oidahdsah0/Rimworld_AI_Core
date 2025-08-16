using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class ColonyStatusComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 40; // after skills/traits
		public string Id => "colony_status";

		public async Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var snap = await world.GetColonySnapshotAsync(ctx?.Request?.PawnLoadId, ct).ConfigureAwait(false);
				if (snap != null)
				{
					var title = ctx?.L?.Invoke("prompt.section.colony", "[领地]") ?? "[领地]";
					var nameLine = ctx?.F?.Invoke("prompt.format.colony_name", new Dictionary<string, string> { { "name", snap.ColonyName ?? string.Empty } }, $"名称：{snap.ColonyName}") ?? $"名称：{snap.ColonyName}";
					var countLine = ctx?.F?.Invoke("prompt.format.colony_count", new Dictionary<string, string> { { "count", snap.ColonistCount.ToString() } }, $"人口：{snap.ColonistCount}") ?? $"人口：{snap.ColonistCount}";
					lines.Add(title + nameLine + "; " + countLine);

					if (snap.Colonists != null && snap.Colonists.Count > 0)
					{
						var title2 = ctx?.L?.Invoke("prompt.section.colonists", "[我方人员列表]") ?? "[我方人员列表]";
						var items = new List<string>();
						foreach (var c in snap.Colonists.Take(18))
						{
							var piece = ctx?.F?.Invoke("prompt.format.colonist_item", new Dictionary<string, string>
							{
								{ "name", c?.Name ?? string.Empty },
								{ "age", c?.Age.ToString() ?? "0" },
								{ "gender", c?.Gender ?? string.Empty },
								{ "job", string.IsNullOrWhiteSpace(c?.JobTitle) ? "" : c.JobTitle }
							}, $"{c?.Name}({c?.Age}岁,{c?.Gender}{(string.IsNullOrWhiteSpace(c?.JobTitle) ? "" : "," + c.JobTitle)})")
							?? $"{c?.Name}({c?.Age}岁,{c?.Gender}{(string.IsNullOrWhiteSpace(c?.JobTitle) ? "" : "," + c.JobTitle)})";
							items.Add(piece);
						}
						var joined = string.Join("、", items);
						var listLine = ctx?.F?.Invoke("prompt.format.colonists_line", new Dictionary<string, string> { { "list", joined } }, $"名单：{joined}") ?? $"名单：{joined}";
						lines.Add(title2 + listLine);
					}
				}
			}
			catch { }
			return new ComposerOutput { SystemLines = lines, ContextBlocks = System.Array.Empty<ContextBlock>() };
		}
	}
}
