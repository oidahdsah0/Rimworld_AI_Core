using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Persona.Biography;
using RimAI.Core.Source.Modules.Persona.Ideology;

namespace RimAI.Core.Source.Modules.Persona
{
	/// <summary>
	/// 后台自动生成器：每 15 天尝试为所有殖民者生成传记与世界观（各最多重试 3 次）。
	/// 遵循：
	/// - P3：使用 Scheduler 定期触发，避免阻塞主线程；Verse 访问通过 WorldDataService 的异步 API。
	/// - P7：调用 Persona 子服务进行非流式生成；失败不覆盖现有内容。
	/// </summary>
	internal sealed class PersonaAutoGenerator : IDisposable
	{
		private readonly ISchedulerService _scheduler;
		private readonly IWorldDataService _world;
		private readonly IBiographyService _bio;
		private readonly IIdeologyService _ideo;
		private readonly IPersonaAutoSettingsService _settings;
		private readonly IDisposable _periodic;
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();

		// RimWorld: 60k ticks/day；15 天=900k ticks
		private const int TicksPerDay = 60000;
		private const int PeriodTicks = TicksPerDay * 3; // 三天检查一次，降低开销；内部判断配置的跨度
		// 旧版“分别独立”的按服务节流已移除：统一在一次全局周期中执行两项更新

		public PersonaAutoGenerator(ISchedulerService scheduler, IWorldDataService world, IBiographyService bio, IIdeologyService ideo, IPersonaAutoSettingsService settings = null)
		{
			_scheduler = scheduler;
			_world = world;
			_bio = bio;
			_ideo = ideo;
			_settings = settings;
			_periodic = _scheduler.SchedulePeriodic("PersonaAutoGenerator", PeriodTicks, OnTickAsync, _cts.Token);
		}

		private async Task OnTickAsync(CancellationToken ct)
		{
			try
			{
				var day = await _world.GetCurrentDayNumberAsync(ct).ConfigureAwait(false);
				// 仅每满 15 天触发一次
				if (_settings != null)
				{
					var last = _settings.GetLastRunDay();
					var interval = Math.Max(1, _settings.GetIntervalDays());
					if (last >= 0 && (day - last) < interval) return;
				}
				var ids = await _world.GetAllColonistLoadIdsAsync(ct).ConfigureAwait(false);
				foreach (var loadId in ids)
				{
					var entityId = $"pawn:{loadId}";
					// 勾选开关：两者均未勾选则跳过
					if (_settings != null)
					{
						bool allowBio = _settings.GetAutoBio(entityId);
						bool allowIdeo = _settings.GetAutoIdeo(entityId);
						if (!allowBio && !allowIdeo) { continue; }
					}
					// 统一在本轮中依次执行两项（失败互不影响）
					if (!ct.IsCancellationRequested) { try { await TryGenerateBiographyAsync(entityId, ct).ConfigureAwait(false); } catch { } }
					if (!ct.IsCancellationRequested) { try { await TryGenerateIdeologyAsync(entityId, ct).ConfigureAwait(false); } catch { } }
					// 间隔配置的毫秒数再处理下一个（真实时间）
					var delayMs = Math.Max(0, _settings?.GetPerPawnDelayMs() ?? 60000);
					try { await Task.Delay(delayMs, ct).ConfigureAwait(false); } catch { }
				}
				if (_settings != null) _settings.SetLastRunDay(day);
			}
			catch { }
		}



		private async Task<bool> TryGenerateBiographyAsync(string entityId, CancellationToken ct)
		{
			int maxRetries = Math.Max(1, _settings?.GetMaxRetries() ?? 3);
			for (int attempt = 1; attempt <= maxRetries; attempt++)
			{
				try
				{
					var drafts = await _bio.GenerateDraftAsync(entityId, ct).ConfigureAwait(false);
					if (drafts != null && drafts.Count > 0)
					{
						foreach (var d in drafts) { try { _bio.Upsert(entityId, d); } catch { } }
						return true;
					}
				}
				catch (OperationCanceledException) { return false; }
				catch { }
			}
			return false;
		}

		private async Task<bool> TryGenerateIdeologyAsync(string entityId, CancellationToken ct)
		{
			int maxRetries = Math.Max(1, _settings?.GetMaxRetries() ?? 3);
			for (int attempt = 1; attempt <= maxRetries; attempt++)
			{
				try
				{
					var s = await _ideo.GenerateAsync(entityId, ct).ConfigureAwait(false);
					if (s != null && (!string.IsNullOrWhiteSpace(s.Worldview) || !string.IsNullOrWhiteSpace(s.Values) || !string.IsNullOrWhiteSpace(s.CodeOfConduct) || !string.IsNullOrWhiteSpace(s.TraitsText)))
					{
						_ideo.Set(entityId, s);
						return true;
					}
				}
				catch (OperationCanceledException) { return false; }
				catch { }
			}
			return false;
		}

		public void Dispose()
		{
			try { _cts.Cancel(); } catch { }
			try { _periodic?.Dispose(); } catch { }
			try { _cts.Dispose(); } catch { }
		}
	}
}


