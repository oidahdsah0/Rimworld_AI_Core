using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ServerStage
{
	internal sealed class ServerStageContractComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ServerStage;
		public int Order => 50; // after facts, before colony status adapter (60)
		public string Id => "server_stage_contract";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var lines = new List<string>();
			try
			{
				var participants = ctx?.Request?.ParticipantIds?.Where(id => id != null && id.StartsWith("thing:"))?.ToList() ?? new List<string>();
				if (participants.Count > 0)
				{
					var whitelist = string.Join(", ", participants);
					var contract = "{\\\"speaker\\\":\\\"thing:<id>\\\",\\\"content\\\":\\\"...\\\"}";
					// Localized server chat contract instruction
					var args = new Dictionary<string, string> { { "contract", contract }, { "whitelist", whitelist } };
					string line = null;
					try { line = ctx?.F?.Invoke("stage.serverchat.contract", args, null); } catch { line = null; }
					if (string.IsNullOrWhiteSpace(line))
					{
						line = $"Output JSON array only: each element is {contract}; speakers must be in whitelist: [{whitelist}]; no extra explanations.";
					}
					lines.Add(line);
				}
			}
			catch { }
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });
		}
	}
}



