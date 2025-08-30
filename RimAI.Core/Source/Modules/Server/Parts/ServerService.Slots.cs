using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Server
{
	internal sealed partial class ServerService
	{
		public void SetInspectionIntervalHours(string entityId, int hours)
		{
			var s = GetOrThrow(entityId);
			s.InspectionIntervalHours = Math.Max(6, hours);
			// 重启调度：首轮先经历完整冷却
			RestartScheduler(entityId, initialDelayTicks: s.InspectionIntervalHours * 2500);
		}

		public void SetInspectionEnabled(string entityId, bool enabled)
		{
			var s = GetOrThrow(entityId);
			s.InspectionEnabled = enabled;
			RestartScheduler(entityId);
		}

		public void AssignSlot(string entityId, int slotIndex, string toolName)
		{
			var s = GetOrThrow(entityId);
			var cap = GetInspectionCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureInspectionSlots(s, cap);
			// 取消全局唯一限制：允许不同服务器加载相同工具；权限校验仍在列表阶段
			s.InspectionSlots[slotIndex] = new InspectionSlot { Index = slotIndex, ToolName = toolName, Enabled = true };
			// 若之前未注册周期任务且巡检启用，则启动定时器
			try { if (s.InspectionEnabled && !_periodics.ContainsKey(entityId)) StartOneScheduler(entityId, s.InspectionIntervalHours, CancellationToken.None, initialDelayTicks: 0); } catch { }
		}

		public void RemoveSlot(string entityId, int slotIndex)
		{
			var s = GetOrThrow(entityId);
			var cap = GetInspectionCapacity(s.Level);
			if (slotIndex < 0 || slotIndex >= cap) throw new ArgumentOutOfRangeException(nameof(slotIndex));
			EnsureInspectionSlots(s, cap);
			s.InspectionSlots[slotIndex] = new InspectionSlot { Index = slotIndex, ToolName = null, Enabled = false };
		}

		public IReadOnlyList<InspectionSlot> GetSlots(string entityId)
		{
			var s = GetOrThrow(entityId);
			EnsureInspectionSlots(s, GetInspectionCapacity(s.Level));
			return s.InspectionSlots.OrderBy(x => x.Index).ToList();
		}
	}
}
