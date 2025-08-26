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

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class UnknownCivContactExecutor : IToolExecutor
    {
        public string Name => "get_unknown_civ_contact";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            try
            {
                // 生成伪加密信息（基于时间和地图的短随机）
                string cipher = GenerateCipherMessage();

                // 与服务交互以计算本次好感变化、是否触发赠礼及冷却等
                var svc = RimAICoreMod.TryGetService<IUnknownCivService>();
                var result = svc?.ApplyContactAndMaybeGift();

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
                return Task.FromResult(payload);
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new { ok = false, error = ex.Message });
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
    }

    internal interface IUnknownCivService
    {
        UnknownCivContactResult ApplyContactAndMaybeGift();
    }

    internal sealed class UnknownCivContactResult
    {
        public int FavorDelta { get; set; }
        public int FavorTotal { get; set; }
        public int CooldownSeconds { get; set; }
        public bool GiftTriggered { get; set; }
        public string GiftNote { get; set; }
    }
}
