using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Eventing;
using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Modules.World;
using RimAI.Core.Settings;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Modules.Stage.Acts;
using RimAI.Core.Modules.Stage.Kernel;

namespace RimAI.Core.Modules.Stage
{
    /// <summary>
    /// P11-M1 舞台服务最小实现：
    /// - convKey 归一化
    /// - Eligibility（MinParticipants/PermittedOrigins/MaxParticipants 裁剪）
    /// - 会话锁（按 convKey）
    /// - 合流窗口（CoalesceWindowMs）
    /// - 冷却（CooldownSeconds）
    /// - 幂等键短期复用
    /// 返回占位结果，M2 接入 Persona 与历史。
    /// </summary>
internal sealed partial class StageService : IStageService
    {
        private readonly IConfigurationService _config;
        private readonly IEventBus _events;
        private readonly IParticipantIdService _pid;
        private readonly IPersonaConversationService _persona;
        private readonly RimAI.Core.Services.IHistoryWriteService _history;
        private readonly IStageKernel _kernel;

        private sealed class CoalesceBucket
        {
            public string ConvKey;
            public DateTime FirstSeenUtc;
            public List<StageExecutionRequest> Requests = new();
            public TaskCompletionSource<bool> Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private sealed class RecentResult
        {
            public string ConvKey;
            public DateTime CompletedUtc;
            public string Text;
        }

        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(); // convKey -> lock
        private readonly ConcurrentDictionary<string, CoalesceBucket> _coalesce = new(); // convKey -> bucket
        private readonly ConcurrentDictionary<string, DateTime> _cooldown = new(); // convKey -> last finished time
        private readonly ConcurrentDictionary<string, RecentResult> _recentByConvKey = new(); // convKey -> last result

        public StageService(IConfigurationService config, IEventBus events, IParticipantIdService pid, IPersonaConversationService persona, RimAI.Core.Services.IHistoryWriteService history, IStageKernel kernel)
        {
            _config = config;
            _events = events;
            _pid = pid;
            _persona = persona;
            _history = history;
            _kernel = kernel;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StartAsync(StageExecutionRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            request = request ?? new StageExecutionRequest { Participants = Array.Empty<string>() };
            var cfg = _config.Current?.Stage ?? new StageConfig();

            // 1) Eligibility
            var normalized = NormalizeParticipants(request.Participants, cfg);
            if (normalized.Count < Math.Max(2, cfg.MinParticipants))
            {
                yield return Result<UnifiedChatChunk>.Failure("TooFewParticipants");
                yield break;
            }
            // 薄层 Debug 路由不校验 Origin

            // 2) convKey & seed
            var convKey = string.Join("|", normalized.OrderBy(x => x, StringComparer.Ordinal));
            var seed = request.Seed ?? ComputeSeed(convKey);

            // 3) 冷却
            if (IsInCooldown(convKey, cfg))
            {
                yield return Result<UnifiedChatChunk>.Failure("InCooldown");
                yield break;
            }

            // 4) 幂等（P11.5 内核承担，薄层暂略）

            // 5) 合流：进入 bucket
            var bucket = _coalesce.GetOrAdd(convKey, key => new CoalesceBucket { ConvKey = key, FirstSeenUtc = DateTime.UtcNow });
            lock (bucket)
            {
                bucket.Requests.Add(request);
            }
            var delayMs = Math.Max(0, cfg.CoalesceWindowMs);
            var now = DateTime.UtcNow;
            if ((now - bucket.FirstSeenUtc).TotalMilliseconds < delayMs)
            {
                try { await Task.Delay(delayMs, ct); } catch { /* ignore */ }
            }

            // 6) 会话锁
            var gate = _locks.GetOrAdd(convKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                // 再次确认：如果有其他请求已经处理并释放，返回合流结果
                if ((DateTime.UtcNow - bucket.FirstSeenUtc).TotalMilliseconds > delayMs + 50)
                {
                    if (_recentByConvKey.TryGetValue(convKey, out var rr0))
                        yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = rr0.Text ?? string.Empty });
                    else
                        yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = string.Empty });
                    yield break;
                }

                // 选主触发（精简：取第一个）
                StageExecutionRequest leader = null;
                lock (bucket)
                {
                    leader = bucket.Requests.FirstOrDefault() ?? request;
                }

            PublishStageEvent("StageStarted", convKey, new { participants = normalized, seed, requestCount = bucket.Requests.Count });

                // 仲裁内核：资源预定（convKey/participants 互斥）。失败则拒绝
                Kernel.StageTicket ticket = null;
                var claim = new Kernel.ActResourceClaim { ConvKeys = new[] { convKey }, ParticipantIds = normalized };
                if (!_kernel.TryReserve(claim, out ticket, TimeSpan.FromSeconds(Math.Max(5, cfg.CooldownSeconds))))
                {
                    yield return Result<UnifiedChatChunk>.Failure("RejectedByArbitration");
                    yield break;
                }
                _tickets[convKey] = ticket;
                _running[ticket.Id] = new RunningActInfo { ActName = request.ActName ?? "GroupChat", ConvKey = convKey, Participants = normalized, SinceUtc = DateTime.UtcNow, LeaseExpiresUtc = ticket.ExpiresAtUtc };

                // 选题与会话级场景提示：P11.5 下沉至具体 Act 内部
                var locale = request.Locale ?? _config.Current?.Stage?.LocaleOverride;

                // M2/M3：调用 Persona 或执行群聊 Act（非流式）
                string final = string.Empty;
                string error = null;
                var maxLatency = Math.Max(1000, _config.Current?.Stage?.MaxLatencyMsPerTurn ?? 5000);
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(maxLatency);
                    try
                    {
                        var acts = CoreServices.Locator.Get<IEnumerable<IStageAct>>();
                        var act = acts?.FirstOrDefault(a => string.Equals(a.Name, request.ActName, StringComparison.OrdinalIgnoreCase));
                        if (act == null)
                        {
                            error = "ActNotFound";
                        }
                        else
                        {
                            var actCtx = new Acts.ActContext
                            {
                                ConvKey = convKey,
                                Participants = normalized,
                                Seed = seed,
                                Locale = locale,
                                Options = _config.Current?.Stage
                            };
                            if (!act.IsEligible(actCtx))
                            {
                                error = "ActNotEligible";
                            }
                            else
                            {
                                var res = await act.RunAsync(actCtx, cts.Token);
                                final = res?.FinalText ?? string.Empty;
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (string.IsNullOrEmpty(final)) error = "Timeout";
                    }
                    catch (Exception ex)
                    {
                        error = ex.Message;
                    }
                }

                if (!string.IsNullOrWhiteSpace(error) && string.IsNullOrWhiteSpace(final))
                {
                    PublishStageEvent("Finished", convKey, new { ok = false, error });
                    yield return Result<UnifiedChatChunk>.Failure(error);
                    yield break;
                }

                // 最终输出写入
                var text = final ?? string.Empty;
                try
                {
                    var idsByKey = await _history.FindByConvKeyAsync(convKey);
                    var convId = idsByKey?.LastOrDefault();
                    if (string.IsNullOrWhiteSpace(convId))
                    {
                        convId = _history.CreateConversation(normalized);
                    }
                    var speakerId = !string.IsNullOrWhiteSpace(request.TargetParticipantId) ? request.TargetParticipantId : (normalized.Count > 0 ? normalized[0] : _pid.GetPlayerId());
                    var entry = new ConversationEntry(speakerId, text, DateTime.UtcNow);
                    await _history.AppendEntryAsync(convId, entry);
                }
                catch { }

                // 设置冷却
                _cooldown[convKey] = DateTime.UtcNow;
                _recentByConvKey[convKey] = new RecentResult { ConvKey = convKey, CompletedUtc = DateTime.UtcNow, Text = text };

                PublishStageEvent("Coalesced", convKey, new { coalesced = true, count = bucket.Requests.Count });
                PublishStageEvent("Finished", convKey, new { ok = true, textLen = text.Length });

                // 清理合流桶
                _coalesce.TryRemove(convKey, out _);
                // 场景提示覆盖清理在具体 Act 内完成

                // 释放仲裁票据
                try
                {
                    if (ticket != null) _kernel.Release(ticket);
                    _tickets.Remove(convKey);
                    if (ticket != null) _running.Remove(ticket.Id);
                }
                catch { }

                yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = text });
            }
            finally
            {
                try { gate.Release(); } catch { }
            }
        }

        

        private static int ComputeSeed(string convKey)
        {
            unchecked
            {
                int hash = 17;
                foreach (var ch in convKey ?? string.Empty) hash = hash * 31 + ch;
                return hash;
            }
        }

        private static IReadOnlyList<string> NormalizeParticipants(IReadOnlyList<string> ids, StageConfig cfg)
        {
            var list = (ids ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Take(Math.Max(2, Math.Min(cfg.MaxParticipants, 10)))
                .ToList();
            return list;
        }

        private static bool IsOriginPermitted(string origin, StageConfig cfg)
        {
            origin = (origin ?? string.Empty).Trim();
            return cfg.PermittedOrigins?.Contains(origin) ?? true;
        }

        private bool IsInCooldown(string convKey, StageConfig cfg)
        {
            if (_cooldown.TryGetValue(convKey, out var last))
            {
                return (DateTime.UtcNow - last).TotalSeconds < Math.Max(0, cfg.CooldownSeconds);
            }
            return false;
        }

        private void PublishStageEvent(string stage, string convKey, object payload)
        {
            try
            {
                var json = payload == null ? string.Empty : JsonConvert.SerializeObject(payload);
                _events?.Publish(new OrchestrationProgressEvent
                {
                    Source = nameof(StageService),
                    Stage = stage,
                    Message = convKey,
                    PayloadJson = json
                });
            }
            catch { /* ignore */ }
        }

        // ---- P11.5: Act 注册/启停/仲裁/查询 ----
        private readonly Dictionary<string, Acts.IStageAct> _acts = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _disabledActs = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Triggers.IStageTrigger> _triggers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _disabledTriggers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RunningActInfo> _running = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Kernel.StageTicket> _tickets = new(StringComparer.Ordinal);

        public void RegisterAct(Acts.IStageAct act)
        {
            if (act == null) return;
            _acts[act.Name] = act;
        }

        public void UnregisterAct(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _acts.Remove(name);
            _disabledActs.Remove(name);
        }

        public void EnableAct(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _disabledActs.Remove(name);
        }

        public void DisableAct(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _disabledActs.Add(name);
        }

        public IReadOnlyList<string> ListActs() => _acts.Keys.ToList();

        public IReadOnlyList<RunningActInfo> QueryRunning() => _running.Values.ToList();

        public StageDecision SubmitIntent(StageIntent intent)
        {
            if (intent == null) return new StageDecision { Outcome = "Reject", Reason = "InvalidIntent" };
            if (string.IsNullOrWhiteSpace(intent.ActName) || !_acts.ContainsKey(intent.ActName))
                return new StageDecision { Outcome = "Reject", Reason = "ActNotFound" };
            if (_disabledActs.Contains(intent.ActName))
                return new StageDecision { Outcome = "Reject", Reason = "ActDisabled" };

            var cfg = _config.Current?.Stage ?? new StageConfig();
            var participants = NormalizeParticipants(intent.Participants, cfg);
            if (participants.Count < Math.Max(2, cfg.MinParticipants))
                return new StageDecision { Outcome = "Reject", Reason = "TooFewParticipants" };
            var convKey = intent.ConvKey ?? string.Join("|", participants.OrderBy(x => x, StringComparer.Ordinal));
            if (IsInCooldown(convKey, cfg))
                return new StageDecision { Outcome = "Defer", Reason = "InCooldown" };

            // MVP：直接批准（后续接入 Kernel.TryReserve 并填充 ticket）
            return new StageDecision { Outcome = "Approve" };
        }

        // 触发器注册/启停/枚举
        public void RegisterTrigger(Triggers.IStageTrigger trigger)
        {
            if (trigger == null) return;
            _triggers[trigger.Name] = trigger;
        }

        public void UnregisterTrigger(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _triggers.Remove(name);
            _disabledTriggers.Remove(name);
        }

        public void EnableTrigger(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _disabledTriggers.Remove(name);
        }

        public void DisableTrigger(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            _disabledTriggers.Add(name);
        }

        public IReadOnlyList<string> ListTriggers() => _triggers.Keys.ToList();

        
    }
}


