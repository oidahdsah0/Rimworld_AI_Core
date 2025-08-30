using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public void StartAllSchedulers(CancellationToken appRootCt)
		{
			foreach (var s in _servers.Values.ToList())
			{
				if (s?.InspectionEnabled == true)
				{
					// 启动时不强制初始冷却，保持原有体验
					StartOneScheduler(s.EntityId, s.InspectionIntervalHours, appRootCt, initialDelayTicks: 0);
				}
			}
		}

		public void RestartScheduler(string entityId, int initialDelayTicks = 0)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			if (_periodics.TryRemove(entityId, out var d)) { try { d.Dispose(); } catch { } }
			var rec = GetOrThrow(entityId);
			if (rec.InspectionEnabled)
			{
				StartOneScheduler(entityId, rec.InspectionIntervalHours, CancellationToken.None, initialDelayTicks);
			}
		}

		private void StartOneScheduler(string entityId, int hours, CancellationToken appRootCt, int initialDelayTicks = 0)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			if (_periodics.ContainsKey(entityId)) return; // 幂等保护：避免重复注册
			// 全局关闭则不注册周期任务
			try { var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService>(); if (cfg != null && !cfg.GetServerConfig().GlobalInspectionEnabled) return; } catch { }
			// 若当前服务器没有任何工具已配置，跳过注册，等首次分配工具时再启动
			try
			{
				var s = Get(entityId);
				if (s != null)
				{
					var anyTool = (s.InspectionSlots ?? new List<InspectionSlot>()).Any(x => x != null && x.Enabled && !string.IsNullOrWhiteSpace(x.ToolName));
					if (!anyTool) return;
				}
			}
			catch { }
			int everyTicks = Math.Max(6, hours) * 2500;
			var name = $"server:{entityId}:inspection";
			try
			{
				var disp = _scheduler.SchedulePeriodic(name, everyTicks, async ct =>
				{
					try { await RunInspectionOnceAsync(entityId, ct).ConfigureAwait(false); }
					catch (OperationCanceledException) { }
					catch (Exception ex) { Verse.Log.Error($"[RimAI.Core][P13.Server] periodic failed: {ex.Message}"); }
				}, appRootCt, initialDelayTicks);
				_periodics[entityId] = disp;
			}
			catch { }
		}
	}
}
