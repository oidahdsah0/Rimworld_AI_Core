using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class PawnIdentityPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public PawnIdentityPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<PawnPromptSnapshot> GetPawnPromptSnapshotAsync(int pawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null;
                foreach (var map in Find.Maps)
                {
                    foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>())
                    { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } }
                    if (pawn != null) break;
                }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var snap = new PawnPromptSnapshot
                {
                    Id = new Identity
                    {
                        Name = pawn.Name?.ToStringShort ?? pawn.LabelCap ?? "Pawn",
                        Gender = pawn.gender.ToString(),
                        Age = pawn.ageTracker != null ? (int)UnityEngine.Mathf.Floor(pawn.ageTracker.AgeBiologicalYearsFloat) : 0,
                        Race = pawn.def?.label ?? string.Empty,
                        Belief = null
                    },
                    Story = new Backstory
                    {
                        Childhood = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetBackstoryTitle(pawn, true) ?? string.Empty,
                        Adulthood = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetBackstoryTitle(pawn, false) ?? string.Empty
                    },
                    Traits = new TraitsAndWork
                    {
                        Traits = (pawn.story?.traits?.allTraits ?? new System.Collections.Generic.List<Trait>()).Select(t => t.LabelCap ?? t.Label).ToList(),
                        WorkDisables = (RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetCombinedDisabledWorkTagsCsv(pawn) ?? string.Empty).Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
                    },
                    Skills = new Skills
                    {
                        Items = (pawn.skills?.skills ?? new System.Collections.Generic.List<SkillRecord>()).Select(s => new SkillItem
                        {
                            Name = s.def?.label ?? s.def?.defName ?? string.Empty,
                            Level = s.Level,
                            Passion = s.passion.ToString(),
                            Normalized = UnityEngine.Mathf.Clamp01(s.Level / 20f)
                        }).ToList()
                    },
                    IsIdeologyAvailable = ModsConfig.IdeologyActive
                };
                if (snap.IsIdeologyAvailable)
                {
                    try { snap.Id.Belief = pawn.Ideo?.name ?? pawn.Ideo?.ToString() ?? null; }
                    catch { snap.Id.Belief = null; }
                }
                return snap;
            }, name: "GetPawnPromptSnapshot", ct: cts.Token);
        }
    }
}
