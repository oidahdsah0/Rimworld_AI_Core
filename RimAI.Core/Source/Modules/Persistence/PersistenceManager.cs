using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence
{
	internal sealed class PersistenceManager : GameComponent
	{
		public PersistenceManager(Game game) { }

		public override void ExposeData()
		{
			var svc = Resolve();
			if (svc == null) return;
			if (Scribe.mode == LoadSaveMode.Saving)
			{
				var snap = BuildSnapshotFromServices();
				// 若通过 Debug 导入了外部 JSON，则优先写入导入内容
				try
				{
					var json = svc.ExportAllToJson();
					if (!string.IsNullOrEmpty(json))
					{
						// no-op: 占位，便于后续集成导入缓冲到存档流程
					}
				}
				catch { }
				svc.SaveAll(snap);
			}
			else if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				var snap = svc.LoadAll();
				ApplySnapshotToServices(snap);
			}
		}

		private IPersistenceService Resolve()
		{
			try { return RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IPersistenceService>(); }
			catch { return null; }
		}


		private PersistenceSnapshot BuildSnapshotFromServices()
		{
			// 简化策略：直接使用 PersistenceService 维护的内存快照作为写入源
			var svc = Resolve();
			return svc?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
		}


		private void ApplySnapshotToServices(PersistenceSnapshot snapshot)
		{
			// 写回 Persona 门面缓存（原子 Upsert 保持一致性）
			try
			{
				var container = RimAI.Core.Source.Boot.RimAICoreMod.Container;
				var persona = container.Resolve<RimAI.Core.Source.Modules.Persona.IPersonaService>();
				if (persona != null && snapshot != null)
				{
					// Fixed Prompts
					foreach (var kv in snapshot.FixedPrompts.Items)
					{
						var id = kv.Key; var text = kv.Value;
						persona.Upsert(id, e => e.SetFixedPrompt(text));
					}
					// Biographies
					foreach (var kv in snapshot.Biographies.Items)
					{
						var id = kv.Key; var list = kv.Value;
						if (list == null) continue;
						foreach (var b in list)
						{
							persona.Upsert(id, e => e.AddOrUpdateBiography(b.Id, b.Text, b.Source));
						}
					}
					// Personal Beliefs (Ideology)
					foreach (var kv in snapshot.PersonalBeliefs.Items)
					{
						var id = kv.Key; var s = kv.Value;
						persona.Upsert(id, e => e.SetIdeology(s?.Worldview, s?.Values, s?.CodeOfConduct, s?.TraitsText));
					}
					// Persona Job
					foreach (var kv in snapshot.PersonaJob.Items)
					{
						var id = kv.Key; var j = kv.Value;
						persona.Upsert(id, e => e.SetJob(j?.Name, j?.Description));
					}
				}
			}
			catch { }
		}
	}
}


