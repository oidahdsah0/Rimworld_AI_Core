using System.Threading;
using System.Threading.Tasks;
using RimWorld;
using RimAI.Core.Source.Modules.World.Parts;

namespace RimAI.Core.Source.Modules.World
{
	internal sealed class GroupChatSessionHandle
	{
		public string Id { get; set; }
	}

	internal sealed class SubspaceInvocationOutcome
	{
		public string Tier { get; set; } // e.g., "low|mid|high|apex"
		public string Composition { get; set; } // e.g., "insects", "shamblers", "revenant", "mixed"
		public int Count { get; set; } // estimated spawned pawn count (if applicable)
	}

	/// <summary>
	/// 世界写入操作（WAS: WorldActionService）的接口门面。
	/// 所有对 Verse/RimWorld 的“写操作”必须经由本接口实现，且由实现确保在主线程执行。
	/// </summary>
	/// <remarks>
	/// 设计约束与约定：
	/// - 主线程化：实现需通过调度器在主线程执行任何 Verse/Unity API 调用，调用方无需关心线程。
	/// - 白名单：仅暴露明确受控的“动作”，调用端不得直接访问 Verse 进行写入。
	/// - 无读职责：游戏数据的读取统一由 WDS（WorldDataService）负责，WAS 不提供读接口。
	/// - 失败容忍：所有方法尽可能失败即返回（false/空），不抛异常，不阻塞游戏主循环。
	/// </remarks>
	internal interface IWorldActionService
	{
		/// <summary>
		/// 尝试在给定殖民者位置附近发起一次“简化的聚会/派对”效果：
		/// 仅强制若干同阵营殖民者移动至其附近（并排队等待），无其他复杂逻辑。
		/// </summary>
		/// <param name="initiatorPawnLoadId">发起者 Pawn 的 thingIDNumber（保存/加载稳定 ID）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		/// <returns>成功下发至少一个移动/等待指令则为 true，否则 false。</returns>
		Task<bool> TryStartPartyAsync(int initiatorPawnLoadId, CancellationToken ct = default);

		/// <summary>
		/// 在指定殖民者头顶显示一段短文本气泡（Mote 文本）。
		/// </summary>
		/// <param name="pawnLoadId">目标 Pawn 的 thingIDNumber。</param>
		/// <param name="text">要显示的文本，空白将被忽略。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		Task ShowSpeechTextAsync(int pawnLoadId, string text, CancellationToken ct = default);

		/// <summary>
		/// 在指定 Thing（如服务器建筑）上方显示一段短文本气泡（Mote 文本）。
		/// </summary>
		/// <param name="thingLoadId">目标 Thing 的 thingIDNumber。</param>
		/// <param name="text">要显示的文本，空白将被忽略。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		Task ShowThingSpeechTextAsync(int thingLoadId, string text, CancellationToken ct = default);

		/// <summary>
		/// 启动一次“群聊”占用：
		/// - 将参与者（不含发起者）强制移动到发起者半径 <paramref name="radius"/> 内的随机可走单元格；
		/// - 为参与者入队 Wait 任务，期间定期守护避免被日常 AI 抢占；
		/// - 达到最大墙钟时长或超过 3 个游戏小时、或任一参与者征召/倒地/极端精神/极饥饿/极疲劳/近期受伤，将自动中止。
		/// </summary>
		/// <param name="initiatorPawnLoadId">发起者 Pawn 的 thingIDNumber。</param>
		/// <param name="participantLoadIds">参与者 Pawn 的 thingIDNumber 列表（可含与发起者同一人，将被忽略）。</param>
		/// <param name="radius">聚集半径（格）。小于 1 的值将被钳制为 1。</param>
		/// <param name="maxDuration">最大墙钟持续时间（真实时间）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		/// <returns>成功则返回会话句柄，失败返回 null。</returns>
		Task<GroupChatSessionHandle> StartGroupChatDutyAsync(int initiatorPawnLoadId, System.Collections.Generic.IReadOnlyList<int> participantLoadIds, int radius, System.TimeSpan maxDuration, CancellationToken ct = default);

		/// <summary>
		/// 主动结束“群聊”会话：
		/// - 在主线程打断参与者当前 Job 且不继续队列；
		/// - 清空 Job 队列，确保 Goto/Wait 等被完全清除。
		/// </summary>
		/// <param name="handle">会话句柄。</param>
		/// <param name="reason">结束原因（"Aborted" 视为中止，其余视为完成）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		/// <returns>是否成功结束（句柄有效且清理流程提交）。</returns>
		Task<bool> EndGroupChatDutyAsync(GroupChatSessionHandle handle, string reason, CancellationToken ct = default);

		/// <summary>
		/// 查询会话是否仍处于存活状态。
		/// </summary>
		/// <remarks>
		/// 存活条件同时受墙钟时间和游戏内 Tick 时长限制（3 小时上限）。
		/// </remarks>
		bool IsGroupChatSessionAlive(GroupChatSessionHandle handle);

		/// <summary>
		/// 在屏幕左上角以 RimWorld Message 的形式弹出一条文本。
		/// </summary>
		/// <param name="text">内容。</param>
		/// <param name="type">消息类型（影响颜色/历史等）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		Task ShowTopLeftMessageAsync(string text, Verse.MessageTypeDef type, CancellationToken ct = default);

		/// <summary>
		/// 在地图最近的贸易投放点附近空投一批“未知文明礼物”。
		/// </summary>
		/// <param name="quantityCoefficient">数量/价值缩放系数（1.0 为默认量）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		Task DropUnknownCivGiftAsync(float quantityCoefficient = 1.0f, CancellationToken ct = default);

		/// <summary>
		/// 在当前地图强制一段指定天气：通过临时的 GameCondition_ForceWeather 实现，时长以 Tick 计（60,000 Tick ≈ 1 天）。
		/// </summary>
		/// <param name="weatherDefName">WeatherDef 名称（精确或模糊匹配由外层工具/配置处理）。</param>
		/// <param name="durationTicks">持续 Tick 数。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		/// <returns>提交成功与否（若地图/WeatherDef 无效通常返回 false）。</returns>
		Task<bool> TryForceWeatherAsync(string weatherDefName, int durationTicks, CancellationToken ct = default);

		/// <summary>
		/// 尝试在当前地图触发一次“亚空间召唤/侵蚀”效果：
		/// - 根据传入的评分（0..100）判定强度与构成；
		/// - 若存在对应 DLC/事件，则优先调用 Incident；否则回退为直接生成虫族单位；
		/// - 在主线程执行，失败不抛异常。
		/// </summary>
		/// <param name="llmScore">强度评分（0..100）。</param>
		/// <param name="ct">取消令牌（可选）。</param>
		/// <returns>执行结果（包含强度分层/构成/数量统计）；失败返回 null。</returns>
		Task<SubspaceInvocationOutcome> TryInvokeSubspaceAsync(int llmScore, CancellationToken ct = default);

		/// <summary>
		/// 在敌对单位附近随机位置执行若干次“开发者式爆炸”（随机伤害类型），用于演示或特殊工具效果。
		/// </summary>
		/// <param name="strikes">爆炸次数（建议 5–15）。</param>
		/// <param name="radius">围绕敌对聚类中心的随机偏移半径（格）。</param>
		/// <param name="ct">取消令牌。</param>
		/// <returns>实际执行的爆炸次数（可能小于请求值）。</returns>
		Task<int> TryDevExplosionsNearEnemiesAsync(int strikes, int radius, CancellationToken ct = default);

		/// <summary>
		/// 调整一个派系与玩家派系之间的好感度（-100..100 范围剪裁），并返回前后值与派系信息。
		/// </summary>
		/// <param name="factionLoadId">目标派系的稳定 loadID。</param>
		/// <param name="delta">增量（负值减少好感，正值增加）。</param>
		/// <param name="reason">记录原因（用于日志/历史）。</param>
		/// <param name="ct">取消令牌。</param>
		/// <returns>结果对象或 null（失败）。</returns>
		Task<FactionGoodwillAdjustResult> TryAdjustFactionGoodwillAsync(int factionLoadId, int delta, string reason, CancellationToken ct = default);
	}
}


