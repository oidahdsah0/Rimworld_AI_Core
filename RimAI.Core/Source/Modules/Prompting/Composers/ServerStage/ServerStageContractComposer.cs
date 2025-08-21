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
					lines.Add($"仅输出 JSON 数组，每个元素形如 {contract}；发言者必须在白名单内：[{whitelist}]；不得输出解释文本或额外内容。");
				}
			}
			catch { }
			return Task.FromResult(new ComposerOutput { SystemLines = lines, ContextBlocks = Array.Empty<ContextBlock>() });
		}
	}
}



