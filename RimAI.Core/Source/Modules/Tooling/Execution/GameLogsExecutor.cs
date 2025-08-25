using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class GameLogsExecutor : IToolExecutor
	{
		public string Name => "get_game_logs";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			int count = 30;
			if (args != null && args.TryGetValue("count", out var vc) && vc != null)
			{
				try { count = Convert.ToInt32(vc, CultureInfo.InvariantCulture); } catch { count = 30; }
			}
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null) return Task.FromResult<object>(new { ok = false });
			return world.GetRecentGameLogsAsync(count, ct).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
				object res = new { ok = true, items = t.Result };
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
