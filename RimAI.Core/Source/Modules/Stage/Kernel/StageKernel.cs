using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Stage.Kernel
{
    internal sealed class ActResourceClaim
    {
        public IReadOnlyList<string> ConvKeys { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> ParticipantIds { get; set; } = Array.Empty<string>();
        public string MapId { get; set; } = null;
        public bool Exclusive { get; set; } = true;
    }

    internal sealed class StageTicket
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string ConvKey { get; set; } = string.Empty;
        public IReadOnlyList<string> Participants { get; set; } = Array.Empty<string>();
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddSeconds(15);
    }

    internal sealed class ResultSnapshot
    {
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
        public string Text { get; set; } = string.Empty;
    }

    internal interface IStageKernel
    {
        bool TryReserve(ActResourceClaim claim, out StageTicket ticket, TimeSpan? ttl = null);
        void ExtendLease(StageTicket ticket, TimeSpan ttl);
        void Release(StageTicket ticket);

        bool IsBusyByConvKey(string convKey);
        bool IsBusyByParticipant(string participantId);

        Task<bool> CoalesceWithin(string convKey, int windowMs, Func<Task<bool>> leaderWork, CancellationToken ct = default);

        bool IsInCooldown(string convKey);
        void SetCooldown(string convKey, TimeSpan cooldown);

        bool IdempotencyTryGet(string key, out ResultSnapshot snapshot);
        void IdempotencySet(string key, ResultSnapshot snapshot, TimeSpan ttl);
    }

    internal sealed class StageKernel : IStageKernel
    {
        private sealed class ActiveLease
        {
            public StageTicket Ticket;
            public ActResourceClaim Claim;
        }

        private readonly ConcurrentDictionary<string, ActiveLease> _activeByConvKey = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ActiveLease> _activeByTicket = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, int> _activeByParticipant = new(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<string, DateTime> _cooldown = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, ResultSnapshot> _idem = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, CoalesceBucket> _coalesce = new(StringComparer.Ordinal);

        private sealed class CoalesceBucket
        {
            public DateTime FirstSeenUtc;
            public int WindowMs;
            public SemaphoreSlim Gate = new(1, 1);
            public bool Executed;
        }

        public bool TryReserve(ActResourceClaim claim, out StageTicket ticket, TimeSpan? ttl = null)
        {
            ticket = null;
            if (claim == null) return false;
            var convKey = (claim.ConvKeys?.FirstOrDefault()) ?? string.Empty;
            var participants = claim.ParticipantIds ?? Array.Empty<string>();

            // 基本互斥：同一 convKey，或参与者交集
            if (!string.IsNullOrWhiteSpace(convKey) && _activeByConvKey.ContainsKey(convKey)) return false;
            foreach (var pid in participants)
            {
                if (string.IsNullOrWhiteSpace(pid)) continue;
                if (_activeByParticipant.ContainsKey(pid)) return false;
            }

            var tk = new StageTicket
            {
                ConvKey = convKey,
                Participants = participants,
                ExpiresAtUtc = DateTime.UtcNow + (ttl ?? TimeSpan.FromSeconds(15))
            };
            var lease = new ActiveLease { Ticket = tk, Claim = claim };

            if (!string.IsNullOrWhiteSpace(convKey))
            {
                if (!_activeByConvKey.TryAdd(convKey, lease)) return false;
            }
            foreach (var pid in participants)
            {
                if (string.IsNullOrWhiteSpace(pid)) continue;
                _activeByParticipant.AddOrUpdate(pid, 1, (_, v) => v + 1);
            }
            _activeByTicket[tk.Id] = lease;
            return true;
        }

        public void ExtendLease(StageTicket ticket, TimeSpan ttl)
        {
            if (ticket == null) return;
            if (_activeByTicket.TryGetValue(ticket.Id, out var lease))
            {
                lease.Ticket.ExpiresAtUtc = DateTime.UtcNow + (ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : ttl);
            }
        }

        public void Release(StageTicket ticket)
        {
            if (ticket == null) return;
            if (_activeByTicket.TryRemove(ticket.Id, out var lease))
            {
                if (!string.IsNullOrWhiteSpace(lease?.Ticket?.ConvKey))
                {
                    _activeByConvKey.TryRemove(lease.Ticket.ConvKey, out _);
                }
                foreach (var pid in lease?.Ticket?.Participants ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(pid)) continue;
                    _activeByParticipant.AddOrUpdate(pid, 0, (_, v) => Math.Max(0, v - 1));
                    if (_activeByParticipant[pid] == 0) _activeByParticipant.TryRemove(pid, out _);
                }
            }
        }

        public bool IsBusyByConvKey(string convKey) => !string.IsNullOrWhiteSpace(convKey) && _activeByConvKey.ContainsKey(convKey);

        public bool IsBusyByParticipant(string participantId)
        {
            if (string.IsNullOrWhiteSpace(participantId)) return false;
            return _activeByParticipant.ContainsKey(participantId);
        }

        public async Task<bool> CoalesceWithin(string convKey, int windowMs, Func<Task<bool>> leaderWork, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return await leaderWork();
            var bucket = _coalesce.GetOrAdd(convKey, _ => new CoalesceBucket { FirstSeenUtc = DateTime.UtcNow, WindowMs = Math.Max(0, windowMs) });
            try
            {
                // 等待窗口结束
                var delay = bucket.WindowMs;
                if ((DateTime.UtcNow - bucket.FirstSeenUtc).TotalMilliseconds < delay)
                {
                    try { await Task.Delay(delay, ct); } catch { }
                }

                await bucket.Gate.WaitAsync(ct);
                try
                {
                    if (bucket.Executed) return false; // 已有 leader 执行
                    var ok = await leaderWork();
                    bucket.Executed = true;
                    return ok;
                }
                finally
                {
                    try { bucket.Gate.Release(); } catch { }
                }
            }
            finally
            {
                _coalesce.TryRemove(convKey, out _);
            }
        }

        public bool IsInCooldown(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return false;
            if (_cooldown.TryGetValue(convKey, out var last))
            {
                return (DateTime.UtcNow - last).TotalMilliseconds < _lastCooldownMs;
            }
            return false;
        }

        public void SetCooldown(string convKey, TimeSpan cooldown)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return;
            _lastCooldownMs = (int)Math.Max(0, cooldown.TotalMilliseconds);
            _cooldown[convKey] = DateTime.UtcNow;
        }

        private int _lastCooldownMs = 0;

        public bool IdempotencyTryGet(string key, out ResultSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(key)) { snapshot = null; return false; }
            if (_idem.TryGetValue(key, out var snap))
            {
                if (DateTime.UtcNow - snap.CompletedUtc < _idemTtl)
                {
                    snapshot = snap; return true;
                }
            }
            snapshot = null; return false;
        }

        public void IdempotencySet(string key, ResultSnapshot snapshot, TimeSpan ttl)
        {
            if (string.IsNullOrWhiteSpace(key) || snapshot == null) return;
            _idem[key] = snapshot;
            _idemTtl = ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : ttl;
        }

        private TimeSpan _idemTtl = TimeSpan.FromSeconds(30);
    }
}


