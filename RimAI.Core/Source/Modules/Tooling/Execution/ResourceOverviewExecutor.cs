using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class ResourceOverviewExecutor : IToolExecutor
	{
		public string Name => "get_resource_overview";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null) return Task.FromResult<object>(new { ok = false });
			return world.GetResourceOverviewAsync(ct).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
				var snap = t.Result;
				object res = new
				{
					ok = true,
					resources = (snap?.Resources ?? new List<ResourceItem>())
						.Select(r => new { defName = r.DefName, qty = r.Count, dailyUse = r.DailyUse, daysLeft = r.DaysLeft, label = r.Label })
				};
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
