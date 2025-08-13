using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.World
{
	internal interface IWorldDataService
	{
		Task<string> GetPlayerNameAsync(CancellationToken ct = default);
	}
}


