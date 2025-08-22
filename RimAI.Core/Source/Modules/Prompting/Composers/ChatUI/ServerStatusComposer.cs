using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class ServerStatusComposer : IPromptComposer
	{
		private readonly RimAI.Core.Source.Modules.Server.IServerService _server;
		public ServerStatusComposer(RimAI.Core.Source.Modules.Server.IServerService server)
		{
			_server = server;
		}

		public string Id => "server_status";
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 7000;

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var output = new ComposerOutput();
			try
			{
				var list = _server?.List() ?? new List<RimAI.Core.Source.Modules.Persistence.Snapshots.ServerRecord>();
				if (list.Count == 0) return System.Threading.Tasks.Task.FromResult(output);
				var lines = new List<string>();
				var title = ctx?.L?.Invoke("prompt.section.server_status", "[Server Status]") ?? "[Server Status]";
				lines.Add(title);
				foreach (var s in list.OrderBy(x => x.EntityId))
				{
					var tpl = ctx?.F?.Invoke(
						"prompt.format.server_status_line",
						new System.Collections.Generic.Dictionary<string, string>
						{
							{"id", s.EntityId ?? string.Empty},
							{"level", (s.Level).ToString()},
							{"slots", (s.InspectionSlots?.Count ?? 0).ToString()},
							{"persona", (s.ServerPersonaSlots?.Count ?? 0).ToString()}
						},
						$"- {s.EntityId} Lv{s.Level} slots={(s.InspectionSlots?.Count ?? 0)} persona={(s.ServerPersonaSlots?.Count ?? 0)}"
					);
					lines.Add(tpl);
				}
				output.SystemLines = lines;
			}
			catch { }
			return System.Threading.Tasks.Task.FromResult(output);
		}
	}
}


