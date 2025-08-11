using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Stage.Topic
{
    internal sealed class TopicResult
    {
        public string Topic { get; set; } = string.Empty;
        public string ScenarioText { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public double? Score { get; set; }
    }

    internal sealed class TopicContext
    {
        public string ConvKey { get; set; } = string.Empty;
        public IReadOnlyList<string> Participants { get; set; } = new List<string>();
        public int Seed { get; set; } = 0;
        public string Locale { get; set; } = null;
        // 可扩展：recentRecaps/recentHistory/worldSnapshot/relationsSnapshot
    }

    internal interface ITopicProvider
    {
        string Name { get; }
        Task<TopicResult> GetTopicAsync(TopicContext ctx, CancellationToken ct = default);
    }

    internal interface ITopicService
    {
        Task<TopicResult> SelectAsync(TopicContext ctx, IReadOnlyDictionary<string, double> weights, CancellationToken ct = default);
    }
}


