using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.World
{
    internal sealed class GroupChatSessionHandle
    {
        public string Id { get; set; }
    }

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

		/// <summary>
		/// 启动一次“群聊”任务：将参与者（除发起者外）强制移动至发起者周围 radius 格内的随机可走位置，并下发等待（Wait）任务，直至会话结束/打断。
		/// 返回会话句柄。会话生命期内由守护逻辑保持 Wait，不被日常 AI 打断；遇到征召/受击/倒地/极端精神状态/饥饿或极低休息将中断并自动 Dismiss。
		/// </summary>
		Task<GroupChatSessionHandle> StartGroupChatDutyAsync(int initiatorPawnLoadId, System.Collections.Generic.IReadOnlyList<int> participantLoadIds, int radius, System.TimeSpan maxDuration, CancellationToken ct = default);

		/// <summary>
		/// 主动结束“群聊”任务（成功或中断），清理相关 Job/状态。返回是否成功。
		/// </summary>
		Task<bool> EndGroupChatDutyAsync(GroupChatSessionHandle handle, string reason, CancellationToken ct = default);

		/// <summary>
		/// 查询会话是否仍然存活。
		/// </summary>
		bool IsGroupChatSessionAlive(GroupChatSessionHandle handle);
	}
}


