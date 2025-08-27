using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Boot;
using Verse;
using RimWorld;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed partial class UnknownCivContactExecutor : IToolExecutor
    {
        public string Name => "get_unknown_civ_contact";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            try
            {
                // Runtime self-checks to keep scheduler simple:
                // 1) Research must be finished (via WDS, async to ensure main-thread scheduling)
                var wds = RimAICoreMod.Container.Resolve<IWorldDataService>();
                var researchOk = wds != null && await wds.IsResearchFinishedAsync("RimAI_GW_Communication", ct).ConfigureAwait(false);
                if (!researchOk)
                {
                    // Do not perform side effects; return structured failure
                    return new
                    {
                        ok = false,
                        error = "RESEARCH_LOCKED",
                        require = new { research = "RimAI_GW_Communication" }
                    };
                }

                // 2) At least one powered antenna must exist (via WDS async; executor may run on background thread)
                var antennaOk = wds != null && await wds.HasPoweredAntennaAsync(ct).ConfigureAwait(false);
                if (!antennaOk)
                {
                    try
                    {
                        var worldSvc = RimAICoreMod.Container.Resolve<IWorldActionService>();
                        worldSvc?.ShowTopLeftMessageAsync("RimAI.Tool.RequireAntennaPowered".Translate(), MessageTypeDefOf.RejectInput);
                    }
                    catch { }
                    return new { ok = false, error = "REQUIRE_ANTENNA_POWERED" };
                }

                // 生成伪加密信息（基于时间和地图的短随机）
                string cipher = GenerateCipherMessage();

                // 计算本次好感变化、是否触发赠礼及冷却等（直接通过 Persistence + WorldAction 执行）
                var persistence = RimAICoreMod.Container.Resolve<IPersistenceService>();
                var action = RimAICoreMod.Container.Resolve<IWorldActionService>();
                var result = ApplyContactAndMaybeGiftInternal(persistence, action);

                object payload = new
                {
                    ok = true,
                    cipher_message = cipher,
                    favor_delta = result?.FavorDelta ?? 0,
                    favor_total = result?.FavorTotal ?? 0,
                    cooldown_sec = result?.CooldownSeconds ?? 0,
                    should_gift = result?.GiftTriggered ?? false,
                    gift_note = result?.GiftNote ?? string.Empty
                };
                return payload;
            }
            catch (Exception ex)
            {
                return new { ok = false, error = ex.Message };
            }
        }

        private static string GenerateCipherMessage()
        {
            var map = Find.CurrentMap;
            int seed = (int)(Find.TickManager.TicksGame ^ 0x5F3759DF);
            if (map != null) seed ^= Gen.HashCombineInt(map.uniqueID, map.Tile);
            var rng = new System.Random(seed);
            int len = rng.Next(28, 56);
            const string glyphs = "⡀⡈⡐⡠⡢⡤⡨⡪⡬⣀⣂⣄⣆⣈⣊⣌⣎⣐⣒⣔⣖⣘⣚⣜⣞⣠⣡⣢⣣⣤⣥⣦⣧⣨⣩⣪⣫⣬⣭⣮⣯⣰⣱⣲⣳⣴⣵⣶⣷⣸⣹⣺⣻⣼⣽⣾⣿";
            var sb = new StringBuilder(len + len / 7 + 4);
            for (int i = 0; i < len; i++)
            {
                if (i > 0 && i % rng.Next(5, 8) == 0) sb.Append(' ');
                sb.Append(glyphs[rng.Next(glyphs.Length)]);
            }
            return sb.ToString();
        }

    // 研究/设备查询已通过 WorldDataService 统一入口
    }

    internal sealed class UnknownCivContactResult
    {
        public int FavorDelta { get; set; }
        public int FavorTotal { get; set; }
        public int CooldownSeconds { get; set; }
        public bool GiftTriggered { get; set; }
        public string GiftNote { get; set; }
    }

    // 内联 UnknownCiv 行为：以 Persistence + WorldAction 为唯一依赖
    internal static class UnknownCivLogic
    {
        internal const float GiftCoeff = 2.0f;
    }

    internal partial class UnknownCivContactExecutor
    {
        private UnknownCivContactResult ApplyContactAndMaybeGiftInternal(IPersistenceService persistence, IWorldActionService world)
        {
            var snap = persistence?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
            var uc = snap.UnknownCiv ?? (snap.UnknownCiv = new UnknownCivState());

            var rng = new System.Random();
            int delta = rng.Next(-5, 16); // [-5, +15]
            long nowAbs = Find.TickManager?.TicksAbs ?? 0;
            uc.Favor = Math.Max(-100, Math.Min(100, uc.Favor + delta));

            try { world?.ShowTopLeftMessageAsync("RimAI.UnknownCiv.FavorChanged".Translate(delta), MessageTypeDefOf.NeutralEvent); } catch { }

            bool gift = false; string note = string.Empty; int cooldownSec = 0;
            int minTicks = 3 * 60000; // 3 天
            int maxTicks = 5 * 60000; // 5 天
            int nextWindow = rng.Next(minTicks, maxTicks + 1);
            if (uc.Favor > 65 && nowAbs >= (uc.NextGiftAllowedAbsTicks <= 0 ? 0 : uc.NextGiftAllowedAbsTicks))
            {
                gift = true;
                uc.LastGiftAtAbsTicks = (int)nowAbs;
                uc.NextGiftAllowedAbsTicks = (int)(nowAbs + nextWindow);
                cooldownSec = (int)(nextWindow / 60); // 60 tick ~= 1s（近似）
                note = "RimAI.UnknownCiv.GiftIncoming".Translate().CapitalizeFirst();
                try { world?.ShowTopLeftMessageAsync(note, MessageTypeDefOf.PositiveEvent); } catch { }
                try { _ = world?.DropUnknownCivGiftAsync(UnknownCivLogic.GiftCoeff); } catch { }
            }
            else
            {
                cooldownSec = (int)(nextWindow / 60);
            }

            persistence?.ReplaceLastSnapshotForDebug(snap);

            return new UnknownCivContactResult
            {
                FavorDelta = delta,
                FavorTotal = uc.Favor,
                CooldownSeconds = cooldownSec,
                GiftTriggered = gift,
                GiftNote = note
            };
        }
    }
}
