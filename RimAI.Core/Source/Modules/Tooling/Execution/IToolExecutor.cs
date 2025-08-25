using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal interface IToolExecutor
	{
		string Name { get; }
		Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default);
	}
}
