using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class AiOrbitalBombardmentExecutor : IToolExecutor
	{
		public string Name => "ai_orbital_bombardment";

		public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			try
			{
				var cfg = RimAICoreMod.Container.Resolve<IConfigurationService>() as ConfigurationService;
				var world = RimAICoreMod.Container.Resolve<IWorldDataService>();
				var action = RimAICoreMod.Container.Resolve<IWorldActionService>();
				var persistence = RimAICoreMod.Container.Resolve<IPersistenceService>();
				if (world == null || action == null || persistence == null) return new { ok = false, error = "world_services_unavailable" };

				// 巡检提示模式：不执行，仅返回冷却与提示
				bool inspection = false;
				try { if (args != null && args.TryGetValue("inspection", out var ins)) bool.TryParse(ins?.ToString() ?? "false", out inspection); } catch { inspection = false; }

				int serverLevel = 1;
				try { if (args != null && args.TryGetValue("server_level", out var lv)) int.TryParse(lv?.ToString() ?? "1", NumberStyles.Integer, CultureInfo.InvariantCulture, out serverLevel); } catch { serverLevel = 1; }
				serverLevel = Math.Max(1, Math.Min(3, serverLevel));
				if (serverLevel < 2) return new { ok = false, error = "ERROR: require_server_level2" };

				// 设备：AI 终端通电
				var terminalPowered = await world.HasPoweredBuildingAsync("RimAI_AITerminalA", ct).ConfigureAwait(false);
				if (!terminalPowered) return new { ok = false, error = "ERROR: terminal_absent_or_unpowered" };

				// 防御性校验：该工具必须被任意服务器加载
				try
				{
					var loaded = await world.GetUniqueLoadedServerToolsAsync(ct).ConfigureAwait(false);
					bool found = false;
					foreach (var name in loaded)
					{
						if (string.Equals(name, Name, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
					}
					if (!found) return new { ok = false, error = "ERROR: tool_not_loaded_by_any_server" };
				}
				catch { /* best-effort; if listing fails,继续后续但通常不会发生 */ }

				// 读取冷却状态（巡检与执行均需要）
				var snap = persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
				snap.OrbitalBombardment ??= new OrbitalBombardmentState();
				long nowAbs = await world.GetNowAbsTicksAsync(ct).ConfigureAwait(false);
				// 冷却：游戏内 12 小时（半天）= 30,000 ticks
				int cooldownTicks = 30000;
				int remainingSec = 0;
				if (snap.OrbitalBombardment.NextAllowedAtAbsTicks > 0 && nowAbs < snap.OrbitalBombardment.NextAllowedAtAbsTicks)
				{
					remainingSec = (int)((snap.OrbitalBombardment.NextAllowedAtAbsTicks - nowAbs) / 60);
				}

				if (inspection)
				{
					return new { ok = true, inspection = true, cooldown_seconds = remainingSec, tip = "RimAI.Bombardment.InspectionHint".Translate().ToString() };
				}

				// 敌对存在（执行模式）
				var threat = await world.GetThreatSnapshotAsync(ct).ConfigureAwait(false);
				if (threat == null || threat.HostilePawns <= 0) return new { ok = false, error = "no_hostile_target", tip = "RimAI.Bombardment.NoHostile".Translate().ToString() };

				// 冷却 gate（3 天）
				if (remainingSec > 0)
				{
					return new { ok = false, error = "cooldown_active", seconds_left = remainingSec };
				}

				int radius = 9;
				try { if (args != null && args.TryGetValue("radius", out var r)) int.TryParse(r?.ToString() ?? "9", NumberStyles.Integer, CultureInfo.InvariantCulture, out radius); } catch { radius = 9; }
				radius = Math.Max(3, Math.Min(30, radius));

				// 炮击次数：从配置 Tooling.Bombardment 读取范围（默认 5..20），可用参数 max_strikes 作为上限
				var toolCfg = cfg?.GetToolingConfig();
				int minStrikes = Math.Max(1, toolCfg?.Bombardment?.StrikesMin ?? 5);
				int maxStrikes = Math.Max(minStrikes, toolCfg?.Bombardment?.StrikesMax ?? 20);
				try
				{
					if (args != null && args.TryGetValue("max_strikes", out var m))
					{
						if (int.TryParse(m?.ToString() ?? maxStrikes.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var upper))
						{
							maxStrikes = Math.Max(minStrikes, Math.Min(maxStrikes, upper));
						}
					}
				}
				catch { }
				var rng = new System.Random(unchecked(Environment.TickCount ^ radius ^ serverLevel));
				int strikes = rng.Next(minStrikes, maxStrikes + 1);

				// 开始提示
				try { await action.ShowTopLeftMessageAsync("RimAI.Bombardment.Start".Translate(), RimWorld.MessageTypeDefOf.ThreatBig, ct).ConfigureAwait(false); } catch { }

				var executed = await action.TryDevExplosionsNearEnemiesAsync(strikes, radius, ct).ConfigureAwait(false);

				// 结束提示
				try { await action.ShowTopLeftMessageAsync("RimAI.Bombardment.End".Translate(), RimWorld.MessageTypeDefOf.PositiveEvent, ct).ConfigureAwait(false); } catch { }

				// 冷却写入
				snap.OrbitalBombardment.LastAtAbsTicks = (int)nowAbs;
				snap.OrbitalBombardment.NextAllowedAtAbsTicks = (int)(nowAbs + cooldownTicks);
				persistence.ReplaceLastSnapshotForDebug(snap);

				return new { ok = true, strikes_executed = executed, radius, cooldown_hours = 12, cooldown_days = 0.5 };
			}
			catch (Exception ex)
			{
				return new { ok = false, error = ex.Message };
			}
		}
	}
}


