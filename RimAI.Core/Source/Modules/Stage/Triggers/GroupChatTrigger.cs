using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Modules.Stage.Kernel;
using RimAI.Core.Modules.World;
using RimAI.Core.Settings;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Modules.Stage.Triggers
{
    internal sealed class GroupChatTrigger : IStageTrigger
    {
        public string Name => "GroupChat.Proximity";
        public string TargetActName => "GroupChat";

        private readonly Infrastructure.Configuration.IConfigurationService _config;

        public GroupChatTrigger(Infrastructure.Configuration.IConfigurationService config)
        {
            _config = config;
        }

        public void Subscribe(IEventBus bus, IStageKernel kernel)
        {
            // 可选：被动事件触发，MVP 暂不订阅
        }

        public Task OnEnableAsync(IStageKernel kernel, CancellationToken ct) => Task.CompletedTask;
        public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

        public async Task RunOnceAsync(IStageService stage, IStageKernel kernel, CancellationToken ct)
        {
            var stageCfg = _config.Current?.Stage ?? new StageConfig();
            var cfg = stageCfg?.ProximityScan ?? new StageProximityScanConfig();
            if (!(_config.Current?.Stage?.Scan?.Enabled ?? true)) return;
            if (!(cfg?.Enabled ?? true)) return;

            // 主线程收集候选 Pawn 列表
            var pawns = await CoreServices.Locator.Get<Infrastructure.ISchedulerService>()
                .ScheduleOnMainThreadAsync(() => CollectEligiblePawns(cfg));
            if (pawns.Count == 0) return;

            var rnd = new Random(Environment.TickCount);
            var maxParticipants = Math.Max(2, Math.Min(10, stageCfg?.MaxParticipants ?? 5));
            var minParticipants = Math.Max(2, stageCfg?.MinParticipants ?? 2);

            // 选择锚点
            var anchor = pawns[rnd.Next(pawns.Count)];

            // 计算邻居（同地图，距离阈值内）
            var neighbors = pawns
                .Where(p => p != anchor && DistanceApprox(anchor, p) <= Math.Max(1f, cfg.RangeK))
                .ToList();
            if (neighbors.Count == 0) return;

            // 映射为参与者 ID，并过滤掉正被 Kernel 占用的参与者
            var pid = CoreServices.Locator.Get<IParticipantIdService>();
            var idAnchor = pid.FromVerseObject(anchor);
            if (string.IsNullOrWhiteSpace(idAnchor) || kernel.IsBusyByParticipant(idAnchor)) return;

            var neighborInfos = neighbors
                .Select(n => new { Pawn = n, Id = pid.FromVerseObject(n), Dist = DistanceApprox(anchor, n) })
                .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !kernel.IsBusyByParticipant(x.Id))
                .OrderBy(x => x.Dist)
                .ToList();

            // 选取最近的若干人，凑到上限为止
            var selectedIds = neighborInfos
                .Take(Math.Max(0, maxParticipants - 1))
                .Select(x => x.Id)
                .ToList();

            var participants = new List<string> { idAnchor };
            participants.AddRange(selectedIds);

            if (participants.Count < minParticipants) return;

            // 按 convKey 早退检查：忙碌或冷却中直接退出
            var convKey = string.Join("|", participants.OrderBy(x => x, StringComparer.Ordinal));
            if (kernel.IsBusyByConvKey(convKey)) return;
            if (kernel.IsInCooldown(convKey)) return;

            // 触发判定
            bool trigger;
            if (cfg.TriggerMode == StageProximityTriggerMode.Threshold)
            {
                var r = rnd.NextDouble();
                trigger = r > Math.Min(1.0, Math.Max(0.0, cfg.TriggerThreshold));
            }
            else
            {
                var r = rnd.NextDouble();
                trigger = r < Math.Max(0.0, Math.Min(1.0, cfg.ProbabilityP));
            }
            if (!trigger) return;

            var intent = new StageIntent
            {
                ActName = TargetActName,
                Participants = participants,
                ConvKey = convKey,
                Seed = null // 交由 Stage 侧统一计算（convKey → seed）
            };
            var decision = stage.SubmitIntent(intent);
            if (!string.Equals(decision?.Outcome, "Approve", StringComparison.OrdinalIgnoreCase)) return;

            var req = new StageExecutionRequest
            {
                ActName = TargetActName,
                Participants = participants,
                Seed = null,
                UserInputOrScenario = string.Empty
            };
            await foreach (var _ in stage.StartAsync(req, ct)) { }
        }

        private static List<Verse.Pawn> CollectEligiblePawns(StageProximityScanConfig cfg)
        {
            var list = new List<Verse.Pawn>();
            if (Verse.Find.Maps == null) return list;
            foreach (var map in Verse.Find.Maps)
            {
                var pawns = map?.mapPawns?.FreeColonistsSpawned;
                if (pawns == null) continue;
                foreach (var p in pawns)
                {
                    if (p == null || p.Dead || p.Downed) continue;
                    if (cfg.ExcludeBusy && p?.jobs?.curDriver != null) continue;
                    list.Add(p);
                }
            }
            return list;
        }

        private static float DistanceApprox(Verse.Pawn a, Verse.Pawn b)
        {
            try
            {
                if (a?.Map != b?.Map) return float.MaxValue;
                var da = a.Position; var db = b.Position;
                var dx = da.x - db.x; var dz = da.z - db.z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
            catch { return float.MaxValue; }
        }
    }
}


