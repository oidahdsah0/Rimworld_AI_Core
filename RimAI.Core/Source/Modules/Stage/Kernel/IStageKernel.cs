using System;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Kernel
{
	internal interface IStageKernel
	{
		bool TryReserve(ActResourceClaim claim, out StageTicket ticket);
		void ExtendLease(StageTicket ticket, TimeSpan ttl);
		void Release(StageTicket ticket);
		bool IsBusyByConvKey(string convKey);
		bool IsBusyByParticipant(string participantId);
		Task<bool> CoalesceWithinAsync(string convKey, int windowMs, Func<Task<bool>> leaderWork);
		bool IsInCooldown(string key);
		void SetCooldown(string key, TimeSpan cooldown);
		bool IdempotencyTryGet(string key, out ActResult result);
		void IdempotencySet(string key, ActResult result, TimeSpan ttl);
		System.Collections.Generic.IReadOnlyList<StageTicket> GetRunningTickets();
		void ForceRelease(string ticketId);
		void ClearIdempotencyCache();
	}
}


