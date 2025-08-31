using System;
using System.Collections.Concurrent;
using Verse;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal static class ReactionCooldown
    {
        // 记录到游戏绝对 Tick（TicksGame）后的“可再次写入时间”
        private static readonly ConcurrentDictionary<int, int> NextAvailableAt = new();
        private static readonly Random Rng = new Random();

        public static bool TryEnter(int pawnLoadId)
        {
            try
            {
                int now = 0;
                try { now = Find.TickManager?.TicksGame ?? 0; } catch { now = 0; }
                if (now <= 0) now = Environment.TickCount & int.MaxValue;

                if (NextAvailableAt.TryGetValue(pawnLoadId, out var ready) && now < ready)
                {
                    return false; // 冷却中
                }

                // 0..24 小时 → 0..1,440,000 ticks
                int cooldownTicks = Rng.Next(0, 24 * 60000 + 1);
                int until = unchecked(now + cooldownTicks);
                NextAvailableAt[pawnLoadId] = until;
                return true;
            }
            catch { return true; }
        }
    }
}
