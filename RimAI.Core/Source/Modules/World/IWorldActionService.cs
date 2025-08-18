using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.World
{
	/// <summary>
	/// 白名单的世界写操作封装。所有 Verse/RimWorld 写入必须在主线程执行，由本服务进行主线程化调度。
	/// </summary>
	internal interface IWorldActionService
	{
		/// <summary>
		/// 在给定殖民者处尝试启动一次“聚会/派对”。返回是否成功。
		/// </summary>
		Task<bool> TryStartPartyAsync(int initiatorPawnLoadId, CancellationToken ct = default);

		/// <summary>
		/// 在指定殖民者头顶显示一段短文本（对话气泡/漂浮文本效果）。
		/// </summary>
		Task ShowSpeechTextAsync(int pawnLoadId, string text, CancellationToken ct = default);
	}
}


