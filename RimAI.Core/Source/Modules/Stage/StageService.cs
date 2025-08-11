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
using RimAI.Core.Modules.Stage.Topic;
using RimAI.Core.Modules.Stage.Acts;

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
    internal sealed class StageService : IStageService
    {
        private readonly IConfigurationService _config;
        private readonly IEventBus _events;
        private readonly IParticipantIdService _pid;
        private readonly IPersonaConversationService _persona;
        private readonly RimAI.Core.Services.IHistoryWriteService _history;

        private sealed class CoalesceBucket
        {
            public string ConvKey;
            public DateTime FirstSeenUtc;
            public List<StageRequest> Requests = new();
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
        private readonly ConcurrentDictionary<string, RecentResult> _idempotent = new(); // idempotencyKey -> result
        private readonly ConcurrentDictionary<string, RecentResult> _recentByConvKey = new(); // convKey -> last result

        public StageService(IConfigurationService config, IEventBus events, IParticipantIdService pid, IPersonaConversationService persona, RimAI.Core.Services.IHistoryWriteService history)
        {
            _config = config;
            _events = events;
            _pid = pid;
            _persona = persona;
            _history = history;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StartAsync(StageRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            request = request ?? new StageRequest { Participants = Array.Empty<string>() };
            var cfg = _config.Current?.Stage ?? new StageConfig();

            // 1) Eligibility
            var normalized = NormalizeParticipants(request.Participants, cfg);
            if (normalized.Count < Math.Max(2, cfg.MinParticipants))
            {
                yield return Result<UnifiedChatChunk>.Failure("TooFewParticipants");
                yield break;
            }
            if (!IsOriginPermitted(request.Origin, cfg))
            {
                yield return Result<UnifiedChatChunk>.Failure("OriginNotPermitted");
                yield break;
            }

            // 2) convKey & seed
            var convKey = string.Join("|", normalized.OrderBy(x => x, StringComparer.Ordinal));
            var seed = request.Seed ?? ComputeSeed(convKey);

            // 3) 冷却
            if (IsInCooldown(convKey, cfg))
            {
                yield return Result<UnifiedChatChunk>.Failure("InCooldown");
                yield break;
            }

            // 4) 幂等
            var idem = request.IdempotencyKey;
            if (!string.IsNullOrWhiteSpace(idem) && _idempotent.TryGetValue(idem, out var recent))
            {
                if ((DateTime.UtcNow - recent.CompletedUtc).TotalSeconds <= Math.Max(1, cfg.CooldownSeconds))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = recent.Text ?? string.Empty });
                    yield break;
                }
            }

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

                // 选主触发（priority/sourceId）
                StageRequest leader = null;
                lock (bucket)
                {
                    leader = bucket.Requests
                        .OrderByDescending(r => r?.Priority ?? 0)
                        .ThenBy(r => r?.SourceId ?? string.Empty, StringComparer.Ordinal)
                        .FirstOrDefault() ?? request;
                }

                PublishStageEvent("StageStarted", convKey, new { participants = normalized, origin = request.Origin, seed, requestCount = bucket.Requests.Count });

                // M3：选题与会话级场景提示（可选）
                var locale = request.Locale ?? _config.Current?.Stage?.LocaleOverride;
                bool scenarioInjected = false;
                if (_config.Current?.Stage?.Topic?.Enabled ?? true)
                {
                    try
                    {
                        var topicSvc = CoreServices.Locator.Get<ITopicService>();
                        var topicCtx = new TopicContext { ConvKey = convKey, Participants = normalized, Seed = seed, Locale = locale };
                        var weights = _config.Current?.Stage?.Topic?.Sources;
                        var selected = await topicSvc.SelectAsync(topicCtx, weights, ct);
                        if (!string.IsNullOrWhiteSpace(selected?.ScenarioText))
                        {
                            var fixedSvc = CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>();
                            fixedSvc?.UpsertConvKeyOverride(convKey, selected.ScenarioText);
                            scenarioInjected = true;
                            _events?.Publish(new OrchestrationProgressEvent
                            {
                                Source = nameof(StageService),
                                Stage = "TopicSelected",
                                Message = selected.Topic ?? string.Empty,
                                PayloadJson = JsonConvert.SerializeObject(new {
                                    convKey,
                                    seed,
                                    scenarioChars = selected.ScenarioText?.Length ?? 0,
                                    weights
                                })
                            });
                        }
                    }
                    catch { /* ignore topic errors */ }
                }

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
                        var groupAct = acts?.FirstOrDefault(a => string.Equals(a.Name, "GroupChat", StringComparison.OrdinalIgnoreCase));
                        if (groupAct != null && (normalized?.Count ?? 0) >= Math.Max(2, _config.Current?.Stage?.MinParticipants ?? 2))
                        {
                            var actCtx = new Acts.ActContext
                            {
                                ConvKey = convKey,
                                Participants = normalized,
                                Seed = seed,
                                Locale = locale,
                                Options = _config.Current?.Stage,
                                Persona = _persona,
                                History = _history,
                                ParticipantId = _pid,
                                Events = _events
                            };
                            var res = await groupAct.RunAsync(actCtx, cts.Token);
                            final = string.Empty; // 群聊逐轮写入历史
                        }
                        else if (string.Equals(request.Mode, "Command", StringComparison.OrdinalIgnoreCase))
                        {
                            var opts = new PersonaCommandOptions { Stream = false, Locale = locale, RequireBoundPersona = true, WriteHistory = false };
                            await foreach (var chunk in _persona.CommandAsync(normalized, personaName: null, userInput: request.UserInputOrScenario ?? string.Empty, options: opts, ct: cts.Token))
                            {
                                if (cts.IsCancellationRequested) break;
                                if (chunk.IsSuccess) final += chunk.Value?.ContentDelta ?? string.Empty; else error = chunk.Error;
                            }
                        }
                        else
                        {
                            var opts = new PersonaChatOptions { Stream = false, Locale = locale, WriteHistory = false };
                            await foreach (var chunk in _persona.ChatAsync(normalized, personaName: null, userInput: request.UserInputOrScenario ?? string.Empty, options: opts, ct: cts.Token))
                            {
                                if (cts.IsCancellationRequested) break;
                                if (chunk.IsSuccess) final += chunk.Value?.ContentDelta ?? string.Empty; else error = chunk.Error;
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

                // 历史写入（仅最终输出）
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
                catch { /* 历史失败不阻断主流程 */ }

                // 记录幂等
                if (!string.IsNullOrWhiteSpace(idem))
                {
                    _idempotent[idem] = new RecentResult { ConvKey = convKey, CompletedUtc = DateTime.UtcNow, Text = text };
                }
                // 设置冷却
                _cooldown[convKey] = DateTime.UtcNow;
                _recentByConvKey[convKey] = new RecentResult { ConvKey = convKey, CompletedUtc = DateTime.UtcNow, Text = text };

                PublishStageEvent("Coalesced", convKey, new { leader = leader?.SourceId, coalesced = true, count = bucket.Requests.Count });
                PublishStageEvent("Finished", convKey, new { ok = true, textLen = text.Length });

                // 清理合流桶
                _coalesce.TryRemove(convKey, out _);
                // 清理会话级场景提示覆盖
                try
                {
                    if (scenarioInjected)
                        CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>()?.DeleteConvKeyOverride(convKey);
                }
                catch { }

                yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = text });
            }
            finally
            {
                try { gate.Release(); } catch { }
            }
        }

        public async Task RunScanOnceAsync(CancellationToken ct = default)
        {
            var stageCfg = _config.Current?.Stage ?? new StageConfig();
            var maxNew = Math.Max(0, stageCfg?.Scan?.MaxNewConversationsPerScan ?? 2);
            var pid = _pid;
            var scans = CoreServices.Locator.Get<IEnumerable<RimAI.Core.Modules.Stage.Scan.IStageScan>>();
            var ctx = new RimAI.Core.Modules.Stage.Scan.ScanContext { Config = stageCfg, ParticipantId = pid };
            var suggestions = new List<RimAI.Core.Modules.Stage.Scan.ConversationSuggestion>();
            foreach (var scan in scans ?? Array.Empty<RimAI.Core.Modules.Stage.Scan.IStageScan>())
            {
                try
                {
                    var list = await scan.RunAsync(ctx, ct) ?? Array.Empty<RimAI.Core.Modules.Stage.Scan.ConversationSuggestion>();
                    suggestions.AddRange(list);
                }
                catch { }
            }
            // 去重（按 convKey）与限流
            var uniq = new List<RimAI.Core.Modules.Stage.Scan.ConversationSuggestion>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in suggestions)
            {
                var convKey = string.Join("|", (s.Participants ?? Array.Empty<string>()).OrderBy(x => x, StringComparer.Ordinal));
                if (string.IsNullOrWhiteSpace(convKey) || seen.Contains(convKey)) continue;
                seen.Add(convKey);
                uniq.Add(s);
                if (uniq.Count >= maxNew) break;
            }
            // 触发会话
            foreach (var s in uniq)
            {
                var req = new StageRequest
                {
                    Participants = s.Participants,
                    Origin = s.Origin,
                    InitiatorId = s.InitiatorId,
                    UserInputOrScenario = s.Scenario,
                    Priority = s.Priority,
                    Seed = s.Seed,
                    Stream = false,
                    Mode = "Chat"
                };
                await foreach (var _ in StartAsync(req, ct)) { /* 触发即可，结果忽略 */ }
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
    }
}


