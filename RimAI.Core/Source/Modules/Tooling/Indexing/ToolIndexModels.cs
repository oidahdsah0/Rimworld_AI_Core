using System;
using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Tooling.Indexing
{
	internal sealed class ToolEmbeddingRecord
	{
		public string Id { get; set; }
		public string ToolName { get; set; }
		public string Variant { get; set; } // name | description | parameters
		public string Text { get; set; }
		public float[] Vector { get; set; }
		public string Provider { get; set; }
		public string Model { get; set; }
		public int Dimension { get; set; }
		public string Instruction { get; set; }
		public DateTime BuiltAtUtc { get; set; }
	}

	internal sealed class ToolIndexFingerprint
	{
		public string Provider { get; set; }
		public string Model { get; set; }
		public int Dimension { get; set; }
		public string Instruction { get; set; }
		public string Hash { get; set; }
	}

	internal sealed class ToolIndexSnapshot
	{
		public ToolIndexFingerprint Fingerprint { get; set; }
		public IReadOnlyList<ToolEmbeddingRecord> Records { get; set; }
		public (double Name, double Desc, double Params) Weights { get; set; }
		public DateTime BuiltAtUtc { get; set; }
	}

	internal sealed class ToolScore
	{
		public string ToolName { get; set; }
		public double Score { get; set; }
	}
}


