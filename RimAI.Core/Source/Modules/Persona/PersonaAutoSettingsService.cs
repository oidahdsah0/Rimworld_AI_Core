using System;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persona
{
	/// <summary>
	/// 自动更新设置存储：使用 Persistence 的配置快照区简易持久化。
	/// </summary>
	internal sealed class PersonaAutoSettingsService : IPersonaAutoSettingsService
	{
		private readonly IPersistenceService _persistence;
		private readonly object _gate = new object();
		private Dictionary<string, string> _map; // key: entityId, value: "bio=0/1;ideo=0/1"
		private int _lastRunDay;
		private int _intervalDays = 15;
		private int _perPawnDelayMs = 60000;
		private int _maxRetries = 3;

		public PersonaAutoSettingsService(IPersistenceService persistence)
		{
			_persistence = persistence;
			Load();
		}

		private void Load()
		{
			try
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				_map = snap.PersonaBindings?.Items ?? new Dictionary<string, string>();
				if (_map.TryGetValue("__auto_last_day__", out var s) && int.TryParse(s, out var d)) _lastRunDay = d; else _lastRunDay = -1;
				if (_map.TryGetValue("__auto_interval_days__", out var s2) && int.TryParse(s2, out var d2)) _intervalDays = Math.Max(1, d2);
				if (_map.TryGetValue("__auto_per_pawn_delay_ms__", out var s3) && int.TryParse(s3, out var d3)) _perPawnDelayMs = Math.Max(0, d3);
				if (_map.TryGetValue("__auto_max_retries__", out var s4) && int.TryParse(s4, out var d4)) _maxRetries = Math.Max(1, d4);
			}
			catch { _map = new Dictionary<string, string>(); _lastRunDay = -1; }
		}

		private void Save()
		{
			try
			{
				var snap = _persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				if (snap.PersonaBindings == null) snap.PersonaBindings = new PersonaBindingsSnapshot();
				snap.PersonaBindings.Items = _map ?? new Dictionary<string, string>();
				_persistence.ReplaceLastSnapshotForDebug(snap);
			}
			catch { }
		}

		public bool GetAutoBio(string entityId)
		{
			lock (_gate) { return Parse(entityId).bio; }
		}
		public bool GetAutoIdeo(string entityId)
		{
			lock (_gate) { return Parse(entityId).ideo; }
		}
		public void SetAutoBio(string entityId, bool enabled)
		{
			lock (_gate)
			{
				var p = Parse(entityId);
				p.bio = enabled; _map[entityId ?? "-"] = Serialize(p);
				Save();
			}
		}
		public void SetAutoIdeo(string entityId, bool enabled)
		{
			lock (_gate)
			{
				var p = Parse(entityId);
				p.ideo = enabled; _map[entityId ?? "-"] = Serialize(p);
				Save();
			}
		}

		public int GetLastRunDay() { lock (_gate) return _lastRunDay; }
		public void SetLastRunDay(int day)
		{
			lock (_gate)
			{
				_lastRunDay = day;
				_map["__auto_last_day__"] = day.ToString();
				Save();
			}
		}

		public int GetIntervalDays() { lock (_gate) return _intervalDays; }
		public void SetIntervalDays(int days)
		{
			lock (_gate) { _intervalDays = Math.Max(1, days); _map["__auto_interval_days__"] = _intervalDays.ToString(); Save(); }
		}
		public int GetPerPawnDelayMs() { lock (_gate) return _perPawnDelayMs; }
		public void SetPerPawnDelayMs(int delayMs)
		{
			lock (_gate) { _perPawnDelayMs = Math.Max(0, delayMs); _map["__auto_per_pawn_delay_ms__"] = _perPawnDelayMs.ToString(); Save(); }
		}
		public int GetMaxRetries() { lock (_gate) return _maxRetries; }
		public void SetMaxRetries(int retries)
		{
			lock (_gate) { _maxRetries = Math.Max(1, retries); _map["__auto_max_retries__"] = _maxRetries.ToString(); Save(); }
		}

		private (bool bio, bool ideo) Parse(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return (false, false);
			if (_map == null) _map = new Dictionary<string, string>();
			if (!_map.TryGetValue(entityId, out var v) || string.IsNullOrWhiteSpace(v)) return (false, false);
			bool bio = v.IndexOf("bio=1", StringComparison.OrdinalIgnoreCase) >= 0;
			bool ideo = v.IndexOf("ideo=1", StringComparison.OrdinalIgnoreCase) >= 0;
			return (bio, ideo);
		}

		private static string Serialize((bool bio, bool ideo) p)
		{
			return $"bio={(p.bio ? 1 : 0)};ideo={(p.ideo ? 1 : 0)}";
		}
	}
}


