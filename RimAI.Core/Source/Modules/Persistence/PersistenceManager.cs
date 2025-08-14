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
			// 当前无领域服务实现，返回空快照占位；后续由各服务 ExportSnapshot() 补齐
			return new PersistenceSnapshot();
		}

		private void ApplySnapshotToServices(PersistenceSnapshot snapshot)
		{
			// 当前无领域服务实现，暂不写回；后续由各服务 ImportSnapshot(state) 补齐
		}
	}
}


