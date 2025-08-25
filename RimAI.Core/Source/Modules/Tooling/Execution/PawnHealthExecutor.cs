using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class PawnHealthExecutor : IToolExecutor
	{
		public string Name => "get_pawn_health";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			if (args == null || !args.TryGetValue("pawn_id", out var v) || v == null)
				throw new ArgumentException("missing pawn_id");
			var pawnId = Convert.ToInt32(v, CultureInfo.InvariantCulture);
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null)
			{
				return Task.FromResult<object>(new { pawn_id = pawnId, ok = false });
			}
			return world.GetPawnHealthSnapshotAsync(pawnId, ct).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { pawn_id = pawnId, ok = false });
				var s = t.Result;
				var avg = (s.Consciousness + s.Moving + s.Manipulation + s.Sight + s.Hearing + s.Talking + s.Breathing + s.BloodPumping + s.BloodFiltration + s.Metabolism) / 10f * 100f;
				object res = new
				{
					pawn_id = s.PawnLoadId,
					avg = avg,
					is_dead = s.IsDead
				};
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
