using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class MoodReactionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public MoodReactionPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        /// <summary>
        /// 为指定 pawn 应用一次 RimAI_ChatReaction 内存思想，设置 moodOffset=delta，时长使用 Def 默认或覆盖。
        /// </summary>
    public Task<bool> TryApplyChatReactionAsync(int pawnLoadId, int delta, string title, int? durationTicksOverride = null, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Math.Max(timeoutMs, 1500));
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (Current.Game == null) return false;
                    Pawn pawn = null;
                    foreach (var map in Find.Maps)
                    {
                        foreach (var p in map.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                        { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } }
                        if (pawn != null) break;
                    }
                    if (pawn == null || pawn.needs?.mood == null) return false;

                    // 三槽位状态机：依次尝试 Slot1/2/3；若都有则清理后写 Slot1
                    var def1 = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimAI_ChatReaction_Slot1");
                    var def2 = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimAI_ChatReaction_Slot2");
                    var def3 = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimAI_ChatReaction_Slot3");
                    if (def1 == null || def2 == null || def3 == null) { Log.Warning("[RimAI.Core] ChatReaction Slot ThoughtDefs missing."); return false; }

                    var mems = pawn.needs.mood.thoughts.memories;
                    var m1 = mems.GetFirstMemoryOfDef(def1);
                    var m2 = mems.GetFirstMemoryOfDef(def2);
                    var m3 = mems.GetFirstMemoryOfDef(def3);

                    ThoughtDef targetDef = null;
                    if (m1 == null) targetDef = def1;
                    else if (m2 == null) targetDef = def2;
                    else if (m3 == null) targetDef = def3;
                    else
                    {
                        // 三者均存在：删除“剩余时间最短”的一个，再复用该槽位
                        int rem1 = System.Math.Max(0, m1.DurationTicks - m1.age);
                        int rem2 = System.Math.Max(0, m2.DurationTicks - m2.age);
                        int rem3 = System.Math.Max(0, m3.DurationTicks - m3.age);
                        if (rem1 <= rem2 && rem1 <= rem3)
                        {
                            mems.RemoveMemory(m1);
                            targetDef = def1;
                        }
                        else if (rem2 <= rem1 && rem2 <= rem3)
                        {
                            mems.RemoveMemory(m2);
                            targetDef = def2;
                        }
                        else
                        {
                            mems.RemoveMemory(m3);
                            targetDef = def3;
                        }
                    }

                    var mem = ThoughtMaker.MakeThought(targetDef) as Thought_Memory;
                    if (mem == null) return false;
                    mem.moodOffset = delta;
                    if (durationTicksOverride.HasValue && durationTicksOverride.Value > 0)
                        mem.durationTicksOverride = durationTicksOverride.Value;

                    if (mem is RimAI.Core.Source.RimWorldCompat.Thoughts.Thought_Memory_RimAI_ChatReaction custom)
                    {
                        if (!string.IsNullOrWhiteSpace(title)) custom.customTitle = title;
                    }

                    pawn.needs.mood.thoughts.memories.TryGainMemory(mem);

                    // 可选：顶左提示，便于可视化调试
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(title))
                        {
                            var msg = $"{pawn.LabelShort}: {title} ({delta:+#;-#;0})";
                            Messages.Message(msg, MessageTypeDefOf.NeutralEvent);
                        }
                    }
                    catch { }

                    return true;
                }
                catch { return false; }
            }, name: "World.TryApplyChatReaction", ct: cts.Token);
        }
    }
}
