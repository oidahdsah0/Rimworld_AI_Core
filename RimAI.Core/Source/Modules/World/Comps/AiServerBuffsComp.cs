using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Comps
{
	public sealed class CompProperties_AiServerBuffs : CompProperties
	{
		public int baseGlobalWorkSpeedPercent = 0;
		public int randomAttributeCount = 0;      // 每台服务器随机影响的工作类属性数量（0 表示不启用）
		public int randomAttributePercent = 0;    // 每个随机属性的百分比加成（叠加到具体属性上）

		public CompProperties_AiServerBuffs()
		{
			this.compClass = typeof(Comp_AiServerBuffs);
		}
	}

	public sealed class Comp_AiServerBuffs : ThingComp
	{
		// 随机属性加成由 StatPart 基于服务器进行确定性选择并生效；本组件不持久化随机结果。

		public CompProperties_AiServerBuffs Props => (CompProperties_AiServerBuffs)props;

		public override void PostSpawnSetup(bool respawningAfterLoad) => base.PostSpawnSetup(respawningAfterLoad);

		public override void PostExposeData() => base.PostExposeData();

	// 随机属性加成逻辑在 StatPart 中处理，这里无需状态与保存

		public override string CompInspectStringExtra()
		{
			try
			{
				var sb = new StringBuilder();
				bool online = false;
				if (parent is Building b)
				{
					var powered = b.GetComp<CompPowerTrader>()?.PowerOn ?? false;
					var flick = b.GetComp<CompFlickable>();
					bool switchedOn = flick == null || flick.SwitchIsOn;
					var broken = b.GetComp<CompBreakdownable>()?.BrokenDown ?? false;
					online = powered && switchedOn && !broken;
				}
				sb.AppendLine($"AI服务器状态：{(online ? "在线" : "离线")}");
				if (Props != null && Props.baseGlobalWorkSpeedPercent > 0)
				{
					var stat = DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal");
					var statLabel = stat != null ? stat.label : "Global work speed";
					sb.AppendLine(string.Format("{0} +{1}%", statLabel.CapitalizeFirst(), Props.baseGlobalWorkSpeedPercent));
				}
				// 显示随机属性加成（与 StatPart 相同的确定性选择）：
				if (Props != null && Props.randomAttributeCount > 0 && Props.randomAttributePercent > 0)
				{
					try
					{
						var candidateNames = new List<string>
						{
							"GeneralLaborSpeed", "ConstructionSpeed", "MiningSpeed",
							"PlantWorkSpeed", "ResearchSpeed", "MedicalOperationSpeed", "MedicalTendSpeed"
						};
						int k = Math.Max(0, Props.randomAttributeCount);
						int pct = Math.Max(0, Props.randomAttributePercent);
						int seed = unchecked((parent.thingIDNumber * 397) ^ (parent.Position.GetHashCode() * 17) ^ (parent.def?.defName?.GetHashCode() ?? 0));
						var rng = new System.Random(seed);
						var shuffled = candidateNames.OrderBy(_ => rng.Next()).ToList();
						var picked = shuffled.Take(Math.Min(k, candidateNames.Count));

						foreach (var name in picked)
						{
							var sdef = DefDatabase<StatDef>.GetNamedSilentFail(name);
							var label = sdef != null ? sdef.label : name;
							sb.AppendLine(string.Format("{0} +{1}% (随机)", label.CapitalizeFirst(), pct));
						}
					}
					catch { }
				}
				var txt = sb.ToString().TrimEnd();
				return string.IsNullOrWhiteSpace(txt) ? null : txt;
			}
			catch { return null; }
		}
	}
}


