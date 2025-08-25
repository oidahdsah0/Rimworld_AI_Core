using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimWorld;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
	internal sealed class ThreatScanPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public ThreatScanPart(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("ThreatScanPart requires ConfigurationService");
		}

		public Task<ThreatSnapshot> GetAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap ?? throw new WorldDataException("No current map");
				int hostile = 0, manhunters = 0, mechs = 0;
				foreach (var p in map.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
				{
					if (p == null) continue;
					if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
					{
						hostile++;
						if (p.RaceProps?.IsMechanoid == true) mechs++;
					}
					if (p.MentalStateDef == MentalStateDefOf.Manhunter || p.MentalStateDef == MentalStateDefOf.ManhunterPermanent)
					{
						manhunters++;
					}
				}
				float points = 0f; try { points = StorytellerUtility.DefaultThreatPointsNow(map); } catch { }
				string danger = string.Empty; try { danger = map.dangerWatcher?.DangerRating.ToString(); } catch { }
				float fire = 0f; try { fire = map.fireWatcher?.FireDanger ?? 0f; } catch { }
				float lastBigDays = 0f; try { lastBigDays = (Find.TickManager.TicksGame - map.storyState.LastThreatBigTick) / 60000f; } catch { }
				return new ThreatSnapshot { HostilePawns = hostile, Manhunters = manhunters, Mechanoids = mechs, ThreatPoints = points, DangerRating = danger, FireDanger = fire, LastBigThreatDaysAgo = lastBigDays };
			}, name: "ThreatScanPart.Get", ct: cts.Token);
		}
	}
}
