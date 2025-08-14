using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Kernel
{
	internal sealed class StageKernel : IStageKernel
	{
		private sealed class RunningTicket
		{
			public StageTicket Ticket { get; set; }
			public Timer LeaseTimer { get; set; }
		}

		private readonly ConcurrentDictionary<string, RunningTicket> _runningByTicket = new();
		private readonly ConcurrentDictionary<string, string> _runningByConvKey = new();
		private readonly ConcurrentDictionary<string, int> _runningByParticipant = new();
		private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();
		private readonly ConcurrentDictionary<string, (ActResult result, DateTime expiresUtc)> _idem = new();
		private readonly int _maxRunning;
		private readonly Timer _sweeper;

		public StageKernel()
		{
			_maxRunning = 4; // 默认值，可未来通过配置注入
			_sweeper = new Timer(SweepExpired, null, 1000, 1000);
		}

		public bool TryReserve(ActResourceClaim claim, out StageTicket ticket)
		{
			ticket = null;
			// 全局并发上限
			if (_runningByTicket.Count >= _maxRunning) return false;

			// convKey 强互斥（至少包含 primary）
			var primaryConvKey = claim.ConvKeys?.FirstOrDefault();
			if (!string.IsNullOrEmpty(primaryConvKey) && _runningByConvKey.ContainsKey(primaryConvKey)) return false;

			// 参与者集合互斥
			if (claim.ParticipantIds != null)
			{
				foreach (var pid in claim.ParticipantIds)
				{
					if (_runningByParticipant.ContainsKey(pid)) return false;
				}
			}

			// 批准：颁发票据
			ticket = new StageTicket
			{
				Id = Guid.NewGuid().ToString("N"),
				ConvKey = primaryConvKey ?? string.Empty,
				ParticipantIds = claim.ParticipantIds ?? Array.Empty<string>(),
				ExpiresAtUtc = DateTime.UtcNow.AddSeconds(10)
			};

			_runningByTicket[ticket.Id] = new RunningTicket { Ticket = ticket };
			if (!string.IsNullOrEmpty(ticket.ConvKey)) _runningByConvKey[ticket.ConvKey] = ticket.Id;
			foreach (var pid in ticket.ParticipantIds) _runningByParticipant.AddOrUpdate(pid, 1, (_, v) => v + 1);
			return true;
		}

		public void ExtendLease(StageTicket ticket, TimeSpan ttl)
		{
			if (ticket == null) return;
			if (_runningByTicket.TryGetValue(ticket.Id, out var rt))
			{
				var newExp = DateTime.UtcNow.Add(ttl);
				rt.Ticket.ExpiresAtUtc = newExp;
			}
		}

		public void Release(StageTicket ticket)
		{
			if (ticket == null) return;
			RunningTicket _ignoredRt;
			if (_runningByTicket.TryRemove(ticket.Id, out _ignoredRt))
			{
				string _ignoredStr;
				if (!string.IsNullOrEmpty(ticket.ConvKey)) _runningByConvKey.TryRemove(ticket.ConvKey, out _ignoredStr);
				foreach (var pid in ticket.ParticipantIds)
				{
					_runningByParticipant.AddOrUpdate(pid, 0, (_, v) => Math.Max(0, v - 1));
				}
			}
		}

		public bool IsBusyByConvKey(string convKey) => !string.IsNullOrEmpty(convKey) && _runningByConvKey.ContainsKey(convKey);
		public bool IsBusyByParticipant(string participantId) => _runningByParticipant.TryGetValue(participantId, out var n) && n > 0;

		public async Task<bool> CoalesceWithinAsync(string convKey, int windowMs, Func<Task<bool>> leaderWork)
		{
			if (string.IsNullOrEmpty(convKey) || windowMs <= 0) return await leaderWork();
			var key = "coalesce:" + convKey;
			var start = DateTime.UtcNow;
			var end = start.AddMilliseconds(windowMs);
			// 简化实现：第一个进入者执行 leaderWork；后续在窗口内视为合流并返回 true
			var first = false;
			lock (_cooldowns)
			{
				if (!_cooldowns.ContainsKey(key)) { _cooldowns[key] = end; first = true; }
			}
			if (first)
			{
				try { return await leaderWork(); }
				finally { DateTime _tmp; _cooldowns.TryRemove(key, out _tmp); }
			}
			else
			{
				return true; // 被合流者视为已完成
			}
		}

		public bool IsInCooldown(string key)
		{
			if (string.IsNullOrEmpty(key)) return false;
			if (_cooldowns.TryGetValue(key, out var until))
			{
				if (DateTime.UtcNow < until) return true;
				DateTime _tmp; _cooldowns.TryRemove(key, out _tmp);
			}
			return false;
		}

		public void SetCooldown(string key, TimeSpan cooldown)
		{
			if (string.IsNullOrEmpty(key)) return;
			_cooldowns[key] = DateTime.UtcNow.Add(cooldown);
		}

		public bool IdempotencyTryGet(string key, out ActResult result)
		{
			result = null;
			if (string.IsNullOrEmpty(key)) return false;
			if (_idem.TryGetValue(key, out var v))
			{
				if (DateTime.UtcNow <= v.expiresUtc)
				{
					result = v.result; return true;
				}
				else _idem.TryRemove(key, out _);
			}
			return false;
		}

		public void IdempotencySet(string key, ActResult result, TimeSpan ttl)
		{
			if (string.IsNullOrEmpty(key) || result == null) return;
			_idem[key] = (result, DateTime.UtcNow.Add(ttl));
		}

		internal static string ComputeIdempotencyKey(string actName, string convKey, string scenario, string seed)
		{
			var s = (actName ?? string.Empty) + "|" + (convKey ?? string.Empty) + "|" + (scenario ?? string.Empty) + "|" + (seed ?? string.Empty);
			using var sha = SHA256.Create();
			var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
			return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
		}

		private void SweepExpired(object _)
		{
			try
			{
				var now = DateTime.UtcNow;
				foreach (var kv in _runningByTicket.ToArray())
				{
					var t = kv.Value.Ticket;
					if (t != null && t.ExpiresAtUtc <= now)
					{
						Release(t);
					}
				}
				// 清理过期的幂等项
				foreach (var kv in _idem.ToArray())
				{
					if (kv.Value.expiresUtc <= now) { var _tmp = default((ActResult result, DateTime expiresUtc)); _idem.TryRemove(kv.Key, out _tmp); }
				}
				// 清理过期冷却标记
				foreach (var kv in _cooldowns.ToArray())
				{
					if (kv.Value <= now) { DateTime _tmp; _cooldowns.TryRemove(kv.Key, out _tmp); }
				}
			}
			catch { }
		}

		public IReadOnlyList<StageTicket> GetRunningTickets()
		{
			return _runningByTicket.Values.Select(v => v.Ticket).ToList();
		}

		public void ForceRelease(string ticketId)
		{
			if (string.IsNullOrEmpty(ticketId)) return;
			if (_runningByTicket.TryGetValue(ticketId, out var rt))
			{
				Release(rt.Ticket);
			}
		}

		public void ClearIdempotencyCache()
		{
			_idem.Clear();
		}
	}
}


