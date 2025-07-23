using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Analysis
{
    /// <summary>
    /// A placeholder implementation for the IPawnAnalyzer service.
    /// In a real scenario, this class would contain detailed logic to inspect a pawn's
    /// health, mood, skills, gear, and other attributes.
    /// </summary>
    public class PawnAnalyzer : IPawnAnalyzer
    {
        private readonly ISafeAccessService _safeAccess;

        public PawnAnalyzer()
        {
            _safeAccess = ServiceContainer.Instance.GetService<ISafeAccessService>();
        }
        
        public Task<string> GetPawnDetailsAsync(string pawnName, CancellationToken cancellationToken = default)
        {
            // This is a placeholder implementation.
            var pawn = _safeAccess.GetAllPawnsSafe(Find.CurrentMap)
                                  .FirstOrDefault(p => p.Name.ToStringFull == pawnName);

            if (pawn == null)
            {
                return Task.FromResult($"Pawn '{pawnName}' not found.");
            }

            // Corrected way to get health summary and top skill
            var healthSummary = HealthUtility.GetGeneralConditionLabel(pawn);
            var topSkill = pawn.skills.skills.OrderByDescending(s => s.Level).FirstOrDefault()?.def.LabelCap;

            var details = $"Details for {pawn.Name.ToStringFull}:\n" +
                          $"- Health: {healthSummary}\n" +
                          $"- Mood: {pawn.needs.mood?.CurLevelPercentage:P0}\n" +
                          $"- Top Skill: {topSkill ?? "None"}";

            return Task.FromResult(details);
        }
    }
} 