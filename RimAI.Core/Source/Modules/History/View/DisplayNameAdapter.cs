using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.History.View
{
	internal interface IDisplayNameService
	{
		Task<Dictionary<string, string>> ResolveAsync(IReadOnlyList<string> participantIds, CancellationToken ct = default);
	}

	internal sealed class DisplayNameAdapter : IDisplayNameService
	{
		private readonly IWorldDataService _world;

		public DisplayNameAdapter(IWorldDataService world)
		{
			_world = world;
		}

		public async Task<Dictionary<string, string>> ResolveAsync(IReadOnlyList<string> participantIds, CancellationToken ct = default)
		{
			var dict = new Dictionary<string, string>();
			if (participantIds == null) return dict;
			// 最小占位实现：仅对 player 显示玩家名，其它原样返回
			foreach (var id in participantIds)
			{
				if (string.IsNullOrWhiteSpace(id)) continue;
				if (id.StartsWith("player:"))
				{
					try { dict[id] = await _world.GetPlayerNameAsync(ct); }
					catch { dict[id] = "Player"; }
				}
				else
				{
					dict[id] = id;
				}
			}
			return dict;
		}
	}
}


