using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Persistence.Diagnostics
{
	public sealed class PersistenceStats
	{
		public string Operation { get; set; } = string.Empty; // save|load
		public int Nodes { get; set; }
		public long ElapsedMs { get; set; }
		public List<NodeStat> Details { get; set; } = new List<NodeStat>();
	}

	public sealed class NodeStat
	{
		public string Node { get; set; } = string.Empty;
		public bool Ok { get; set; }
		public int Entries { get; set; }
		public long BytesApprox { get; set; }
		public long ElapsedMs { get; set; }
		public string Error { get; set; }
	}
}


