using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Modules.Stage.Diagnostics;
using RimAI.Core.Source.Modules.Stage.Acts;
using RimAI.Core.Source.Modules.Stage.History;
using RimAI.Core.Source.Modules.Stage.Kernel;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage
{
	internal sealed class StageService : IStageService
	{
		private readonly IStageKernel _kernel;
		private readonly StageHistorySink _history;
		private readonly StageLogging _log;
		private readonly ConfigurationService _cfg;

		private readonly ConcurrentDictionary<string, IStageAct> _acts = new();
		private readonly ConcurrentDictionary<string, (IStageTrigger trigger, bool enabled)> _triggers = new();
		private readonly ConcurrentDictionary<string, string> _ticketToActName = new();

		public StageService(IStageKernel kernel, StageHistorySink history, StageLogging log, IConfigurationService cfg)
		{
			_kernel = kernel;
			_history = history;
			_log = log;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("StageService requires ConfigurationService");
		}

		public void RegisterAct(IStageAct act)
		{
			if (act == null || string.IsNullOrEmpty(act.Name)) throw new ArgumentException("invalid act");
			_acts[act.Name] = act;
			_ = act.OnEnableAsync(CancellationToken.None);
		}

		public void UnregisterAct(string name) { if (!string.IsNullOrEmpty(name)) _acts.TryRemove(name, out _); }
		public void EnableAct(string name) { /* minimal: acts are stateless for now; OnEnable via registration flow */ }
		public void DisableAct(string name) { /* minimal toggle not implemented; can be enforced via config DisabledActs */ }
		public IReadOnlyList<string> ListActs() => _acts.Keys.OrderBy(x => x).ToList();

		public void RegisterTrigger(IStageTrigger trigger)
		{
			if (trigger == null || string.IsNullOrEmpty(trigger.Name)) throw new ArgumentException("invalid trigger");
			var disabled = _cfg.GetInternal().Stage?.DisabledTriggers ?? Array.Empty<string>();
			bool enabled = !disabled.Contains(trigger.Name, StringComparer.OrdinalIgnoreCase);
			_triggers[trigger.Name] = (trigger, enabled);
			_ = (enabled ? trigger.OnEnableAsync(CancellationToken.None) : trigger.OnDisableAsync(CancellationToken.None));
		}

		public void UnregisterTrigger(string name) { if (!string.IsNullOrEmpty(name)) _triggers.TryRemove(name, out _); }
		public void EnableTrigger(string name) { if (_triggers.TryGetValue(name, out var t)) { _triggers[name] = (t.trigger, true); _ = t.trigger.OnEnableAsync(CancellationToken.None); } }
		public void DisableTrigger(string name) { if (_triggers.TryGetValue(name, out var t)) { _triggers[name] = (t.trigger, false); _ = t.trigger.OnDisableAsync(CancellationToken.None); } }
		public IReadOnlyList<string> ListTriggers() => _triggers.Keys.OrderBy(x => x).ToList();

		public bool ArmTrigger(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return false;
			if (_triggers.TryGetValue(name, out var t))
			{
				try
				{
					if (t.trigger is IManualStageTrigger m) { m.ArmOnce(); return true; }
				}
				catch { }
			}
			return false;
		}

		public async Task<StageDecision> SubmitIntentAsync(StageIntent intent, CancellationToken ct)
		{
			if (intent == null) throw new ArgumentNullException(nameof(intent));
			var participants = intent.ParticipantIds?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList() ?? new List<string>();
			var convKey = string.Join("|", participants);
			bool isManual = string.Equals(intent.Origin, "Manual", StringComparison.OrdinalIgnoreCase);
			_log.Info($"SubmitIntent act={intent.ActName} convKey={convKey} origin={intent.Origin} participants={participants.Count}");
			if (participants.Count < 2)
			{
				_log.Warn($"Reject intent (TooFewParticipants) act={intent.ActName} convKey={convKey}");
				return new StageDecision { Outcome = "Reject", Reason = "TooFewParticipants" };
			}

			var stageCfg = _cfg.GetInternal().Stage;
			var coalesceMs = stageCfg?.CoalesceWindowMs ?? 300;
			var cooldownSec = stageCfg?.CooldownSeconds ?? 30;

			// 合流窗口：手动触发不参与合流
			if (!isManual)
			{
				_log.Info($"CoalesceCheck convKey={convKey} windowMs={coalesceMs}");
				var coalesced = await _kernel.CoalesceWithinAsync(convKey, coalesceMs, () => Task.FromResult(false));
				if (coalesced)
				{
					_log.Info($"Coalesced intent act={intent.ActName} convKey={convKey} windowMs={coalesceMs}");
					return new StageDecision { Outcome = "Coalesced", Reason = "Window" };
				}
			}

			// 冷却：手动触发跳过冷却检查
			var cdKey = (intent.ActName ?? string.Empty) + ":" + convKey;
			if (!isManual && _kernel.IsInCooldown(cdKey)) { _log.Info($"Reject intent (Cooling) act={intent.ActName} convKey={convKey}"); return new StageDecision { Outcome = "Reject", Reason = "Cooling" }; }
			_log.Info($"Reserve attempt act={intent.ActName} convKey={convKey}");

			// 幂等
			var idemKey = Kernel.StageKernel.ComputeIdempotencyKey(intent.ActName ?? string.Empty, convKey, intent.ScenarioText ?? string.Empty, intent.Seed ?? string.Empty);
			if (_kernel.IdempotencyTryGet(idemKey, out var cached))
			{
				_log.Info($"Idempotency hit act={intent.ActName} convKey={convKey}");
				return new StageDecision { Outcome = "Approve", Reason = "IdempotencyHit", Ticket = new StageTicket { Id = "cached:" + idemKey, ConvKey = convKey, ParticipantIds = participants, ExpiresAtUtc = DateTime.UtcNow.AddSeconds(1) } };
			}

			// 互斥 + 并发上限
			var claim = new ActResourceClaim { ConvKeys = new[] { convKey }, ParticipantIds = participants, Exclusive = true };
			if (!_kernel.TryReserve(claim, out var ticket))
			{
				_log.Info($"Reject intent (ConflictOrBusy) act={intent.ActName} convKey={convKey}");
				return new StageDecision { Outcome = "Reject", Reason = "ConflictOrBusy" };
			}
			_log.Info($"Reserve success ticket={ticket.Id} convKey={convKey}");

			// 路由执行（后台）
			_ = Task.Run(async () =>
			{
				try
				{
					await RouteAndExecuteAsync(intent, ticket, convKey, cooldownSec, ct);
				}
				catch { }
			});

			_log.Info($"Accepted intent act={intent.ActName} convKey={convKey} ticket={ticket.Id}");
			return new StageDecision { Outcome = "Approve", Ticket = ticket };
		}

		public async Task<ActResult> StartAsync(string actName, StageExecutionRequest req, CancellationToken ct)
		{
			if (!_acts.TryGetValue(actName, out var act)) return new ActResult { Completed = false, Reason = "ActNotFound", FinalText = "（未找到指定 Act）" };
			if (!act.IsEligible(req)) return new ActResult { Completed = false, Reason = "Rejected", FinalText = "（条件不满足，已跳过）" };
			var start = DateTime.UtcNow;
			try
			{
				var leaseTtlMs = _cfg.GetInternal().Stage?.LeaseTtlMs ?? 10000;
				var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
				var maxMs = _cfg.GetInternal().Stage?.Acts?.GroupChat?.MaxLatencyMsPerTurn ?? 8000;
				var leaseTtl = TimeSpan.FromMilliseconds(leaseTtlMs);
				var renewTask = Task.Run(async () =>
				{
					while (!cts.IsCancellationRequested)
					{
						try { await Task.Delay(Math.Max(100, leaseTtlMs / 2), cts.Token); } catch { break; }
						try { _kernel.ExtendLease(req.Ticket, leaseTtl); } catch { }
					}
				});

				ActResult result = null;
				Exception execError = null;
				try
				{
					var execTask = act.ExecuteAsync(req, cts.Token);
					var timeoutTask = Task.Delay(maxMs, CancellationToken.None);
					var finished = await Task.WhenAny(execTask, timeoutTask);
					if (finished == execTask)
					{
						result = await execTask; // observe result/exception
					}
					else
					{
						// Hard timeout: signal cancel and return a timeout result without awaiting the hanging task
						try { cts.Cancel(); } catch { }
						_log.Warn($"ActTimeout act={actName} convKey={req?.Ticket?.ConvKey} maxMs={maxMs}");
						result = new ActResult { Completed = false, Reason = "Timeout", FinalText = "（本轮对话失败或超时，已跳过）" };
					}
				}
				catch (OperationCanceledException)
				{
					result = new ActResult { Completed = false, Reason = "Timeout", FinalText = "（本轮对话失败或超时，已跳过）" };
				}
				catch (Exception ex)
				{
					execError = ex;
					result = new ActResult { Completed = false, Reason = "Exception", FinalText = "（本轮对话失败或超时，已跳过）" };
				}
				finally
				{
					try { cts.Cancel(); } catch { }
					try { await renewTask; } catch { }
				}

				result = result ?? new ActResult { Completed = false, Reason = "Exception", FinalText = "（本轮对话失败或超时，已跳过）" };
				result.LatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
				if (string.IsNullOrEmpty(result.FinalText)) result.FinalText = "（本轮对话失败或超时，已跳过）";
				if (!(result.Completed))
				{
					var reason = string.IsNullOrWhiteSpace(result.Reason) ? "Unknown" : result.Reason;
					_log.Warn($"ActFailed act={actName} convKey={req?.Ticket?.ConvKey} reason={reason} latencyMs={result.LatencyMs}");
					if (execError != null) { _log.Error($"ActException act={actName} convKey={req?.Ticket?.ConvKey} error={execError.GetType().Name}:{execError.Message}"); }
				}
				return result;
			}
			catch (Exception ex)
			{
				_log.Error($"StartAsyncError act={actName} convKey={req?.Ticket?.ConvKey} error={ex.GetType().Name}:{ex.Message}");
				return new ActResult { Completed = false, Reason = "Exception", FinalText = "（本轮对话失败或超时，已跳过）", LatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds };
			}
		}

		public IReadOnlyList<RunningActInfo> QueryRunning()
		{
			var tickets = _kernel.GetRunningTickets();
			return tickets.Select(t => new RunningActInfo
			{
				ActName = (_ticketToActName.TryGetValue(t.Id, out var nm) ? nm : "?"),
				ConvKey = t.ConvKey,
				ParticipantIds = t.ParticipantIds,
				TicketId = t.Id,
				LeaseExpiresUtc = t.ExpiresAtUtc
			}).ToList();
		}

		public async Task RunActiveTriggersOnceAsync(CancellationToken ct)
		{
			foreach (var kv in _triggers.ToArray())
			{
				if (ct.IsCancellationRequested) break;
				var (trigger, enabled) = kv.Value;
				if (trigger == null || !enabled) continue;
				try
				{
					await trigger.RunOnceAsync(intent => SubmitIntentAsync(intent, ct), ct);
				}
				catch { }
			}
		}

		public IAutoStageIntentProvider TryGetAutoProvider(string actName)
		{
			if (string.IsNullOrWhiteSpace(actName)) return null;
			if (_acts.TryGetValue(actName, out var act))
			{
				return act as IAutoStageIntentProvider;
			}
			return null;
		}

		public void ForceRelease(string ticketId) => _kernel.ForceRelease(ticketId);
		public void ClearIdempotencyCache() => _kernel.ClearIdempotencyCache();

		private async Task RouteAndExecuteAsync(StageIntent intent, StageTicket ticket, string convKey, int cooldownSec, CancellationToken outerCt)
		{
			try
			{
				if (!_acts.TryGetValue(intent.ActName ?? string.Empty, out var act))
				{
					_history.TryWrite(new ActResult { Completed = false, Reason = "ActNotFound", FinalText = "（未找到指定 Act）" }, intent.ActName, convKey);
					_kernel.Release(ticket);
					return;
				}
				_log.Info($"ActStarted act={intent.ActName} convKey={convKey} ticket={ticket.Id}");
				_ticketToActName[ticket.Id] = intent.ActName ?? string.Empty;
				var req = new StageExecutionRequest { Ticket = ticket, ScenarioText = intent.ScenarioText, Origin = intent.Origin, Locale = intent.Locale, Seed = intent.Seed };
				_log.Info($"StartAsync begin act={intent.ActName} ticket={ticket.Id}");
				var result = await StartAsync(intent.ActName, req, outerCt);
				_log.Info($"StartAsync end act={intent.ActName} ticket={ticket.Id} completed={result?.Completed} reason={result?.Reason}");
				if (!(result?.Completed ?? false))
				{
					var reason = result?.Reason ?? "Unknown";
					_log.Warn($"ActResultNotCompleted act={intent.ActName} convKey={convKey} reason={reason} latencyMs={result?.LatencyMs}");
				}
				// 仅当 GroupChat 成功完成时写入；其他 Act 按原逻辑全部写入
				bool isGroupChat = string.Equals(intent.ActName, "GroupChat", StringComparison.OrdinalIgnoreCase);
				if (!isGroupChat || (result?.Completed ?? false))
				{
					_history.TryWrite(result, intent.ActName, convKey);
				}
				// 幂等缓存
				var idemKey = Kernel.StageKernel.ComputeIdempotencyKey(intent.ActName ?? string.Empty, convKey, intent.ScenarioText ?? string.Empty, intent.Seed ?? string.Empty);
				var idemTtl = TimeSpan.FromMilliseconds(_cfg.GetInternal().Stage?.IdempotencyTtlMs ?? 60000);
				_kernel.IdempotencySet(idemKey, result, idemTtl);
				var finReason = result?.Reason ?? "";
				_log.Info($"ActFinished act={intent.ActName} convKey={convKey} ticket={ticket.Id} latency={result?.LatencyMs}ms completed={result?.Completed} reason={finReason}");
			}
			finally
			{
				_log.Info($"ActCleanup begin ticket={ticket.Id} convKey={convKey}");
				_ticketToActName.TryRemove(ticket.Id, out _);
				_kernel.Release(ticket);
				_kernel.SetCooldown((intent.ActName ?? string.Empty) + ":" + convKey, TimeSpan.FromSeconds(Math.Max(1, cooldownSec)));
				_log.Info($"ActCleanup end ticket={ticket.Id} convKey={convKey}");
			}
		}
	}
}


