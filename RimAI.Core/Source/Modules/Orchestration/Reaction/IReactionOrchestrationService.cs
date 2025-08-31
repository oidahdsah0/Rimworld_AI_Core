using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Orchestration.Reaction
{
	internal interface IReactionOrchestrationService
	{
		// 入队：在后台执行一次“对话后反应”工具调用（非阻塞 UI）
		Task EnqueuePawnSmalltalkReactionAsync(
			string convKey,
			IReadOnlyList<string> participantIds,
			string lastUserText,
			string lastAssistantText,
			string locale,
			string playerTitle,
			CancellationToken ct = default);
	}
}
