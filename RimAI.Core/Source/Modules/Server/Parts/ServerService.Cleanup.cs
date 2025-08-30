using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public async Task RemoveAsync(string entityId, bool clearInspectionHistory = true, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			// 1) 停止周期巡检任务
			if (_periodics.TryRemove(entityId, out var disp)) { try { disp.Dispose(); } catch { } }
			// 2) 构造巡检会话键
			var convKey = BuildServerInspectionConvKey(entityId);
			// 3) 清空槽位并移除记录
			if (_servers.TryRemove(entityId, out var s) && s != null)
			{
				try { s.InspectionSlots?.Clear(); } catch { }
				try { s.ServerPersonaSlots?.Clear(); } catch { }
			}
			// 4) 可选：清空巡检会话历史与对应 Recap，避免残留继续触发 UI
			if (clearInspectionHistory)
			{
				try
				{
					await _history.ClearThreadAsync(convKey, ct).ConfigureAwait(false);
					try {
						var recap = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IRecapService>();
						var got = recap?.GetRecaps(convKey);
						var items = got ?? new List<RecapItem>();
						foreach (var r in items) { try { recap.DeleteRecap(convKey, r.Id); } catch { } }
					} catch { }
				}
				catch { }
			}

			// 5) 额外清理：仅删除该服务器与玩家的严格 1v1 聊天线程（仅 server:<id>/thing:<id> 与 player:* 两个参与者）
			try
			{
				int? id = TryParseThingId(entityId);
				if (id.HasValue)
				{
					var serverPid1 = $"server:{id.Value}";
					var serverPid2 = $"thing:{id.Value}";
					var allKeys = _history.GetAllConvKeys() ?? new List<string>();
					foreach (var ck in allKeys)
					{
						if (string.IsNullOrWhiteSpace(ck)) continue;
						var parts = _history.GetParticipantsOrEmpty(ck) ?? Array.Empty<string>();
						bool isStrict1v1 = false;
						try
						{
							// 首选 participants；若缺失，则用 convKey 拆分。
							List<string> tokens = null;
							if (parts != null && parts.Count > 0) tokens = parts.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
							if (tokens == null || tokens.Count == 0) tokens = ck.Split('|').Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
							if (tokens != null && tokens.Count == 2)
							{
								bool hasServer = tokens.Any(x => string.Equals(x, serverPid1, StringComparison.Ordinal) || string.Equals(x, serverPid2, StringComparison.Ordinal));
								bool hasPlayer = tokens.Any(x => x.StartsWith("player:", StringComparison.Ordinal));
								isStrict1v1 = hasServer && hasPlayer;
							}
						}
						catch { }
						if (isStrict1v1)
						{
							try
							{
								await _history.ClearThreadAsync(ck, ct).ConfigureAwait(false);
								try {
									var recap = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IRecapService>();
									var got = recap?.GetRecaps(ck);
									var items = got ?? new List<RecapItem>();
									foreach (var r in items) { try { recap.DeleteRecap(ck, r.Id); } catch { } }
								} catch { }
							}
							catch { }
						}
					}
				}
			}
			catch { }
		}
	}
}
