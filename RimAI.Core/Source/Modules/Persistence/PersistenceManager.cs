using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.History.Models;
using System.Linq;

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
			var snap = svc?.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
			// 从 P8 的内存服务同步导出 History 与 Recap 到快照
			try
			{
				var container = RimAI.Core.Source.Boot.RimAICoreMod.Container;
				var history = container.Resolve<IHistoryService>();
				var recap = container.Resolve<IRecapService>();
				if (history != null)
				{
					// Conversations
					snap.History.Conversations.Clear();
					foreach (var ck in history.GetAllConvKeys())
					{
						var parts = history.GetParticipantsOrEmpty(ck) ?? System.Array.Empty<string>();
						var entries = history.GetAllEntriesAsync(ck).GetAwaiter().GetResult();
						var rec = new ConversationRecord { ParticipantIds = parts.ToList(), Entries = new System.Collections.Generic.List<ConversationEntry>() };
						foreach (var e in entries)
						{
							rec.Entries.Add(new ConversationEntry
							{
								Role = e.Role == History.Models.EntryRole.User ? "user" : "ai",
								Text = e.Content,
								CreatedAtTicksUtc = e.Timestamp.Ticks,
								TurnOrdinal = e.TurnOrdinal
							});
						}
						snap.History.Conversations[ck] = rec; // 使用 convKey 作为 convId
					}
					// Indexes
					snap.History.ConvKeyIndex.Clear();
					snap.History.ParticipantIndex.Clear();
					foreach (var kv in snap.History.Conversations)
					{
						var convId = kv.Key;
						var parts2 = kv.Value.ParticipantIds ?? new System.Collections.Generic.List<string>();
						var convKey = string.Join("|", parts2.OrderBy(x => x));
						if (!snap.History.ConvKeyIndex.TryGetValue(convKey, out var list1)) { list1 = new System.Collections.Generic.List<string>(); snap.History.ConvKeyIndex[convKey] = list1; }
						if (!list1.Contains(convId)) list1.Add(convId);
						foreach (var pid in parts2)
						{
							if (!snap.History.ParticipantIndex.TryGetValue(pid, out var list2)) { list2 = new System.Collections.Generic.List<string>(); snap.History.ParticipantIndex[pid] = list2; }
							if (!list2.Contains(convId)) list2.Add(convId);
						}
					}
				}
				if (recap != null && history != null)
				{
					var export = recap.ExportSnapshot();
					snap.Recap.Recaps.Clear();
					foreach (var ck in history.GetAllConvKeys())
					{
						var list = recap.GetRecaps(ck);
						var target = new System.Collections.Generic.List<RecapSnapshotItem>();
						foreach (var r in list)
						{
							target.Add(new RecapSnapshotItem
							{
								Id = r.Id,
								Text = r.Text,
								CreatedAtTicksUtc = r.CreatedAt.Ticks,
								IdempotencyKey = r.IdempotencyKey,
								FromTurnExclusive = r.FromTurnExclusive,
								ToTurnInclusive = r.ToTurnInclusive,
								Mode = r.Mode.ToString()
							});
						}
						snap.Recap.Recaps[ck] = target;
					}
				}
			}
			catch { }
			return snap;
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

			// 将 History 与 Recap 快照回灌到 P8 内存服务，并重算水位
			try
			{
				var container = RimAI.Core.Source.Boot.RimAICoreMod.Container;
				var recap = container.Resolve<IRecapService>();
				var history = container.Resolve<IHistoryService>();
				if (recap != null && history != null && snapshot != null)
				{
					// History
					foreach (var kv in snapshot.History.Conversations)
					{
						var convId = kv.Key;
						var parts = kv.Value?.ParticipantIds ?? new System.Collections.Generic.List<string>();
						var convKey = string.Join("|", parts.OrderBy(x => x));
						try { history.UpsertParticipantsAsync(convKey, parts).GetAwaiter().GetResult(); } catch { }
						var entries = kv.Value?.Entries ?? new System.Collections.Generic.List<ConversationEntry>();
						foreach (var e in entries)
						{
							if (string.Equals(e.Role, "user", System.StringComparison.OrdinalIgnoreCase))
								history.AppendUserAsync(convKey, e.Text).GetAwaiter().GetResult();
							else
								history.AppendAiFinalAsync(convKey, e.Text).GetAwaiter().GetResult();
						}
					}

					var temp = new RecapSnapshot();
					foreach (var kv in snapshot.Recap.Recaps)
					{
						var list = new System.Collections.Generic.List<RimAI.Core.Source.Modules.History.Models.RecapItem>();
						foreach (var r in kv.Value)
						{
							list.Add(new RimAI.Core.Source.Modules.History.Models.RecapItem
							{
								Id = r.Id,
								ConvKey = kv.Key,
								Mode = string.Equals(r.Mode, "Replace", System.StringComparison.OrdinalIgnoreCase) ? RecapMode.Replace : RecapMode.Append,
								Text = r.Text,
								MaxChars = 1200,
								FromTurnExclusive = r.FromTurnExclusive,
								ToTurnInclusive = r.ToTurnInclusive,
								Stale = false,
								IdempotencyKey = r.IdempotencyKey,
								CreatedAt = new System.DateTime(r.CreatedAtTicksUtc, System.DateTimeKind.Utc),
								UpdatedAt = new System.DateTime(r.CreatedAtTicksUtc, System.DateTimeKind.Utc)
							});
						}
						temp.Items[kv.Key] = list;
					}
					recap.ImportSnapshot(temp);
				}
			}
			catch { }
		}
	}
}


