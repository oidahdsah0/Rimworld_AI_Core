using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.History
{
	internal sealed class StageHistorySink
	{
		// Deprecated in V5：Stage 总线历史不再写入，保留空实现以便构造注入不破坏。
		public StageHistorySink(object _, object __) { }
		public bool TryWrite(ActResult result, string actName = null, string convKey = null) { return false; }
	}
}


