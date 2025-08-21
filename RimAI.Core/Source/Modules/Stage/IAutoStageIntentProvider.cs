using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage
{
	/// <summary>
	/// 表示该 Act 支持被“全局唯一触发器”自动选中并构造一次 StageIntent。
	/// 返回 null 表示当前条件不满足，跳过本次触发。
	/// </summary>
	internal interface IAutoStageIntentProvider
	{
		Task<StageIntent> TryBuildAutoIntentAsync(CancellationToken ct);
	}
}



