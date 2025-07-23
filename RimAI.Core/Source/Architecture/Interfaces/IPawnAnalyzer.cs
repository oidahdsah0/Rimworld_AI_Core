using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// Defines the contract for an analyzer that provides detailed information about a single pawn.
    /// </summary>
    public interface IPawnAnalyzer
    {
        /// <summary>
        /// Asynchronously retrieves detailed information about a specific pawn by name.
        /// </summary>
        /// <param name="pawnName">The name of the pawn to analyze.</param>
        /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
        /// <returns>A string containing a descriptive summary of the pawn's status, skills, and needs.</returns>
        Task<string> GetPawnDetailsAsync(string pawnName, CancellationToken cancellationToken = default);
    }
} 