using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Infrastructure;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Eventing;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 发送逻辑、进度事件订阅与数据加载。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        private void SubscribeProgressOnce()
        {
            if (!string.Equals(_modeTitle, "命令", StringComparison.Ordinal)) return;
            if (_progressSubscribed) return;
            try
            {
                var bus = CoreServices.Locator.Get<IEventBus>();
                _progressHandler = (IEvent evt) =>
                {
                    try
                    {
                        var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                        var pc = cfg?.Current?.Orchestration?.Progress;
                        string template = pc?.DefaultTemplate ?? "[{Source}] {Stage}: {Message}";
                        string source = null, stage = null, message = evt.Describe();
                        string payload = null;
                        var t = evt.GetType();
                        var pStage = t.GetProperty("Stage");
                        var pSource = t.GetProperty("Source");
                        var pMessage = t.GetProperty("Message");
                        var pPayload = t.GetProperty("PayloadJson");
                        if (pStage != null) stage = pStage.GetValue(evt) as string;
                        if (pSource != null) source = pSource.GetValue(evt) as string;
                        if (pMessage != null) message = pMessage.GetValue(evt) as string ?? message;
                        if (pc?.StageTemplates != null && stage != null && pc.StageTemplates.TryGetValue(stage, out var st))
                            template = st;
                        string line = template
                            .Replace("{Source}", source ?? string.Empty)
                            .Replace("{Stage}", stage ?? string.Empty)
                            .Replace("{Message}", message ?? string.Empty);
                        _progressQueue.Enqueue(line);
                        if (pPayload != null)
                        {
                            payload = pPayload.GetValue(evt) as string;
                            int max = System.Math.Max(0, pc?.PayloadPreviewChars ?? 200);
                            if (!string.IsNullOrEmpty(payload))
                            {
                                if (payload.Length > max) payload = payload.Substring(0, max) + "…";
                                _progressQueue.Enqueue("  payload: " + payload);
                            }
                        }
                    }
                    catch
                    {
                        _progressQueue.Enqueue("[Progress] " + evt.Describe());
                    }
                };
                bus?.Subscribe(_progressHandler);
                _progressSubscribed = true;
            }
            catch { }
        }

        private void UnsubscribeProgress()
        {
            if (!_progressSubscribed) return;
            try
            {
                var bus = CoreServices.Locator.Get<IEventBus>();
                if (_progressHandler != null)
                {
                    bus?.Unsubscribe<IEvent>(_progressHandler);
                }
            }
            catch { }
            finally
            {
                _progressSubscribed = false;
                _progressHandler = null;
            }
        }

        private async Task SendAsync()
        {
            var mode = string.Equals(_modeTitle, "命令", StringComparison.Ordinal) ? SendMode.Command : SendMode.Chat;
            await SendAsync(mode);
        }

        private async Task SendAsync(SendMode mode)
        {
            if (_isSending) return;
            _isSending = true;
            _status = "处理中…";
            _cts?.Dispose();
            _cts = new System.Threading.CancellationTokenSource();
            var ct = _cts.Token;
            try
            {
                _pendingPlayerMessage = _inputText ?? string.Empty;
                _pendingTimestamp = DateTime.UtcNow;
                _inputText = string.Empty;

                var parts = (_convKeyInput ?? string.Empty).Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (parts.Count == 0)
                {
                    parts = new List<string> { _pidService.GetPlayerId(), "pawn:UNKNOWN" };
                    _convKeyInput = CanonicalizeConvKey(string.Join("|", parts));
                }

                // 闲聊走 Organizer→Compose→LLM；命令暂沿用既有路径（D3 再切）
                var svc = CoreServices.Locator.TryGetExisting<IPersonaConversationService>(out var personaSvc) ? personaSvc : null;
                _streamAssistantBuffer = string.Empty;
                _streamStartedAtUtc = DateTime.UtcNow;
                _streamLastDeltaAtUtc = _streamStartedAtUtc;
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    string final = string.Empty;
                    try
                    {
                        if (mode == SendMode.Command)
                        {
                            // 历史写入职责上移：Command 仍采用流式；历史在完成后由 UI 自行写入
                            var opts = new PersonaCommandOptions { Stream = true, WriteHistory = false };
                            try { _progressSb.Clear(); } catch { }
                            _streamAssistantBuffer = string.Empty;
                            SubscribeProgressOnce();
                            await foreach (var chunk in svc.CommandAsync(parts, null, _pendingPlayerMessage, opts, ct))
                            {
                                if (ct.IsCancellationRequested) break;
                                if (chunk.IsSuccess)
                                {
                                    var delta = chunk.Value?.ContentDelta ?? string.Empty;
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        _streamQueue.Enqueue(delta);
                                        final += delta;
                                        _streamLastDeltaAtUtc = DateTime.UtcNow;
                                        TriggerIndicatorOnChunk();
                                    }
                                }
                                else
                                {
                                    var err = chunk.Error ?? "未知错误";
                                    _progressQueue.Enqueue("[Error] " + err);
                                }
                                if (!string.IsNullOrEmpty(chunk.Value?.FinishReason)) break;
                                if ((DateTime.UtcNow - _streamLastDeltaAtUtc).TotalSeconds > 120)
                                {
                                    try { _cts?.Cancel(); } catch { }
                                    break;
                                }
                            }
                            if (!ct.IsCancellationRequested)
                            {
                                try
                                {
                                    // 优先复用已有会话，否则创建新会话
                                    var list = await _history.FindByConvKeyAsync(_convKeyInput);
                                    _selectedConversationId = list?.LastOrDefault() ?? _selectedConversationId;
                                    if (string.IsNullOrWhiteSpace(_selectedConversationId))
                                    {
                                        _selectedConversationId = _history.CreateConversation(parts);
                                    }
                                    // 统一在 UI 层落盘，仅记录最终输出
                                    var now = DateTime.UtcNow;
                                    var playerId = parts.FirstOrDefault(id => id.StartsWith("player:")) ?? _pidService.GetPlayerId();
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry(playerId, _pendingPlayerMessage ?? string.Empty, now));
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry("assistant", final ?? string.Empty, now.AddMilliseconds(1)));
                                }
                                catch { }
                            }
                            TriggerIndicatorOnCompleted();
                        }
                        else // Chat（闲聊）
                        {
                            // Organizer → Compose → ILLM 流式
                            var organizer = CoreServices.Locator.Get<RimAI.Core.Modules.Orchestration.PromptOrganizers.IPromptOrganizer>();
                            var ctxOrg = new RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptContext
                            {
                                Mode = RimAI.Core.Modules.Orchestration.PromptMode.Chat,
                                ParticipantIds = parts,
                                ConvKey = _convKeyInput,
                                Locale = _templateService.ResolveLocale(),
                                IncludeFlags = RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.Persona
                                              | RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.Beliefs
                                              | RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.Recap
                                              | RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.History
                                              | RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.World
                                              | RimAI.Core.Modules.Orchestration.PromptOrganizers.PromptIncludeFlags.Extras,
                            };
                            var input = await organizer.BuildAsync(ctxOrg, ct);
                            var systemPrompt = await CoreServices.Locator.Get<RimAI.Core.Modules.Orchestration.IPromptAssemblyService>()
                                .ComposeSystemPromptAsync(input, ct);
                            var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                            var req = new UnifiedChatRequest
                            {
                                Stream = true,
                                Messages = new List<ChatMessage>
                                {
                                    new ChatMessage{ Role = "system", Content = systemPrompt },
                                    new ChatMessage{ Role = "user", Content = _pendingPlayerMessage ?? string.Empty }
                                }
                            };
                            await foreach (var chunk in llm.StreamResponseAsync(req, ct))
                            {
                                if (ct.IsCancellationRequested) break;
                                if (chunk.IsSuccess)
                                {
                                    var delta = chunk.Value?.ContentDelta ?? string.Empty;
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        _streamQueue.Enqueue(delta);
                                        final += delta;
                                        _streamLastDeltaAtUtc = DateTime.UtcNow;
                                        TriggerIndicatorOnChunk();
                                    }
                                }
                                if (!string.IsNullOrEmpty(chunk.Value?.FinishReason)) break;
                                if ((DateTime.UtcNow - _streamLastDeltaAtUtc).TotalSeconds > 60)
                                {
                                    try { _cts?.Cancel(); } catch { }
                                    break;
                                }
                            }
                            if (!ct.IsCancellationRequested)
                            {
                                try
                                {
                                    if (string.IsNullOrWhiteSpace(_selectedConversationId))
                                    {
                                        _selectedConversationId = _history.CreateConversation(parts);
                                    }
                                    var now = DateTime.UtcNow;
                                    var playerId = parts.FirstOrDefault(id => id.StartsWith("player:")) ?? _pidService.GetPlayerId();
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry(playerId, _pendingPlayerMessage ?? string.Empty, now));
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry("assistant", final ?? string.Empty, now.AddMilliseconds(1)));
                                }
                                catch { }
                            }
                            TriggerIndicatorOnCompleted();
                        }
                    }
                    catch (Exception ex)
                    {
                        _streamQueue.Enqueue($"\n[Error] {ex.Message}\n");
                    }
                    finally
                    {
                        try { if (!ct.IsCancellationRequested) await ReloadEntriesAsync(); } catch { }
                        UnsubscribeProgress();
                        _streamAssistantBuffer = null;
                        _pendingPlayerMessage = null;
                        _isSending = false;
                        try { _cts?.Dispose(); } catch { }
                        _cts = null;
                    }
                });
                return;
            }
            catch (Exception ex)
            {
                _status = "失败: " + ex.Message;
                _isSending = false;
                _pendingPlayerMessage = null;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }
        }

        private async Task EnsureExactLoadOrCreateAsync()
        {
            try
            {
                var canon = CanonicalizeConvKey(_convKeyInput);
                _convKeyInput = canon;

                var list = await _history.FindByConvKeyAsync(canon);
                var ids = list?.ToList() ?? new List<string>();
                if (ids.Count == 0)
                {
                    var parts = canon.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (parts.Count == 0)
                    {
                        parts = new List<string> { _pidService.GetPlayerId(), "pawn:UNKNOWN" };
                        canon = CanonicalizeConvKey(string.Join("|", parts));
                        _convKeyInput = canon;
                    }
                    _selectedConversationId = _history.CreateConversation(parts);
                }
                else
                {
                    _selectedConversationId = ids.Last();
                }
                await ReloadEntriesAsync();
            }
            catch (Exception ex)
            {
                Messages.Message("加载聊天失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }

        private async Task ReloadEntriesAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedConversationId)) { _entries = new List<ConversationEntry>(); return; }
            try
            {
                var rec = await _history.GetConversationAsync(_selectedConversationId);
                _entries = rec?.Entries?.ToList() ?? new List<ConversationEntry>();
            }
            catch
            {
                _entries = new List<ConversationEntry>();
            }
        }

        private static string CanonicalizeConvKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var ids = key.Split('|').Select(s => s?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join("|", ids.OrderBy(x => x, StringComparer.Ordinal));
        }
    }
}


