using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Globalization;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Tooling.Indexing;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.Modules.Tooling
{
	internal sealed class ToolRegistryService : IToolRegistryService
	{
		private readonly ILLMService _llm;
		private readonly IPersistenceService _persistence;
		private readonly ToolIndexManager _index;
		private readonly ConfigurationService _cfgService;
		private readonly ILocalizationService _loc;

		// 简化：先用内存中的演示工具列表；后续可加 ToolDiscovery 反射扫描
		private readonly List<IRimAITool> _allTools;

		// 索引配置（从内部配置读取）
		private readonly string _indexBasePath;
		private readonly string _indexFileName;
		private readonly (double name, double desc, double @params) _weights;
		private readonly string _provider;
		private readonly string _model;
		private readonly int _dimension;
		private readonly string _instruction;

		public ToolRegistryService(ILLMService llm, IPersistenceService persistence, IConfigurationService configurationService, ILocalizationService localization)
		{
			_llm = llm;
			_persistence = persistence;
			_index = new ToolIndexManager(llm, persistence);
			_allTools = ToolDiscovery.DiscoverTools();
			// 读取内部配置（构造函数注入，禁止 Service Locator）
			_cfgService = configurationService as ConfigurationService;
			_loc = localization;
			var tooling = _cfgService?.GetToolingConfig();
			if (tooling == null) tooling = new CoreConfig.ToolingSection();
			_indexBasePath = tooling.IndexFiles?.BasePath ?? "Config/RimAI/Indices";
			_indexFileName = tooling.IndexFiles?.FileNameFormat ?? "tools_index_{provider}_{model}.json";
			_weights = (tooling.Embedding?.Weights?.Name ?? 0.6, tooling.Embedding?.Weights?.Desc ?? 0.4, tooling.Embedding?.Weights?.Params ?? 0.0);
			_provider = tooling.Embedding?.Provider ?? "auto";
			_model = tooling.Embedding?.Model ?? "auto";
			_dimension = tooling.Embedding?.Dimension ?? 0;
			_instruction = tooling.Embedding?.Instruction ?? string.Empty;

			// 订阅配置变化：Embedding 相关字段变化 → 标记过期并重建
			if (_cfgService != null)
			{
				_cfgService.OnConfigurationChanged += snap =>
				{
					try
					{
						var t = _cfgService.GetToolingConfig();
						if (t == null) return;
						var newProvider = t.Embedding?.Provider ?? _provider;
						var newModel = t.Embedding?.Model ?? _model;
						var newDim = t.Embedding?.Dimension ?? _dimension;
						var newInstr = t.Embedding?.Instruction ?? _instruction;
						var newWeights = (t.Embedding?.Weights?.Name ?? _weights.name, t.Embedding?.Weights?.Desc ?? _weights.desc, t.Embedding?.Weights?.Params ?? _weights.@params);
						var changed = newProvider != _provider || newModel != _model || newDim != _dimension || newInstr != _instruction || newWeights != _weights;
						if (changed)
						{
							MarkIndexStale();
							_ = RebuildIndexAsync();
						}
					}
					catch { }
				};
			}
		}

		public ToolClassicResult GetClassicToolCallSchema(ToolQueryOptions options = null)
		{
			var tools = _allTools.AsEnumerable();
			if (options?.IncludeWhitelist != null && options.IncludeWhitelist.Count > 0)
			{
				var set = new HashSet<string>(options.IncludeWhitelist, StringComparer.OrdinalIgnoreCase);
				tools = tools.Where(t => set.Contains(t.Name ?? string.Empty));
			}
			if (options?.ExcludeBlacklist != null && options.ExcludeBlacklist.Count > 0)
			{
				var set = new HashSet<string>(options.ExcludeBlacklist, StringComparer.OrdinalIgnoreCase);
				tools = tools.Where(t => !set.Contains(t.Name ?? string.Empty));
			}
			var toolList = tools.ToList();
			var json = toolList.Select(t => t.BuildToolJson());
			return new ToolClassicResult { ToolsJson = json.ToList() };
		}

		public async Task<ToolNarrowTopKResult> GetNarrowTopKToolCallSchemaAsync(string userInput, int k, double? minScore, ToolQueryOptions options = null, CancellationToken ct = default)
		{
			if (!_index.IsReady())
			{
				throw new ToolIndexNotReadyException("index_not_ready");
			}

			var embed = await _llm.GetEmbeddingsAsync(userInput, ct);
			if (!embed.IsSuccess || embed.Value?.Data == null || embed.Value.Data.Count == 0)
			{
				return new ToolNarrowTopKResult { ToolsJson = Array.Empty<string>(), Scores = Array.Empty<ToolScore>() };
			}
			var q = embed.Value.Data[0].Embedding?.Select(x => (float)x).ToArray() ?? Array.Empty<float>();

			var snapshot = _index.GetSnapshot() ?? throw new ToolIndexNotReadyException("index_load_failed");

			IReadOnlyList<ToolScore> scores = RankTopK(q, snapshot, k, minScore);
			var topNames = new HashSet<string>(scores.Select(s => s.ToolName), StringComparer.OrdinalIgnoreCase);
			var selected = _allTools.Where(t => topNames.Contains(t.Name ?? string.Empty)).Select(t => t.BuildToolJson()).ToList();
			return new ToolNarrowTopKResult { ToolsJson = selected, Scores = scores };
		}

		private IReadOnlyList<ToolScore> RankTopK(float[] query, ToolIndexSnapshot snapshot, int k, double? minScore)
		{
			// 将记录按工具名分组，并分别收集三类变体向量
			var groups = new Dictionary<string, (List<float[]> name, List<float[]> desc, List<float[]> param)>(StringComparer.OrdinalIgnoreCase);
			foreach (var r in snapshot.Records)
			{
				if (!groups.TryGetValue(r.ToolName, out var tuple))
				{
					tuple = (new List<float[]>(), new List<float[]>(), new List<float[]>());
					groups[r.ToolName] = tuple;
				}
				if (r.Variant == "name") tuple.name.Add(r.Vector);
				else if (r.Variant == "description") tuple.desc.Add(r.Vector);
				else if (r.Variant == "parameters") tuple.param.Add(r.Vector);
			}

			var list = new List<ToolScore>();
			foreach (var kv in groups)
			{
				var s = CosineBest(query, kv.Value.name) * _weights.name + CosineBest(query, kv.Value.desc) * _weights.desc + CosineBest(query, kv.Value.param) * _weights.@params;
				if (minScore == null || s >= minScore.Value)
				{
					list.Add(new ToolScore { ToolName = kv.Key, Score = s });
				}
			}
			return list.OrderByDescending(x => x.Score).Take(k).ToList();
		}

		private static double CosineBest(float[] query, List<float[]> list)
		{
			if (list == null || list.Count == 0 || query == null || query.Length == 0) return 0.0;
			double best = 0.0;
			for (int i = 0; i < list.Count; i++)
			{
				var s = CosineSimilarity(query, list[i]);
				if (s > best) best = s;
			}
			return best;
		}

		private static double CosineSimilarity(float[] a, float[] b)
		{
			if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0.0;
			double dot = 0, na = 0, nb = 0;
			for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
			if (na == 0 || nb == 0) return 0.0;
			return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
		}

		public Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> args, CancellationToken ct = default)
		{
			// S10 最小演示：返回固定对象（只读工具，无副作用）
			if (string.Equals(toolName, "get_colony_status", StringComparison.OrdinalIgnoreCase))
			{
				var result = new { name = "Colony", population = 0, wealth = 0 };
				return Task.FromResult<object>(result);
			}
			if (string.Equals(toolName, "get_pawn_health", StringComparison.OrdinalIgnoreCase))
			{
				if (args == null || !args.TryGetValue("pawn_id", out var v) || v == null)
					throw new ArgumentException("missing pawn_id");
				var pawnId = Convert.ToInt32(v, CultureInfo.InvariantCulture);
				IWorldDataService world = null;
				try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
				if (world == null)
				{
					// 回退：工具注册期不可用时（不应发生），返回占位
					return Task.FromResult<object>(new { pawn_id = pawnId, ok = false });
				}
				return world.GetPawnHealthSnapshotAsync(pawnId, ct).ContinueWith<Task<object>>(t =>
				{
					if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { pawn_id = pawnId, ok = false });
					var s = t.Result;
					var avg = (s.Consciousness + s.Moving + s.Manipulation + s.Sight + s.Hearing + s.Talking + s.Breathing + s.BloodPumping + s.BloodFiltration + s.Metabolism) / 10f * 100f;
					object res = new
					{
						pawn_id = s.PawnLoadId,
						avg = avg,
						is_dead = s.IsDead
					};
					return Task.FromResult(res);
				}).Unwrap();
			}
			if (string.Equals(toolName, "get_beauty_average", StringComparison.OrdinalIgnoreCase))
			{
				if (args == null || !args.TryGetValue("x", out var vx) || !args.TryGetValue("z", out var vz) || !args.TryGetValue("radius", out var vr))
					throw new ArgumentException("missing x|z|radius");
				var cx = Convert.ToInt32(vx, CultureInfo.InvariantCulture);
				var cz = Convert.ToInt32(vz, CultureInfo.InvariantCulture);
				var r = Convert.ToInt32(vr, CultureInfo.InvariantCulture);
				IWorldDataService world = null;
				try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
				if (world == null) return Task.FromResult<object>(new { ok = false });
				return world.GetBeautyAverageAsync(cx, cz, r, ct).ContinueWith<Task<object>>(t =>
				{
					if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
					object res = new { x = cx, z = cz, radius = r, avg = t.Result };
					return Task.FromResult(res);
				}).Unwrap();
			}
			if (string.Equals(toolName, "get_terrain_group_counts", StringComparison.OrdinalIgnoreCase))
			{
				if (args == null || !args.TryGetValue("x", out var vx) || !args.TryGetValue("z", out var vz) || !args.TryGetValue("radius", out var vr))
					throw new ArgumentException("missing x|z|radius");
				var cx = Convert.ToInt32(vx, CultureInfo.InvariantCulture);
				var cz = Convert.ToInt32(vz, CultureInfo.InvariantCulture);
				var r = Convert.ToInt32(vr, CultureInfo.InvariantCulture);
				IWorldDataService world = null;
				try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
				if (world == null) return Task.FromResult<object>(new { ok = false });
				return world.GetTerrainCountsAsync(cx, cz, r, ct).ContinueWith<Task<object>>(t =>
				{
					if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
					object res = new { x = cx, z = cz, radius = r, counts = t.Result };
					return Task.FromResult(res);
				}).Unwrap();
			}
			if (string.Equals(toolName, "get_game_logs", StringComparison.OrdinalIgnoreCase))
			{
				int count = 30;
				if (args != null && args.TryGetValue("count", out var vc) && vc != null)
				{
					try { count = Convert.ToInt32(vc, CultureInfo.InvariantCulture); } catch { count = 30; }
				}
				IWorldDataService world = null;
				try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
				if (world == null) return Task.FromResult<object>(new { ok = false });
				return world.GetRecentGameLogsAsync(count, ct).ContinueWith<Task<object>>(t =>
				{
					if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
					object res = new { ok = true, items = t.Result };
					return Task.FromResult(res);
				}).Unwrap();
			}
			throw new NotImplementedException(toolName);
		}

		public string GetToolDisplayNameOrNull(string toolName)
		{
			try
			{
				var t = _allTools.FirstOrDefault(x => string.Equals(x?.Name, toolName, StringComparison.OrdinalIgnoreCase));
				return t?.DisplayName;
			}
			catch { return null; }
		}

		public IReadOnlyList<string> GetRegisteredToolNames()
		{
			try { return _allTools.Select(t => t?.Name ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)).ToList(); }
			catch { return new List<string>(); }
		}

		public async Task<bool> CheckIndexMatchesToolsAsync(CancellationToken ct = default)
		{
			try
			{
				var snapshot = _index.GetSnapshot();
				if (snapshot == null || snapshot.Records == null || snapshot.Records.Count == 0) return false;
				var toolSet = new HashSet<string>(_allTools.Select(t => t?.Name ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
				var indexed = new HashSet<string>(snapshot.Records.Select(r => r.ToolName ?? string.Empty).Where(s => !string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
				// 至少应包含所有已注册工具（允许索引包含额外历史项，但通常不应）
				bool ok = toolSet.SetEquals(indexed);
				return await Task.FromResult(ok);
			}
			catch { return false; }
		}

		public bool IsIndexReady() => _index.IsReady();
		public Indexing.ToolIndexFingerprint GetIndexFingerprint() => _index.GetFingerprint();
		public Task EnsureIndexBuiltAsync(CancellationToken ct = default) => _index.EnsureBuiltAsync(_provider, _model, _dimension, _instruction, _weights, BuildRecordsAsync, _indexBasePath, _indexFileName, ct);
		public Task RebuildIndexAsync(CancellationToken ct = default) => _index.RebuildAsync(_provider, _model, _dimension, _instruction, _weights, BuildRecordsAsync, _indexBasePath, _indexFileName, ct);
		public void MarkIndexStale() => _index.MarkStale();

		private async Task<List<ToolEmbeddingRecord>> BuildRecordsAsync(CancellationToken ct)
		{
			// 采集语料：name/description/parameters（示例使用 name/desc）
			var records = new List<ToolEmbeddingRecord>();
			try
			{
				var toolNames = _allTools.Select(x => x.Name ?? string.Empty).ToList();
				Verse.Log.Message($"[RimAI.Core][P4] tools=[{string.Join(", ", toolNames)}] count={toolNames.Count}");
			}
			catch { }
			foreach (var t in _allTools)
			{
				var nameText = (t.Name ?? string.Empty).Trim();
				var descText = (t.Description ?? string.Empty).Trim();
				var displayText = (t.DisplayName ?? string.Empty).Trim();
				var texts = new List<(string variant, string text)> { ("name", nameText) };
				if (!string.IsNullOrEmpty(descText)) texts.Add(("description", descText));
				if (!string.IsNullOrEmpty(displayText)) texts.Add(("display", displayText));

				foreach (var p in texts)
				{
					var e = await _llm.GetEmbeddingsAsync(p.text, ct);
					if (!e.IsSuccess || e.Value?.Data == null || e.Value.Data.Count == 0) continue;
					var vec = e.Value.Data[0].Embedding?.Select(x => (float)x).ToArray() ?? Array.Empty<float>();
					// Debug: 打印向量前三位
					try
					{
						var head = (vec ?? Array.Empty<float>()).Take(3).Select(x => x.ToString("0.###", CultureInfo.InvariantCulture));
						Verse.Log.Message($"[RimAI.Core][P4] vec tool={t.Name} variant={p.variant} head=[{string.Join(", ", head)}] len={vec.Length}");
					}
					catch { }
					records.Add(new ToolEmbeddingRecord
					{
						Id = Guid.NewGuid().ToString("N"),
						ToolName = t.Name ?? string.Empty,
						Variant = p.variant,
						Text = p.text,
						Vector = vec,
						Provider = _provider,
						Model = _model,
						Dimension = vec?.Length ?? 0,
						Instruction = _instruction,
						BuiltAtUtc = DateTime.UtcNow
					});
				}
			}
			return records;
		}
	}

	internal sealed class ToolIndexNotReadyException : Exception
	{
		public ToolIndexNotReadyException(string message) : base(message) { }
	}
}


