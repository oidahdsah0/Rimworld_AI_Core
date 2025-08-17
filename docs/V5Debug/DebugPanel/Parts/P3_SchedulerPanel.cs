using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Scheduler;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P3_SchedulerPanel
	{
		public static void Draw(Listing_Standard listing, ISchedulerService scheduler)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P3] Scheduler");
			Text.Font = GameFont.Small;
			listing.GapLine();

			if (listing.ButtonText("PingOnMainThread"))
			{
				_ = Task.Run(async () =>
				{
					var sw = Stopwatch.StartNew();
					var tid = await scheduler.ScheduleOnMainThreadAsync(() => Environment.CurrentManagedThreadId, name: "PingOnMainThread");
					sw.Stop();
					Log.Message($"[RimAI.Core][P3] PingOnMainThread ok tid={tid} elapsed={sw.Elapsed.TotalMilliseconds:F3} ms");
				});
			}

			if (listing.ButtonText("SpikeTest (N=1000)"))
			{
				_ = Task.Run(() =>
				{
					var N = 1000;
					for (int i = 0; i < N; i++)
					{
						int idx = i;
						scheduler.ScheduleOnMainThread(() => { var _ = idx * idx; }, name: "SpikeTest");
					}
					Log.Message($"[RimAI.Core][P3] SpikeTest enqueued {N}");
				});
			}

			// Metrics
			var gameComp = Current.Game?.GetComponent<RimAI.Core.Source.Infrastructure.Scheduler.SchedulerGameComponent>();
			var snap = gameComp?.GetSnapshot();
			if (snap != null)
			{
				listing.Label($"QueueLength={snap.QueueLength} | LastFrameProcessed={snap.LastFrameProcessed} | LastFrameMs={snap.LastFrameMs:F3} | LongTaskCount={snap.LongTaskCount} | TotalProcessed={snap.TotalProcessed}");
			}
		}
	}
}


