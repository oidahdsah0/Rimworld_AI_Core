using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Services;

namespace RimAI.Core.Modules.Stage.Topic
{
    internal sealed class HistoryRecapProvider : ITopicProvider
    {
        public string Name => "HistoryRecap";
        private readonly IHistoryWriteService _history;

        public HistoryRecapProvider(IHistoryWriteService history)
        {
            _history = history;
        }

        public async Task<TopicResult> GetTopicAsync(TopicContext ctx, CancellationToken ct = default)
        {
            // 简化版：取最近会话的最后一条作为话题引子
            try
            {
                var ids = await _history.FindByConvKeyAsync(ctx.ConvKey);
                var lastId = ids?.LastOrDefault();
                if (!string.IsNullOrWhiteSpace(lastId))
                {
                    var conv = await _history.GetConversationAsync(lastId);
                    var last = conv?.Entries?.LastOrDefault();
                    if (last != null)
                    {
                        var title = $"继续围绕：{Truncate(last.Content, 60)}";
                        var scenario = $"本轮对话以此前内容为引子：{Truncate(last.Content, 200)}。请围绕该主题展开，保证观点有延续性。";
                        return new TopicResult { Topic = title, ScenarioText = scenario, Source = Name, Score = 0.8 };
                    }
                }
            }
            catch { }
            return new TopicResult { Topic = "自由交流", ScenarioText = string.Empty, Source = Name, Score = 0.2 };
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }

    internal sealed class RandomPoolProvider : ITopicProvider
    {
        public string Name => "RandomPool";
        private static readonly string[] Pool = new[]
        {
            "今日天气与农事安排",
            "最近一次战斗的反思",
            "谁负责厨房值日",
            "殖民地发展规划",
            "休闲时间的安排"
        };

        public Task<TopicResult> GetTopicAsync(TopicContext ctx, CancellationToken ct = default)
        {
            var rnd = new Random(unchecked(ctx.Seed ^ (int)0x9e3779b9));
            var t = Pool[rnd.Next(Pool.Length)];
            var scenario = $"这是一次友好的群聊，参与者将围绕“{t}”进行1~{Math.Max(2, ctx.Participants?.Count ?? 2)}轮交流。";
            return Task.FromResult(new TopicResult { Topic = t, ScenarioText = scenario, Source = Name, Score = 0.5 });
        }
    }

    internal sealed class TopicService : ITopicService
    {
        private readonly IEnumerable<ITopicProvider> _providers;
        private readonly IConfigurationService _config;

        public TopicService(IEnumerable<ITopicProvider> providers, IConfigurationService config)
        {
            _providers = providers ?? Enumerable.Empty<ITopicProvider>();
            _config = config;
        }

        public async Task<TopicResult> SelectAsync(TopicContext ctx, IReadOnlyDictionary<string, double> weights, CancellationToken ct = default)
        {
            var available = _providers.ToDictionary(p => p.Name, p => p);
            var active = weights?.Where(kv => kv.Value > 0 && available.ContainsKey(kv.Key)).ToList() ?? new List<KeyValuePair<string,double>>();
            if (active.Count == 0)
            {
                // 退化：任选一个 provider
                var any = available.Values.FirstOrDefault();
                return any == null ? new TopicResult() : await any.GetTopicAsync(ctx, ct);
            }
            var total = active.Sum(kv => kv.Value);
            var r = (new Random(ctx.Seed)).NextDouble() * total;
            double acc = 0;
            string chosen = active[0].Key;
            foreach (var kv in active)
            {
                acc += kv.Value;
                if (r <= acc) { chosen = kv.Key; break; }
            }
            var provider = available[chosen];
            return await provider.GetTopicAsync(ctx, ct);
        }
    }
}


