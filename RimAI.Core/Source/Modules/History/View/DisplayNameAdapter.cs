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
		private readonly RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService _cfg;

		public DisplayNameAdapter(IWorldDataService world)
		{
			_world = world;
			try { _cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; } catch { _cfg = null; }
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
					try
					{
						var title = _cfg?.GetPlayerTitleOrDefault();
						if (!string.IsNullOrWhiteSpace(title)) { dict[id] = title; }
						else { dict[id] = await _world.GetPlayerNameAsync(ct); }
					}
					catch { dict[id] = "Player"; }
				}
				else
				{
					// pawn:<id> 的显示名在渲染时绑定，这里回传原 id；呼叫方应根据 id 解析实际显示名
					dict[id] = id;
				}
			}
			return dict;
		}
	}
}


