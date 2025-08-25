using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class PowerStatusExecutor : IToolExecutor
	{
		public string Name => "get_power_status";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null) return Task.FromResult<object>(new { ok = false });
			return world.GetPowerStatusAsync(ct).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
				var s = t.Result;
				object res = new
				{
					ok = true,
					power = new
					{
						gen = s?.GenW ?? 0f,
						cons = s?.ConsW ?? 0f,
						net = s?.NetW ?? 0f,
						batteries = new
						{
							count = s?.Batteries?.Count ?? 0,
							stored = s?.Batteries?.StoredWd ?? 0f,
							days = s?.Batteries?.DaysLeft ?? -1f
						}
					}
				};
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
