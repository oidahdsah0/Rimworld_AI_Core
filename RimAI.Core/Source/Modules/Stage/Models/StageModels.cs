using System;
using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Stage.Models
{
	internal sealed class StageIntent
	{
		public string ActName { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public string Origin { get; set; }
		public string ScenarioText { get; set; }
		public int? Priority { get; set; }
		public string Locale { get; set; }
		public string Seed { get; set; }
	}

	internal sealed class StageTicket
	{
		public string Id { get; set; }
		public string ConvKey { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public DateTime ExpiresAtUtc { get; set; }
	}

	internal sealed class StageDecision
	{
		public string Outcome { get; set; } // Approve|Reject|Defer|Coalesced
		public string Reason { get; set; }
		public StageTicket Ticket { get; set; }
	}

	internal sealed class StageExecutionRequest
	{
		public StageTicket Ticket { get; set; }
		public string ScenarioText { get; set; }
		public string Origin { get; set; }
		public string Locale { get; set; }
		public string Seed { get; set; }
	}

	internal sealed class ActResult
	{
		public bool Completed { get; set; }
		public string Reason { get; set; }    // Completed|Timeout|Rejected|Aborted|NoEligibleTargets|Exception
		public string FinalText { get; set; }
		public int Rounds { get; set; }
		public int LatencyMs { get; set; }
		public object Payload { get; set; }
	}

	internal sealed class RunningActInfo
	{
		public string ActName { get; set; }
		public string ConvKey { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public string TicketId { get; set; }
		public DateTime LeaseExpiresUtc { get; set; }
	}

	internal sealed class ActResourceClaim
	{
		public IReadOnlyList<string> ConvKeys { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public string MapId { get; set; }
		public bool Exclusive { get; set; } = true;
	}
}


