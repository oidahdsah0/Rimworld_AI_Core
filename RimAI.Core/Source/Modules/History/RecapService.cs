using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Modules.LLM;
using RimAI.Core.Services;
using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Contracts.Services;
using InfraConfigService = RimAI.Core.Infrastructure.Configuration.IConfigurationService;

namespace RimAI.Core.Modules.History
{
    /// <summary>
    /// 总结/前情提要服务的最小占位实现（M1）。
    /// 当前不进行实际 LLM 调用，仅维护轮次计数。
    /// </summary>
    internal sealed class RecapService : IRecapService
    {
        private readonly ILLMService _llm;
        private readonly IHistoryWriteService _history;
        private readonly IHistoryQueryService _historyQuery;
        private readonly InfraConfigService _config;
        private readonly ConcurrentDictionary<string, int> _roundCounters = new();
        private readonly ConcurrentDictionary<string, List<RecapItem>> _recapDict = new();
        private readonly ConcurrentDictionary<string, object> _locks = new();

        public RecapService(ILLMService llm, IHistoryWriteService history, IHistoryQueryService historyQuery, InfraConfigService config)
        {
            _llm = llm;
            _history = history;
            _historyQuery = historyQuery;
            _config = config;
            // M1：不订阅事件源，由调用方手动触发 OnEntryRecorded
        }

        public void OnEntryRecorded(string convKey, ConversationEntry entry)
        {
            if (string.IsNullOrWhiteSpace(convKey) || entry == null) return;
            var count = _roundCounters.AddOrUpdate(convKey, 1, (_, n) => n + 1);

            var cfg = _config?.Current?.History ?? new HistoryConfig();

            // 到达 N 轮阈值，触发一次总结（后台）
            if (cfg.SummaryEveryNRounds > 0 && count % cfg.SummaryEveryNRounds == 0)
            {
                _ = Task.Run(() => TrySummarizeAsync(convKey, cfg));
            }

            // 到达“每十轮”阈值，触发叠加压缩（后台）
            if (cfg.RecapUpdateEveryRounds > 0 && count % cfg.RecapUpdateEveryRounds == 0)
            {
                _ = Task.Run(() => TryAggregateEveryTenAsync(convKey, cfg));
            }
        }

        public void OnEveryTenRounds(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return;
            var cfg = _config?.Current?.History ?? new HistoryConfig();
            _ = Task.Run(() => TryAggregateEveryTenAsync(convKey, cfg));
        }

        public Task RebuildRecapAsync(string convKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return Task.CompletedTask;

            return Task.Run(async () =>
            {
                var cfg = _config?.Current?.History ?? new HistoryConfig();
                var gate = _locks.GetOrAdd(convKey, _ => new object());
                lock (gate)
                {
                    _recapDict[convKey] = new List<RecapItem>();
                    _roundCounters[convKey] = 0;
                }

                // 读取已有历史并按轮次回放
                IReadOnlyList<string> ids = convKey.Split('|').ToList();
                var ctx = await _historyQuery.GetHistoryAsync(ids);
                var entries = ctx.MainHistory.SelectMany(c => c.Entries)
                    .OrderBy(e => e.Timestamp)
                    .ToList();

                foreach (var e in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    OnEntryRecorded(convKey, e);
                }
            }, ct);
        }

        public int GetCounter(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return 0;
            return _roundCounters.TryGetValue(convKey, out var n) ? n : 0;
        }

        // --- 内部：获取 recap 只读视图（为后续 UI/M4 做准备） ---
        internal IReadOnlyList<RecapItem> GetRecap(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return Array.Empty<RecapItem>();
            if (_recapDict.TryGetValue(convKey, out var list))
            {
                lock (_locks.GetOrAdd(convKey, _ => new object()))
                {
                    return list.ToList();
                }
            }
            return Array.Empty<RecapItem>();
        }

        public IReadOnlyList<RecapSnapshotItem> GetRecapItems(string convKey)
        {
            var items = GetRecap(convKey);
            return items
                .Select(i => new RecapSnapshotItem(i.Id, i.Text, i.CreatedAt))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();
        }

        public bool UpdateRecapItem(string convKey, string itemId, string newText)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(itemId)) return false;
            var gate = _locks.GetOrAdd(convKey, _ => new object());
            lock (gate)
            {
                if (!_recapDict.TryGetValue(convKey, out var list)) return false;
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                var old = list[idx];
                list[idx] = new RecapItem(old.Id, Truncate(newText ?? string.Empty, Math.Max(200, (_config?.Current?.History?.RecapMaxChars ?? 1200))), old.CreatedAt);
                return true;
            }
        }

        public bool RemoveRecapItem(string convKey, string itemId)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(itemId)) return false;
            var gate = _locks.GetOrAdd(convKey, _ => new object());
            lock (gate)
            {
                if (!_recapDict.TryGetValue(convKey, out var list)) return false;
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                list.RemoveAt(idx);
                return true;
            }
        }

        public bool ReorderRecapItem(string convKey, string itemId, int newIndex)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(itemId)) return false;
            var gate = _locks.GetOrAdd(convKey, _ => new object());
            lock (gate)
            {
                if (!_recapDict.TryGetValue(convKey, out var list)) return false;
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                newIndex = Math.Max(0, Math.Min(newIndex, list.Count - 1));
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(newIndex, item);
                return true;
            }
        }

        // --- 核心实现 ---
        private async Task TrySummarizeAsync(string convKey, HistoryConfig cfg)
        {
            var gate = _locks.GetOrAdd(convKey, _ => new object());
            try
            {
                // 取最近 N 轮最终输出
                IReadOnlyList<string> ids = convKey.Split('|').ToList();
                var ctx = await _historyQuery.GetHistoryAsync(ids);
                var all = ctx.MainHistory.SelectMany(c => c.Entries)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(cfg.SummaryEveryNRounds)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
                if (all.Count == 0) return;

                string prompt = BuildSummaryPrompt(all);
                var cts = new CancellationTokenSource(Math.Max(1000, cfg.Budget?.MaxLatencyMs ?? 5000));
                string text = null;
                try
                {
                    text = await _llm.GetResponseAsync(prompt, forceJson: false, ct: cts.Token);
                }
                catch (Exception ex)
                {
                    CoreServices.Logger.Warn($"[Recap] Summarize failed: {ex.Message}");
                    return;
                }

                if (string.IsNullOrWhiteSpace(text)) return;
                text = Truncate(text, Math.Max(200, cfg.RecapMaxChars));

                var item = new RecapItem(Guid.NewGuid().ToString("N"), text, DateTime.UtcNow);
                lock (gate)
                {
                    if (!_recapDict.TryGetValue(convKey, out var list))
                    {
                        list = new List<RecapItem>();
                        _recapDict[convKey] = list;
                    }
                    list.Add(item);
                    EnforceCapacity(list, cfg.RecapDictMaxEntries);
                }
                CoreServices.Logger.Info($"[Recap] +Summary conv={convKey}, len={text.Length}");
            }
            catch (Exception ex)
            {
                CoreServices.Logger.Warn($"[Recap] TrySummarizeAsync error: {ex.Message}");
            }
        }

        private async Task TryAggregateEveryTenAsync(string convKey, HistoryConfig cfg)
        {
            var gate = _locks.GetOrAdd(convKey, _ => new object());
            try
            {
                // 取最近 10 轮（若 SummaryEveryNRounds!=10，仍以配置的阈值为逻辑窗口）
                IReadOnlyList<string> ids = convKey.Split('|').ToList();
                var ctx = await _historyQuery.GetHistoryAsync(ids);
                var all = ctx.MainHistory.SelectMany(c => c.Entries)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(cfg.RecapUpdateEveryRounds)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
                if (all.Count == 0) return;

                string prompt = BuildAggregatePrompt(all);
                var cts = new CancellationTokenSource(Math.Max(1000, cfg.Budget?.MaxLatencyMs ?? 5000));
                string text = null;
                try
                {
                    text = await _llm.GetResponseAsync(prompt, forceJson: false, ct: cts.Token);
                }
                catch (Exception ex)
                {
                    CoreServices.Logger.Warn($"[Recap] Aggregate failed: {ex.Message}");
                    // 退化为拼接 + 裁剪
                    text = string.Join("\n", all.Select(e => e.Content));
                }

                if (string.IsNullOrWhiteSpace(text)) return;
                text = Truncate(text, Math.Max(200, cfg.RecapMaxChars));

                var item = new RecapItem(Guid.NewGuid().ToString("N"), text, DateTime.UtcNow);
                lock (gate)
                {
                    if (!_recapDict.TryGetValue(convKey, out var list))
                    {
                        list = new List<RecapItem>();
                        _recapDict[convKey] = list;
                    }
                    list.Add(item);
                    EnforceCapacity(list, cfg.RecapDictMaxEntries);
                }
                CoreServices.Logger.Info($"[Recap] +Aggregate conv={convKey}, len={text.Length}");
            }
            catch (Exception ex)
            {
                CoreServices.Logger.Warn($"[Recap] TryAggregateEveryTenAsync error: {ex.Message}");
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Length <= max) return text;
            return text.Substring(0, Math.Max(0, max));
        }

        private static void EnforceCapacity(List<RecapItem> list, int maxEntries)
        {
            if (maxEntries <= 0) return; // 0/负数=无限
            while (list.Count > maxEntries)
            {
                list.RemoveAt(0);
            }
        }

        private static string BuildSummaryPrompt(List<ConversationEntry> entries)
        {
            // 简化：拼接最近 N 轮，提示进行要点摘要
            var lines = new List<string> { "请将以下对话要点总结为一段简短上下文：" };
            foreach (var e in entries)
            {
                lines.Add("- " + e.Content);
            }
            return string.Join("\n", lines);
        }

        private static string BuildAggregatePrompt(List<ConversationEntry> entries)
        {
            var lines = new List<string> { "请将以下最近对话压缩成一条‘前情提要’：" };
            foreach (var e in entries)
            {
                lines.Add("- " + e.Content);
            }
            return string.Join("\n", lines);
        }

        internal readonly struct RecapItem
        {
            public string Id { get; }
            public string Text { get; }
            public DateTime CreatedAt { get; }
            public RecapItem(string id, string text, DateTime createdAt)
            {
                Id = id;
                Text = text;
                CreatedAt = createdAt;
            }
        }

        // RecapViewItem removed; use RecapSnapshotItem defined in IRecapService

        public IReadOnlyDictionary<string, IReadOnlyList<RecapSnapshotItem>> ExportSnapshot()
        {
            var dict = new Dictionary<string, IReadOnlyList<RecapSnapshotItem>>();
            foreach (var kvp in _recapDict)
            {
                var list = kvp.Value?.Select(x => new RecapSnapshotItem(x.Id, x.Text, x.CreatedAt)).ToList() ?? new List<RecapSnapshotItem>();
                dict[kvp.Key] = list;
            }
            return dict;
        }

        public void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyList<RecapSnapshotItem>> snapshot)
        {
            _recapDict.Clear();
            if (snapshot == null) return;
            foreach (var kvp in snapshot)
            {
                _recapDict[kvp.Key] = kvp.Value?.Select(x => new RecapItem(x.Id, x.Text, x.CreatedAt)).ToList() ?? new List<RecapItem>();
            }
        }
    }
}


