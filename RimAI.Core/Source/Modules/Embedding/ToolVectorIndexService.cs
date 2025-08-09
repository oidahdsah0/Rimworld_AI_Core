using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure.Configuration;
using Verse;
using RimWorld;
using System.Runtime.InteropServices;

namespace RimAI.Core.Modules.Embedding
{
    /// <summary>
    /// 工具向量索引构建与落盘（最小实现）：
    /// - 为每个真实工具名生成 name/description 两条向量，并在匹配时对同名项聚合；
    /// - 构建触发：游戏开始/小人落地检测到索引不存在时；
    /// - 构建期间可阻断工具功能（S2.5 仅落盘与状态）。
    /// </summary>
    internal sealed class ToolVectorIndexService : IToolVectorIndexService
    {
        private readonly IEmbeddingService _embedding;
        private readonly IToolRegistryService _toolRegistry;
        private readonly IConfigurationService _config;
        private readonly object _gate = new object();
        private volatile bool _building;
        private volatile bool _ready;

        public bool IsReady => _ready;
        public bool IsBuilding => _building;
        public string IndexFilePath { get; private set; }

        public ToolVectorIndexService(IEmbeddingService embedding, IToolRegistryService toolRegistry, IConfigurationService config)
        {
            _embedding = embedding;
            _toolRegistry = toolRegistry;
            _config = config;
            IndexFilePath = ResolveIndexPath();
            // 启动阶段：若已存在任何索引文件，尝试加载最新的一份，避免重复构建
            try
            {
                var dir = ResolveIndexPath();
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "tools_index_*.json");
                    if (files != null && files.Length > 0)
                    {
                        // 最新修改时间的索引文件
                        var latest = files.OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc).First();
                        IndexFilePath = latest;
                        _ready = true;
                    }
                }
            }
            catch { /* ignore */ }
        }

        public async Task EnsureBuiltAsync()
        {
            if (_ready) return;
            if (_building) { await WaitUntilReadyAsync(); return; }

            lock (_gate)
            {
                if (_ready || _building) return;
                _building = true;
            }

            try
            {
                // Embedding 可用性检查
                var available = await _embedding.IsAvailableAsync();
                if (!available)
                {
                    RimAI.Core.Infrastructure.CoreServices.Logger.Warn("[ToolIndex] Embedding service not available; skip building.");
                    return;
                }

                // 读取 Framework 活跃 provider 与模型信息（用于指纹）
                string activeProvider = null;
                string modelName = null;
                int requestedDim = 0; // 0 means 'use default dimension'
                int actualDim = 0;
                try
                {
                    var fwMod = Verse.LoadedModManager.GetMod<RimAI.Framework.UI.RimAIFrameworkMod>();
                    var fwSettings = fwMod?.GetSettings<RimAI.Framework.UI.RimAIFrameworkSettings>();
                    activeProvider = string.IsNullOrEmpty(fwSettings?.ActiveEmbeddingProviderId) ? fwSettings?.ActiveChatProviderId : fwSettings?.ActiveEmbeddingProviderId;
                    // 模型名由用户配置（ModelOverride 或模板默认）——最终以一次 embedding 的结果维度为准
                    modelName = activeProvider ?? "auto";
                }
                catch { /* ignore */ }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var tools = _toolRegistry.GetAllToolSchemas();
                var entries = new List<IndexEntry>(Math.Max(1, tools.Count * 2));

                foreach (var t in tools)
                {
                    // name 向量
                    var nameVec = await _embedding.GetEmbeddingAsync(t.Name ?? string.Empty);
                    if (actualDim == 0 && nameVec != null) actualDim = nameVec.Length;
                    entries.Add(new IndexEntry { Tool = t.Name, Kind = "name", Vector = nameVec, Text = t.Name ?? string.Empty });

                    // description 向量
                    var descText = t.Description ?? string.Empty;
                    var descVec = await _embedding.GetEmbeddingAsync(descText);
                    if (actualDim == 0 && descVec != null) actualDim = descVec.Length;
                    entries.Add(new IndexEntry { Tool = t.Name, Kind = "description", Vector = descVec, Text = descText });
                }

                var index = new ToolVectorIndex
                {
                    Provider = activeProvider ?? "auto",
                    Model = modelName ?? "auto",
                    RequestedDimension = requestedDim,
                    Dimension = actualDim,
                    BuiltAtUtc = DateTime.UtcNow,
                    Entries = entries
                };

                // 根据 provider+model 决定实际文件名
                IndexFilePath = ResolveIndexPath(index.Provider, index.Model);
                var dir = Path.GetDirectoryName(IndexFilePath);
                if (string.IsNullOrWhiteSpace(dir)) dir = ResolveIndexPath();
                Directory.CreateDirectory(dir);
                var json = JsonConvert.SerializeObject(index, Formatting.None);
                // 基本校验后落盘
                if (index.Dimension <= 0 || index.Entries == null || index.Entries.Count == 0)
                {
                    throw new InvalidOperationException("Invalid tool index content (empty or dimension=0)");
                }
                File.WriteAllText(IndexFilePath, json, Encoding.UTF8);

                _ready = true;
                sw.Stop();
                var msg = $"[ToolIndex] Built {entries.Count} vectors → {IndexFilePath} | provider={index.Provider}, model={index.Model}, reqDim={index.RequestedDimension}, dim={index.Dimension}, elapsed={sw.ElapsedMilliseconds}ms";
                RimAI.Core.Infrastructure.CoreServices.Logger.Info(msg);
                try { Messages.Message("工具向量库已就绪", MessageTypeDefOf.PositiveEvent, historical: false); } catch { /* UI not ready */ }
            }
            catch (Exception ex)
            {
                RimAI.Core.Infrastructure.CoreServices.Logger.Error($"[ToolIndex] Build failed: {ex.Message}");
            }
            finally
            {
                _building = false;
            }
        }

        public void MarkStale() { _ready = false; }

        private async Task WaitUntilReadyAsync()
        {
            var start = DateTime.UtcNow;
            while (_building && (DateTime.UtcNow - start).TotalSeconds < 30)
            {
                await Task.Delay(100);
            }
        }

        private string ResolveIndexPath()
        {
            // 优先读取配置；否则落到 RimWorld LocalLow Config 路径
            var basePath = _config?.Current?.Embedding?.Tools?.IndexPath;
            if (!string.IsNullOrWhiteSpace(basePath) && !string.Equals(basePath, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return basePath;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var appDataDir = Directory.GetParent(local)?.Parent?.FullName ?? local; // .../AppData
                    var localLow = Path.Combine(appDataDir, "LocalLow");
                    return Path.Combine(localLow,
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    return Path.Combine(home,
                        "Library", "Application Support",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    return Path.Combine(home,
                        ".config", "unity3d",
                        "Ludeon Studios",
                        "RimWorld by Ludeon Studios",
                        "Config",
                        "RimAI_Core");
                }
            }
            catch { /* ignore */ }

            // 最后兜底到原先 LocalAppData/RimWorld/RimAI
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RimWorld", "RimAI");
        }

        private string ResolveIndexPath(string provider, string model)
        {
            var dir = ResolveIndexPath();
            Directory.CreateDirectory(dir);
            var file = $"tools_index_{Sanitize(provider)}_{Sanitize(model)}.json";
            return Path.Combine(dir, file);
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "auto";
            foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s;
        }

        private sealed class ToolVectorIndex
        {
            public string Provider { get; set; }
            public string Model { get; set; }
            public int RequestedDimension { get; set; }
            public int Dimension { get; set; }
            public DateTime BuiltAtUtc { get; set; }
            public List<IndexEntry> Entries { get; set; }
        }

        private sealed class IndexEntry
        {
            public string Tool { get; set; }
            public string Kind { get; set; } // name | description
            public float[] Vector { get; set; }
            public string Text { get; set; }
        }

        // ---- 查询 API（最小实现） ----
        public async Task<IReadOnlyList<ToolMatch>> SearchAsync(string query, IEnumerable<ToolFunction> candidates, int topK, double weightName, double weightDescription)
        {
            await EnsureBuiltAsync();
            if (!_ready) return Array.Empty<ToolMatch>();

            ToolVectorIndex index;
            try
            {
                var json = File.ReadAllText(IndexFilePath, Encoding.UTF8);
                index = JsonConvert.DeserializeObject<ToolVectorIndex>(json);
            }
            catch
            {
                return Array.Empty<ToolMatch>();
            }

            var qNameVec = await _embedding.GetEmbeddingAsync(query ?? string.Empty);
            var qDescVec = qNameVec; // 简化：统一使用同一向量

            var groups = index.Entries.GroupBy(e => e.Tool, StringComparer.OrdinalIgnoreCase);
            var result = new List<ToolMatch>();
            var candidateSet = new HashSet<string>((candidates ?? Enumerable.Empty<ToolFunction>()).Select(c => c?.Name ?? string.Empty), StringComparer.OrdinalIgnoreCase);

            foreach (var g in groups)
            {
                var toolName = g.Key;
                if (!candidateSet.Contains(toolName)) continue;
                var nameEntry = g.FirstOrDefault(e => string.Equals(e.Kind, "name", StringComparison.OrdinalIgnoreCase));
                var descEntry = g.FirstOrDefault(e => string.Equals(e.Kind, "description", StringComparison.OrdinalIgnoreCase));
                double score = 0;
                if (nameEntry?.Vector != null) score += weightName * Cosine(qNameVec, nameEntry.Vector);
                if (descEntry?.Vector != null) score += weightDescription * Cosine(qDescVec, descEntry.Vector);
                var schema = (candidates ?? Enumerable.Empty<ToolFunction>()).FirstOrDefault(t => string.Equals(t?.Name, toolName, StringComparison.OrdinalIgnoreCase));
                result.Add(new ToolMatch { Tool = toolName, Score = score, Schema = schema });
            }

            return result
                .OrderByDescending(m => m.Score)
                .Take(Math.Max(1, topK))
                .ToList();
        }

        public async Task<ToolMatch> SearchTop1Async(string query, IEnumerable<ToolFunction> candidates, double weightName, double weightDescription)
        {
            var list = await SearchAsync(query, candidates, topK: 1, weightName, weightDescription);
            return list.FirstOrDefault();
        }

        private static double Cosine(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a == null || b == null) return 0;
            var len = Math.Min(a.Count, b.Count);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na <= 1e-8 || nb <= 1e-8) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}


