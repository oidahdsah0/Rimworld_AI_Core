using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure.Configuration;
using Verse;
using RimWorld;

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
            // If file exists at startup, mark ready
            try { if (File.Exists(IndexFilePath)) _ready = true; } catch { /* ignore */ }
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

                Directory.CreateDirectory(Path.GetDirectoryName(IndexFilePath));
                var json = JsonConvert.SerializeObject(index, Formatting.None);
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
            var basePath = _config?.Current?.Embedding?.Tools?.IndexPath;
            if (string.IsNullOrWhiteSpace(basePath) || string.Equals(basePath, "auto", StringComparison.OrdinalIgnoreCase))
            {
                // 使用 ModSettings 目录下固定文件名
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RimWorld", "RimAI", "tools_index_hash64.json");
            }
            return basePath;
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
    }
}


