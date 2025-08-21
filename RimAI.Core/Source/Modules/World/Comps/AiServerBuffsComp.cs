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
		public int randomAttributeCount = 0;
		public int randomAttributePercent = 0;

		public CompProperties_AiServerBuffs()
		{
			this.compClass = typeof(Comp_AiServerBuffs);
		}
	}

	public sealed class Comp_AiServerBuffs : ThingComp
	{
		private readonly string[] _candidateStatNames = new[]
		{
			"ConstructionSpeed",
			"MiningSpeed",
			"PlantWorkSpeed",
			"ResearchSpeedFactor",
			"MedicalOperationSpeed",
			"MoveSpeed"
		};

		private List<string> _pickedStatNames; // persisted
		private int _seed; // persisted

		public CompProperties_AiServerBuffs Props => (CompProperties_AiServerBuffs)props;

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			EnsurePickedStats();
		}

		public override void PostExposeData()
		{
			base.PostExposeData();
			Scribe_Collections.Look(ref _pickedStatNames, "rimai_server_picked_stats", LookMode.Value);
			Scribe_Values.Look(ref _seed, "rimai_server_seed", 0);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				EnsurePickedStats();
			}
		}

		private void EnsurePickedStats()
		{
			try
			{
				if (_pickedStatNames == null) _pickedStatNames = new List<string>();
				int need = Math.Max(0, Props?.randomAttributeCount ?? 0);
				if (_pickedStatNames.Count == need) return;
				var src = _candidateStatNames.ToList();
				_pickedStatNames.Clear();
				if (_seed == 0) _seed = Gen.HashCombineInt(parent?.thingIDNumber ?? 0, (int)Find.TickManager.TicksAbs);
				var rng = new System.Random(_seed);
				for (int i = 0; i < need && src.Count > 0; i++)
				{
					int idx = rng.Next(0, src.Count);
					_pickedStatNames.Add(src[idx]);
					src.RemoveAt(idx);
				}
			}
			catch { }
		}

		public override string CompInspectStringExtra()
		{
			try
			{
				var sb = new StringBuilder();
				if (Props != null && Props.baseGlobalWorkSpeedPercent > 0)
				{
					var stat = DefDatabase<StatDef>.GetNamedSilentFail("WorkSpeedGlobal");
					var statLabel = stat != null ? stat.label : "Global work speed";
					sb.AppendLine(string.Format("{0} +{1}%", statLabel.CapitalizeFirst(), Props.baseGlobalWorkSpeedPercent));
				}
				if ((Props?.randomAttributeCount ?? 0) > 0 && (Props?.randomAttributePercent ?? 0) > 0)
				{
					EnsurePickedStats();
					foreach (var name in _pickedStatNames ?? Enumerable.Empty<string>())
					{
						var st = DefDatabase<StatDef>.GetNamedSilentFail(name);
						var label = st != null ? st.label : name;
						sb.AppendLine(string.Format("{0} +{1}%", label.CapitalizeFirst(), Props.randomAttributePercent));
					}
				}
				var txt = sb.ToString().TrimEnd();
				return string.IsNullOrWhiteSpace(txt) ? null : txt;
			}
			catch { return null; }
		}
	}
}


