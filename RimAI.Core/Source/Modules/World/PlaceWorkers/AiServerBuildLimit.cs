using System;
using System.Linq;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.PlaceWorkers
{
	internal abstract class PlaceWorker_AIServerLimitBase : PlaceWorker
	{
		protected abstract int GetLimit();
		protected abstract string GetLimitMessageKey();

		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			try
			{
				if (map == null || checkingDef == null) return true;
				var buildingDef = checkingDef as ThingDef;
				if (buildingDef == null) return true;
				int current = CountExisting(map, buildingDef, thingToIgnore);
				int limit = Math.Max(0, GetLimit());
				if (current >= limit)
				{
					// 动态消息：本地图最多可建 {limit} 台 {label}（已存在 {current} 台）
					var label = buildingDef.label ?? buildingDef.defName;
					var msg = GetLimitMessageKey().Translate(limit.ToString(), label, current.ToString());
					return new AcceptanceReport(msg);
				}
				return true;
			}
			catch { return true; }
		}

		private static int CountExisting(Map map, ThingDef targetDef, Thing thingToIgnore)
		{
			int count = 0;
			try
			{
				// 已建成
				count += map.listerThings?.ThingsOfDef(targetDef)?.Count(t => t != null && t != thingToIgnore) ?? 0;
				// 蓝图
				var blueprints = map.listerThings?.ThingsInGroup(ThingRequestGroup.Blueprint) ?? null;
				if (blueprints != null)
				{
					foreach (var t in blueprints)
					{
						if (t == null || t == thingToIgnore) continue;
						var bp = t as Blueprint_Build;
						if (bp != null)
						{
							try { if ((bp.def?.entityDefToBuild as ThingDef) == targetDef) count++; } catch { }
						}
					}
				}
				// 框架（施工中）
				var frames = map.listerThings?.ThingsInGroup(ThingRequestGroup.BuildingFrame) ?? null;
				if (frames != null)
				{
					foreach (var t in frames)
					{
						if (t == null || t == thingToIgnore) continue;
						var f = t as Frame;
						if (f != null)
						{
							try { if ((f.def?.entityDefToBuild as ThingDef) == targetDef) count++; } catch { }
						}
					}
				}
			}
			catch { }
			return count;
		}
	}

	internal sealed class PlaceWorker_AIServerLimit_Lv1 : PlaceWorker_AIServerLimitBase
	{
		protected override int GetLimit() => 3;
		protected override string GetLimitMessageKey() => "RimAI.Building.LimitReached.Format";
	}

	internal sealed class PlaceWorker_AIServerLimit_Lv2 : PlaceWorker_AIServerLimitBase
	{
		protected override int GetLimit() => 2;
		protected override string GetLimitMessageKey() => "RimAI.Building.LimitReached.Format";
	}

	internal sealed class PlaceWorker_AIServerLimit_Lv3 : PlaceWorker_AIServerLimitBase
	{
		protected override int GetLimit() => 1;
		protected override string GetLimitMessageKey() => "RimAI.Building.LimitReached.Format";
	}
}


