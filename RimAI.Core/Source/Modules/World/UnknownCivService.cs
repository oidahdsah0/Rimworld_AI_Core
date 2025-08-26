using System;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.Modules.World
{
    internal sealed class UnknownCivService : RimAI.Core.Source.Modules.Tooling.Execution.IUnknownCivService
    {
        private readonly IPersistenceService _persistence;
        private readonly IWorldActionService _world;
        private readonly Random _rng = new Random();

        public UnknownCivService(IPersistenceService persistence, IWorldActionService world)
        {
            _persistence = persistence;
            _world = world;
        }

        public RimAI.Core.Source.Modules.Tooling.Execution.UnknownCivContactResult ApplyContactAndMaybeGift()
        {
            var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
            var uc = snap.UnknownCiv ?? (snap.UnknownCiv = new UnknownCivState());

            // 随机好感变化 [-5, +15]
            int delta = _rng.Next(-5, 16);
            long nowAbs = Find.TickManager?.TicksAbs ?? 0;
            uc.Favor = Math.Max(-100, Math.Min(100, uc.Favor + delta));

            // 顶栏提示（好感变化）
            try { _world.ShowTopLeftMessageAsync("RimAI.UnknownCiv.FavorChanged".Translate(delta), RimWorld.MessageTypeDefOf.NeutralEvent); } catch { }

            // 判断是否触发赠礼
            bool gift = false; string note = string.Empty; int cooldownSec = 0;
            int minTicks = 3 * 60000; // 3 天（绝对 tick）按 60k/天 → 180k（此处使用 60k/天需确保 TicksAbs；这里保留 3*60k）
            int maxTicks = 5 * 60000; // 5 天
            int nextWindow = _rng.Next(minTicks, maxTicks + 1);
            if (uc.Favor > 65 && nowAbs >= (uc.NextGiftAllowedAbsTicks <= 0 ? 0 : uc.NextGiftAllowedAbsTicks))
            {
                gift = true;
                uc.LastGiftAtAbsTicks = (int)nowAbs;
                uc.NextGiftAllowedAbsTicks = (int)(nowAbs + nextWindow);
                cooldownSec = (int)(nextWindow / 60); // 60 tick ~= 1s 近似
                note = "RimAI.UnknownCiv.GiftIncoming".Translate().CapitalizeFirst();
                try { _world.ShowTopLeftMessageAsync(note, RimWorld.MessageTypeDefOf.PositiveEvent); } catch { }
                // 实际投放在主线程调度（异步 fire-and-forget）
                try { _ = _world.DropUnknownCivGiftAsync(GiftCoeff); } catch { }
            }
            else
            {
                // 设置下次窗口（即使本次未触发，也刷新一个随机冷却预览）
                cooldownSec = (int)(nextWindow / 60);
            }

            // 写回持久化快照（内存）
            _persistence.ReplaceLastSnapshotForDebug(snap);

            return new RimAI.Core.Source.Modules.Tooling.Execution.UnknownCivContactResult
            {
                FavorDelta = delta,
                FavorTotal = uc.Favor,
                CooldownSeconds = cooldownSec,
                GiftTriggered = gift,
                GiftNote = note
            };
        }

        private const float GiftCoeff = 2.0f;
    }
}
