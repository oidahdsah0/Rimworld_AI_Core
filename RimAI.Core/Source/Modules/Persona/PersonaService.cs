using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persona
{
	internal sealed class PersonaService : IPersonaService
	{
		private readonly IPersistenceService _persistence;
		private readonly ConfigurationService _cfg;

		private readonly object _gate = new object();
		private readonly Dictionary<string, PersonaRecordSnapshot> _cache = new Dictionary<string, PersonaRecordSnapshot>();

		public event Action<string, string[]> OnPersonaUpdated;

		public PersonaService(IPersistenceService persistence, IConfigurationService cfg)
		{
			_persistence = persistence;
			_cfg = cfg as ConfigurationService;
		}

		public PersonaRecordSnapshot Get(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return new PersonaRecordSnapshot { EntityId = entityId ?? string.Empty };
			lock (_gate)
			{
				if (_cache.TryGetValue(entityId, out var found)) return Clone(found);
				var snap = ReadFromPersistence(entityId);
				_cache[entityId] = snap;
				return Clone(snap);
			}
		}

		public void Upsert(string entityId, Action<PersonaRecordEditor> edit)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			var editor = new PersonaRecordEditor();
			var changed = new List<string>();
			// wire actions
			editor.SetJobAction = (name, desc) => { SetJob(entityId, name, desc); changed.Add("job"); };
			editor.SetFixedPromptAction = text => { SetFixed(entityId, text); changed.Add("fixed"); };
			editor.UpsertBiographyAction = item => { UpsertBio(entityId, item); changed.Add("bio"); };
			editor.RemoveBiographyAction = id => { RemoveBio(entityId, id); changed.Add("bio"); };
			editor.SetIdeologyAction = s => { SetIdeology(entityId, s); changed.Add("ideology"); };
			// apply
			edit?.Invoke(editor);
			// fire event
			var handler = OnPersonaUpdated;
			if (handler != null && changed.Count > 0) handler.Invoke(entityId, changed.Distinct().ToArray());
		}

		public void Delete(string entityId)
		{
			lock (_gate)
			{
				_cache.Remove(entityId);
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				snap.FixedPrompts.Items.Remove(entityId);
				snap.Biographies.Items.Remove(entityId);
				snap.PersonalBeliefs.Items.Remove(entityId);
				snap.PersonaJob.Items.Remove(entityId);
				_persistence.ReplaceLastSnapshotForDebug(snap);
			}
			OnPersonaUpdated?.Invoke(entityId, new[] { "delete" });
		}

		public string ComposePersonaBlock(string entityId, PersonaComposeOptions options, out PersonaComposeAudit audit)
		{
			var budget = _cfg?.GetInternal()?.Persona?.Budget;
			var locale = options?.Locale ?? _cfg?.GetInternal()?.Persona?.Locale ?? "en";
			var o = options ?? new PersonaComposeOptions();
			if (budget != null)
			{
				o.MaxTotalChars = budget.MaxTotalChars;
				o.MaxJobChars = budget.Job;
				o.MaxFixedChars = budget.Fixed;
				o.MaxIdeologySegment = budget.IdeologySegment;
				o.MaxBioPerItem = budget.BiographyPerItem;
				o.MaxBioItems = budget.BiographyMaxItems;
			}
			var rec = Get(entityId) ?? new PersonaRecordSnapshot { EntityId = entityId, Locale = locale };
			var sb = new System.Text.StringBuilder();
			var segments = new List<(string seg, int len, bool truncated)>();
			void AppendCapped(string label, string text, int cap)
			{
				if (string.IsNullOrWhiteSpace(text)) return;
				var t = text.Trim();
				var truncated = false;
				if (t.Length > cap) { t = t.Substring(0, cap); truncated = true; }
				sb.AppendLine(label);
				sb.AppendLine(t);
				sb.AppendLine();
				segments.Add((label, t.Length, truncated));
			}
			if (o.IncludeJob && rec.Job != null && (!string.IsNullOrWhiteSpace(rec.Job.Name) || !string.IsNullOrWhiteSpace(rec.Job.Description)))
			{
				var header = locale.StartsWith("zh") ? "[职务]" : "[Job]";
				AppendCapped(header, $"{rec.Job.Name}：{rec.Job.Description}".Trim('：'), o.MaxJobChars);
			}
			if (o.IncludeFixedPrompts && rec.FixedPrompts != null && !string.IsNullOrWhiteSpace(rec.FixedPrompts.Text))
			{
				var header = locale.StartsWith("zh") ? "[固定提示词]" : "[Fixed Prompts]";
				AppendCapped(header, rec.FixedPrompts.Text, o.MaxFixedChars);
			}
			if (o.IncludeIdeology && rec.Ideology != null)
			{
				var header = locale.StartsWith("zh") ? "[意识形态]" : "[Ideology]";
				sb.AppendLine(header);
				void Line(string name, string text) { if (!string.IsNullOrWhiteSpace(text)) { var t = text.Trim(); var truncated = false; if (t.Length > o.MaxIdeologySegment) { t = t.Substring(0, o.MaxIdeologySegment); truncated = true; } var line = (locale.StartsWith("zh") ? name + "：" : name + ": ") + t; sb.AppendLine(line); segments.Add((name, t.Length, truncated)); } }
				Line(locale.StartsWith("zh") ? "世界观" : "Worldview", rec.Ideology.Worldview);
				Line(locale.StartsWith("zh") ? "价值观" : "Values", rec.Ideology.Values);
				Line(locale.StartsWith("zh") ? "行为准则" : "Code of Conduct", rec.Ideology.CodeOfConduct);
				Line(locale.StartsWith("zh") ? "性格特质" : "Traits", rec.Ideology.TraitsText);
				sb.AppendLine();
			}
			if (o.IncludeBiography && rec.Biography != null && rec.Biography.Count > 0)
			{
				var header = locale.StartsWith("zh") ? "[人物传记]" : "[Biography]";
				sb.AppendLine(header);
				int count = 0;
				foreach (var b in rec.Biography.Take(Math.Max(0, o.MaxBioItems)))
				{
					var t = b.Text ?? string.Empty; var truncated = false; if (t.Length > o.MaxBioPerItem) { t = t.Substring(0, o.MaxBioPerItem); truncated = true; }
					sb.AppendLine("- " + t);
					segments.Add(("bio", t.Length, truncated));
					count++;
				}
				sb.AppendLine();
			}
			var block = sb.ToString();
			if (block.Length > o.MaxTotalChars)
			{
				block = block.Substring(0, o.MaxTotalChars);
			}
			audit = new PersonaComposeAudit { TotalChars = block.Length, Segments = segments };
			return block.Trim();
		}

		private PersonaRecordSnapshot ReadFromPersistence(string entityId)
		{
			var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
			var rec = new PersonaRecordSnapshot { EntityId = entityId, Locale = _cfg?.GetInternal()?.Persona?.Locale ?? "en" };
			if (snap.FixedPrompts.Items.TryGetValue(entityId, out var fp))
			{
				rec.FixedPrompts = new FixedPromptSnapshot { Text = fp, UpdatedAtUtc = DateTime.UtcNow };
			}
			if (snap.Biographies.Items.TryGetValue(entityId, out var list) && list != null)
			{
				rec.Biography = list.Select(x => new BiographyItem { Id = x.Id, Text = x.Text, Source = x.Source, UpdatedAtUtc = new DateTime(x.CreatedAtTicksUtc, DateTimeKind.Utc) }).ToList();
			}
			if (snap.PersonalBeliefs.Items.TryGetValue(entityId, out var bel) && bel != null)
			{
				rec.Ideology = new IdeologySnapshot { Worldview = bel.Worldview, Values = bel.Values, CodeOfConduct = bel.CodeOfConduct, TraitsText = bel.TraitsText, UpdatedAtUtc = DateTime.UtcNow };
			}
			// PersonaJobSnapshot 从新节点读取
			if (snap.PersonaJob.Items.TryGetValue(entityId, out var job) && job != null)
			{
				rec.Job = new PersonaJobSnapshot { Name = job.Name, Description = job.Description, UpdatedAtUtc = new DateTime(job.UpdatedAtTicksUtc, DateTimeKind.Utc) };
			}
			return rec;
		}

		private void SetJob(string entityId, string name, string description)
		{
			lock (_gate)
			{
				if (!_cache.TryGetValue(entityId, out var rec)) rec = new PersonaRecordSnapshot { EntityId = entityId };
				rec.Job = new PersonaJobSnapshot { Name = name ?? string.Empty, Description = description ?? string.Empty, UpdatedAtUtc = DateTime.UtcNow };
				_cache[entityId] = rec;
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				snap.PersonaJob.Items[entityId] = new PersonaJob { Name = rec.Job.Name ?? string.Empty, Description = rec.Job.Description ?? string.Empty, UpdatedAtTicksUtc = rec.Job.UpdatedAtUtc.Ticks };
				_persistence.ReplaceLastSnapshotForDebug(snap);
			}
		}

		private void SetFixed(string entityId, string text)
		{
			lock (_gate)
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				snap.FixedPrompts.Items[entityId] = text ?? string.Empty;
				_persistence.ReplaceLastSnapshotForDebug(snap);
				if (!_cache.TryGetValue(entityId, out var rec)) rec = new PersonaRecordSnapshot { EntityId = entityId };
				rec.FixedPrompts = new FixedPromptSnapshot { Text = text ?? string.Empty, UpdatedAtUtc = DateTime.UtcNow };
				_cache[entityId] = rec;
			}
		}

		private void UpsertBio(string entityId, BiographyItem item)
		{
			lock (_gate)
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				if (!snap.Biographies.Items.TryGetValue(entityId, out var list) || list == null)
				{
					list = new List<RimAI.Core.Source.Modules.Persistence.Snapshots.BiographyItem>();
					snap.Biographies.Items[entityId] = list;
				}
				var found = list.FirstOrDefault(x => x.Id == item.Id);
				if (found == null) list.Add(new RimAI.Core.Source.Modules.Persistence.Snapshots.BiographyItem { Id = item.Id ?? Guid.NewGuid().ToString("N"), Text = item.Text ?? string.Empty, Source = item.Source ?? string.Empty, CreatedAtTicksUtc = DateTime.UtcNow.Ticks });
				else { found.Text = item.Text ?? string.Empty; found.Source = item.Source ?? string.Empty; found.CreatedAtTicksUtc = DateTime.UtcNow.Ticks; }
				_persistence.ReplaceLastSnapshotForDebug(snap);
				if (!_cache.TryGetValue(entityId, out var rec)) rec = new PersonaRecordSnapshot { EntityId = entityId };
				rec.Biography = list.Select(x => new BiographyItem { Id = x.Id, Text = x.Text, Source = x.Source, UpdatedAtUtc = new DateTime(x.CreatedAtTicksUtc, DateTimeKind.Utc) }).ToList();
				_cache[entityId] = rec;
			}
		}

		private void RemoveBio(string entityId, string id)
		{
			lock (_gate)
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				if (snap.Biographies.Items.TryGetValue(entityId, out var list) && list != null)
				{
					list.RemoveAll(x => x.Id == id);
					_persistence.ReplaceLastSnapshotForDebug(snap);
					if (!_cache.TryGetValue(entityId, out var rec)) rec = new PersonaRecordSnapshot { EntityId = entityId };
					rec.Biography = list.Select(x => new BiographyItem { Id = x.Id, Text = x.Text, Source = x.Source, UpdatedAtUtc = new DateTime(x.CreatedAtTicksUtc, DateTimeKind.Utc) }).ToList();
					_cache[entityId] = rec;
				}
			}
		}

		private void SetIdeology(string entityId, IdeologySnapshot s)
		{
			lock (_gate)
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				snap.PersonalBeliefs.Items[entityId] = new PersonalBeliefs { Worldview = s?.Worldview ?? string.Empty, Values = s?.Values ?? string.Empty, CodeOfConduct = s?.CodeOfConduct ?? string.Empty, TraitsText = s?.TraitsText ?? string.Empty };
				_persistence.ReplaceLastSnapshotForDebug(snap);
				if (!_cache.TryGetValue(entityId, out var rec)) rec = new PersonaRecordSnapshot { EntityId = entityId };
				rec.Ideology = new IdeologySnapshot { Worldview = s?.Worldview ?? string.Empty, Values = s?.Values ?? string.Empty, CodeOfConduct = s?.CodeOfConduct ?? string.Empty, TraitsText = s?.TraitsText ?? string.Empty, UpdatedAtUtc = DateTime.UtcNow };
				_cache[entityId] = rec;
			}
		}

		private static PersonaRecordSnapshot Clone(PersonaRecordSnapshot s)
		{
			if (s == null) return null;
			return new PersonaRecordSnapshot
			{
				EntityId = s.EntityId,
				Locale = s.Locale,
				Job = s.Job == null ? null : new PersonaJobSnapshot { Name = s.Job.Name, Description = s.Job.Description, UpdatedAtUtc = s.Job.UpdatedAtUtc },
				FixedPrompts = s.FixedPrompts == null ? null : new FixedPromptSnapshot { Text = s.FixedPrompts.Text, UpdatedAtUtc = s.FixedPrompts.UpdatedAtUtc },
				Ideology = s.Ideology == null ? null : new IdeologySnapshot { Worldview = s.Ideology.Worldview, Values = s.Ideology.Values, CodeOfConduct = s.Ideology.CodeOfConduct, TraitsText = s.Ideology.TraitsText, UpdatedAtUtc = s.Ideology.UpdatedAtUtc },
				Biography = s.Biography == null ? null : s.Biography.Select(x => new BiographyItem { Id = x.Id, Text = x.Text, Source = x.Source, UpdatedAtUtc = x.UpdatedAtUtc }).ToList()
			};
		}
	}
}


