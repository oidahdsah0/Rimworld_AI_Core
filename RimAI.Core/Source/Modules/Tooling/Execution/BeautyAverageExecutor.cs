using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class BeautyAverageExecutor : IToolExecutor
	{
		public string Name => "get_beauty_average";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			if (args == null || !args.TryGetValue("x", out var vx) || !args.TryGetValue("z", out var vz) || !args.TryGetValue("radius", out var vr))
				throw new ArgumentException("missing x|z|radius");
			var cx = Convert.ToInt32(vx, CultureInfo.InvariantCulture);
			var cz = Convert.ToInt32(vz, CultureInfo.InvariantCulture);
			var r = Convert.ToInt32(vr, CultureInfo.InvariantCulture);
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null) return Task.FromResult<object>(new { ok = false });
			return world.GetBeautyAverageAsync(cx, cz, r, ct).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
				object res = new { x = cx, z = cz, radius = r, avg = t.Result };
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
