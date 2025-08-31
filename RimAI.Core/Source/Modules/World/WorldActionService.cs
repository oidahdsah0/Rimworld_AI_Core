using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.World.Parts;
using Verse;

namespace RimAI.Core.Source.Modules.World
{
	/// <summary>
	/// 世界写入门面（WAS）的具体实现：按职能拆分到多个 Part，并统一通过调度器保证主线程执行。
	/// </summary>
	/// <remarks>
	/// 组成 Part：
	/// - PartyActionPart：最小化“聚会/强制移动”逻辑。
	/// - GroupChatActionPart：群聊占用的开始/守护/结束，以及 Pawn/Thing 漂浮文本。
	/// - MiscActionPart：通用显示消息、强制天气等杂项动作。
	/// - UnknownCivActionPart：未知文明互动（如空投礼物）。
	///
	/// 线程模型：所有方法仅作委派，不直接触碰 Verse；Part 内部通过 ISchedulerService 将写操作派发至游戏主线程。
	/// </remarks>
	internal sealed class WorldActionService : IWorldActionService
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;
		private readonly PartyActionPart _partyPart;
		private readonly GroupChatActionPart _groupChatPart;
		private readonly MiscActionPart _miscPart;
		private readonly UnknownCivActionPart _unknownCivPart;
		private readonly SubspaceActionPart _subspacePart;
		private readonly FactionActionPart _factionActionPart;
		private readonly DevExplosionPart _devExplosionPart;
		private readonly MoodReactionPart _moodReactionPart;

		public WorldActionService(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("WorldActionService requires ConfigurationService");
			_partyPart = new PartyActionPart(_scheduler, _cfg);
			_groupChatPart = new GroupChatActionPart(_scheduler, _cfg);
			_miscPart = new MiscActionPart(_scheduler, _cfg);
			_unknownCivPart = new UnknownCivActionPart(_scheduler, _cfg);
			_subspacePart = new SubspaceActionPart(_scheduler, _cfg);
			_factionActionPart = new FactionActionPart(_scheduler, _cfg);
			_devExplosionPart = new DevExplosionPart(_scheduler, _cfg);
			_moodReactionPart = new MoodReactionPart(_scheduler, _cfg);
		}

	/// <summary>
	/// 尝试在发起者附近发起一次简化“聚会”：强制若干同阵营殖民者移动到附近并等待。
	/// </summary>
	/// <param name="initiatorPawnLoadId">发起者 Pawn 的 thingIDNumber。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	/// <returns>至少有一名殖民者成功接收指令则返回 true。</returns>
	public Task<bool> TryStartPartyAsync(int initiatorPawnLoadId, CancellationToken ct = default)
			=> _partyPart.TryStartPartyAsync(initiatorPawnLoadId, ct);

	/// <summary>
	/// 在指定殖民者头顶显示一段漂浮文本（气泡）。
	/// </summary>
	/// <param name="pawnLoadId">目标 Pawn 的 thingIDNumber。</param>
	/// <param name="text">显示文本（空白将被忽略）。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	public Task ShowSpeechTextAsync(int pawnLoadId, string text, CancellationToken ct = default)
			=> _groupChatPart.ShowSpeechTextAsync(pawnLoadId, text, ct);

	/// <summary>
	/// 在指定 Thing（如服务器建筑）上方显示一段漂浮文本。
	/// </summary>
	/// <param name="thingLoadId">目标 Thing 的 thingIDNumber。</param>
	/// <param name="text">显示文本（空白将被忽略）。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	public Task ShowThingSpeechTextAsync(int thingLoadId, string text, CancellationToken ct = default)
			=> _groupChatPart.ShowThingSpeechTextAsync(thingLoadId, text, ct);

	/// <summary>
	/// 启动一次“群聊”占用：将参与者移至发起者半径内并入队 Wait，期间由守护逻辑维持；
	/// 达到墙钟上限或超过 3 个游戏小时，或出现征召/倒地/极端精神/极饥饿/极疲劳/近期受伤等情况会自动中止。
	/// </summary>
	/// <param name="initiatorPawnLoadId">发起者 Pawn 的 thingIDNumber。</param>
	/// <param name="participantLoadIds">参与者 Pawn 的 thingIDNumber 列表。</param>
	/// <param name="radius">围聚半径（格）。</param>
	/// <param name="maxDuration">最大墙钟持续时间。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	/// <returns>成功则返回会话句柄，失败返回 null。</returns>
	public Task<GroupChatSessionHandle> StartGroupChatDutyAsync(int initiatorPawnLoadId, System.Collections.Generic.IReadOnlyList<int> participantLoadIds, int radius, System.TimeSpan maxDuration, CancellationToken ct = default)
			=> _groupChatPart.StartAsync(initiatorPawnLoadId, participantLoadIds, radius, maxDuration, ct);

	/// <summary>
	/// 结束“群聊”占用：在主线程打断当前 Job（不继续队列）并清空队列，确保 Goto/Wait 被移除。
	/// </summary>
	/// <param name="handle">会话句柄。</param>
	/// <param name="reason">结束原因（"Aborted" 视为中止）。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	/// <returns>是否成功提交结束操作。</returns>
	public Task<bool> EndGroupChatDutyAsync(GroupChatSessionHandle handle, string reason, CancellationToken ct = default)
			=> _groupChatPart.EndAsync(handle, reason, ct);

	/// <summary>
	/// 查询群聊会话是否仍存活（受墙钟与 3 小时游戏 Tick 双重上限影响）。
	/// </summary>
	/// <param name="handle">会话句柄。</param>
	/// <returns>true 表示仍在进行；false 表示已结束或无效。</returns>
	public bool IsGroupChatSessionAlive(GroupChatSessionHandle handle)
			=> _groupChatPart.IsAlive(handle);

	/// <summary>
	/// 在屏幕左上角显示一条 RimWorld 消息。
	/// </summary>
	/// <param name="text">文本内容。</param>
	/// <param name="type">消息类型。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	public Task ShowTopLeftMessageAsync(string text, MessageTypeDef type, CancellationToken ct = default)
			=> _miscPart.ShowTopLeftMessageAsync(text, type, ct);

	/// <summary>
	/// 在最近的贸易落点附近空投一批“未知文明礼物”。
	/// </summary>
	/// <param name="quantityCoefficient">数量/价值的缩放系数（默认 1.0）。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	public Task DropUnknownCivGiftAsync(float quantityCoefficient = 1.0f, CancellationToken ct = default)
			=> _unknownCivPart.DropUnknownCivGiftAsync(quantityCoefficient, ct);

	/// <summary>
	/// 通过临时 GameCondition 在当前地图强制指定天气，持续给定 Tick 数（60,000 ≈ 1 天）。
	/// </summary>
	/// <param name="weatherDefName">WeatherDef 名称。</param>
	/// <param name="durationTicks">持续 Tick 数。</param>
	/// <param name="ct">取消令牌（可选）。</param>
	/// <returns>提交成功与否（地图/Def 无效将返回 false）。</returns>
	public Task<bool> TryForceWeatherAsync(string weatherDefName, int durationTicks, CancellationToken ct = default)
			=> _miscPart.TryForceWeatherAsync(weatherDefName, durationTicks, ct);

	/// <summary>
	/// 在当前地图尝试触发一次“亚空间召唤/侵蚀”。
	/// </summary>
	/// <param name="llmScore">强度评分（0..100）。</param>
	/// <param name="ct">取消令牌。</param>
	/// <returns>结果或 null。</returns>
	public Task<SubspaceInvocationOutcome> TryInvokeSubspaceAsync(int llmScore, CancellationToken ct = default)
			=> _subspacePart.TryInvokeAsync(llmScore, ct);

	public Task<FactionGoodwillAdjustResult> TryAdjustFactionGoodwillAsync(int factionLoadId, int delta, string reason, CancellationToken ct = default)
			=> _factionActionPart.TryAdjustGoodwillAsync(factionLoadId, delta, reason, ct);

		public Task<int> TryDevExplosionsNearEnemiesAsync(int strikes, int radius, CancellationToken ct = default)
		{
			return _devExplosionPart.TryExplodeNearEnemiesAsync(strikes, radius, ct);
		}

		public Task<bool> TryApplyChatReactionAsync(int pawnLoadId, int delta, string title, CancellationToken ct = default)
			=> _moodReactionPart.TryApplyChatReactionAsync(pawnLoadId, delta, title, null, ct);

		public Task<bool> TryApplyChatReactionAsync(int pawnLoadId, int delta, string title, int durationTicksOverride, CancellationToken ct = default)
			=> _moodReactionPart.TryApplyChatReactionAsync(pawnLoadId, delta, title, durationTicksOverride, ct);
	}
}


