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
using RimAI.Core.Source.Modules.Tooling.Execution;
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
        private readonly IWorldDataService _world;
        private readonly bool _topkAvailable;

		// 简化：先用内存中的演示工具列表；后续可加 ToolDiscovery 反射扫描
		private readonly List<IRimAITool> _allTools;
		// 执行器分发表（按工具名）
		private readonly Dictionary<string, IToolExecutor> _executors;

		// 索引配置（从内部配置读取）
		private readonly string _indexBasePath;
		private readonly string _indexFileName;
		private readonly (double name, double desc, double @params) _weights;
		private readonly string _provider;
		private readonly string _model;
		private readonly int _dimension;
		private readonly string _instruction;
		private readonly ToolEmbeddingDataSource _embeddingDataSource;

		public ToolRegistryService(ILLMService llm, IPersistenceService persistence, IConfigurationService configurationService, ILocalizationService localization)
		{
			_llm = llm;
			_persistence = persistence;
			_index = new ToolIndexManager(llm, persistence);
			_allTools = ToolDiscovery.DiscoverTools();
			// 1) 自动发现执行器
			var discovered = ExecutorDiscovery.DiscoverExecutors();
			var execMap = new Dictionary<string, IToolExecutor>(StringComparer.OrdinalIgnoreCase);
			foreach (var e in discovered)
			{
				if (e == null || string.IsNullOrWhiteSpace(e.Name)) continue;
				// 若同名已存在，后者覆盖前者（记录日志）
				if (execMap.ContainsKey(e.Name))
				{
					try { Verse.Log.Warning($"[RimAI.Core] Duplicate executor discovered for '{e.Name}', latter overrides former."); } catch { }
				}
				execMap[e.Name] = e;
			}
			// 2) 手动覆盖（最稳方案保留兜底）
			var manual = new Dictionary<string, IToolExecutor>(StringComparer.OrdinalIgnoreCase)
			{
				["get_colony_status"] = new ColonyStatusExecutor(),
				["get_pawn_health"] = new PawnHealthExecutor(),
				["get_resource_overview"] = new ResourceOverviewExecutor(),
				["get_beauty_average"] = new BeautyAverageExecutor(),
				["get_terrain_group_counts"] = new TerrainGroupCountsExecutor(),
				["get_game_logs"] = new GameLogsExecutor(),
				["get_power_status"] = new PowerStatusExecutor(),
				["get_weather_status"] = new WeatherStatusExecutor(),
				["get_storage_saturation"] = new StorageSaturationExecutor(),
				["get_research_options"] = new ResearchOptionsExecutor(),
				["get_construction_backlog"] = new ConstructionBacklogExecutor(),
				["get_security_posture"] = new SecurityPostureExecutor(),
				["get_medical_overview"] = new MedicalOverviewExecutor(),
				["get_wildlife_opportunities"] = new WildlifeOpportunitiesExecutor()
			};
			// Action tools
			manual["set_forced_weather"] = new Execution.SetForcedWeatherExecutor();
			// 扩展注册
			manual["get_mood_risk_overview"] = new MoodRiskOverviewExecutor();
			manual["get_trade_readiness"] = new TradeReadinessExecutor();
			manual["get_animal_management"] = new AnimalManagementExecutor();
			manual["get_prison_overview"] = new PrisonOverviewExecutor();
			manual["get_alert_digest"] = new AlertDigestExecutor();
			manual["get_raid_readiness"] = new RaidReadinessExecutor();

			foreach (var kv in manual)
			{
				execMap[kv.Key] = kv.Value; // 手动注册优先级更高
			}
			_executors = execMap;
			// 3) 覆盖率校验与日志
			try
			{
				var toolNames = new HashSet<string>(_allTools.Select(t => t?.Name ?? string.Empty).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
				var missing = toolNames.Where(n => !_executors.ContainsKey(n)).ToList();
				if (missing.Count > 0)
				{
					Verse.Log.Warning($"[RimAI.Core] Missing executors for tools: {string.Join(", ", missing)}");
				}
			}
			catch { }
			// 读取内部配置（构造函数注入，禁止 Service Locator）
			_cfgService = configurationService as ConfigurationService;
			_loc = localization;
            try { _world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IWorldDataService>(); } catch { _world = null; }
			// Embedding 总开关：由 LLMService 桥接 Framework 的 IsEmbeddingEnabled
			try { _topkAvailable = _llm?.IsEmbeddingEnabled() ?? false; } catch { _topkAvailable = false; }
			var tooling = _cfgService?.GetToolingConfig();
			if (tooling == null) tooling = new CoreConfig.ToolingSection();
			_indexBasePath = tooling.IndexFiles?.BasePath ?? "Config/RimAI/Indices";
			_indexFileName = tooling.IndexFiles?.FileNameFormat ?? "tools_index_{provider}_{model}.json";
			_weights = (tooling.Embedding?.Weights?.Name ?? 0.6, tooling.Embedding?.Weights?.Desc ?? 0.4, tooling.Embedding?.Weights?.Params ?? 0.0);
			_provider = tooling.Embedding?.Provider ?? "auto";
			_model = tooling.Embedding?.Model ?? "auto";
			_dimension = tooling.Embedding?.Dimension ?? 0;
			_instruction = tooling.Embedding?.Instruction ?? string.Empty;
			_embeddingDataSource = new ToolEmbeddingDataSource(_llm, _provider, _model, _instruction);

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
			// Scheme A：此方法仅做“原始集合”构建与可选黑白名单裁剪；
			// 不负责任何等级/研究过滤。单点鉴权集中在 BuildToolsAsync。
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
			var json = tools.Select(t => t.BuildToolJson());
			return new ToolClassicResult { ToolsJson = json.ToList() };
		}

		public bool IsTopKAvailable() => _topkAvailable;

		public async Task<ToolNarrowTopKResult> GetNarrowTopKToolCallSchemaAsync(string userInput, int k, double? minScore, ToolQueryOptions options = null, CancellationToken ct = default)
		{
			if (!_topkAvailable)
			{
				throw new ToolIndexNotReadyException("embedding_disabled");
			}
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
			// Scheme A：TopK 仅负责召回；不在此做等级/研究过滤。
			var selectedTools = _allTools.Where(t => topNames.Contains(t.Name ?? string.Empty));
			// 可选：应用黑白名单裁剪（非权限过滤，仅场景裁剪）
			if (options?.IncludeWhitelist != null && options.IncludeWhitelist.Count > 0)
			{
				var set = new HashSet<string>(options.IncludeWhitelist, StringComparer.OrdinalIgnoreCase);
				selectedTools = selectedTools.Where(t => set.Contains(t.Name ?? string.Empty));
			}
			if (options?.ExcludeBlacklist != null && options.ExcludeBlacklist.Count > 0)
			{
				var set = new HashSet<string>(options.ExcludeBlacklist, StringComparer.OrdinalIgnoreCase);
				selectedTools = selectedTools.Where(t => !set.Contains(t.Name ?? string.Empty));
			}
			var selected = selectedTools.Select(t => t.BuildToolJson()).ToList();
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
			if (string.IsNullOrWhiteSpace(toolName)) throw new ArgumentNullException(nameof(toolName));
			// 使用解耦的执行器分发表
			if (_executors != null && _executors.TryGetValue(toolName, out var ex))
			{
				return ex.ExecuteAsync(args ?? new Dictionary<string, object>(), ct);
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

		public async Task EnsureIndexReadyAsync(bool rebuildIfMissing, CancellationToken ct = default)
		{
			if (!_topkAvailable) return;
			if (_index.IsReady()) return;
			var snap = await _index.TryLoadAsync(_provider, _model, _indexBasePath, _indexFileName, ct).ConfigureAwait(false);
			if (snap == null && rebuildIfMissing)
			{
				await _index.EnsureBuiltAsync(_provider, _model, _dimension, _instruction, _weights, BuildRecordsAsync, _indexBasePath, _indexFileName, ct).ConfigureAwait(false);
			}
		}

		public async Task<(IReadOnlyList<string> toolsJson, IReadOnlyList<(string name, double score)> scores, string error)> BuildToolsAsync(
			RimAI.Core.Contracts.Config.ToolCallMode mode,
			string userInput,
			int? k,
			double? minScore,
			ToolQueryOptions options = null,
			CancellationToken ct = default)
		{
			if (mode == ToolCallMode.TopK)
			{
				if (!_topkAvailable)
				{
					return (Array.Empty<string>(), Array.Empty<(string, double)>(), "embedding_disabled");
				}
				try
				{
					var res = await GetNarrowTopKToolCallSchemaAsync(userInput ?? string.Empty, Math.Max(1, k ?? 5), minScore, options, ct).ConfigureAwait(false);
					var scores = res.Scores?.Select(s => (s.ToolName ?? string.Empty, s.Score)).ToList() ?? new List<(string,double)>();

					// 单点鉴权（TopK）：统一在此执行“等级 + 研究”过滤（异步安全）。
					var tools = res.ToolsJson?.ToList() ?? new List<string>();
					var nameSet = new HashSet<string>(tools.Select(ExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
					// 等级过滤
					int maxLv = Math.Max(1, options?.MaxToolLevel ?? 3);
					var candidates = _allTools.Where(t => nameSet.Contains(t?.Name ?? string.Empty))
						.Where(t => (t?.Level ?? 1) <= 3 && (t?.Level ?? 1) <= maxLv)
						.ToList();
					// 研究过滤（异步）
					if (_world != null)
					{
						foreach (var t in candidates)
						{
							if (t is IResearchGatedTool g && g.RequiredResearchDefNames != null)
							{
								foreach (var def in g.RequiredResearchDefNames)
								{
									if (string.IsNullOrWhiteSpace(def)) continue;
									var ok = await _world.IsResearchFinishedAsync(def, ct).ConfigureAwait(false);
									if (!ok) { nameSet.Remove(t.Name); break; }
								}
							}
						}
					}
					// 应用最终集合
					tools = tools.Where(j => nameSet.Contains(ExtractName(j) ?? string.Empty)).ToList();
					// 过滤分数，仅保留仍然可见的工具
					scores = scores.Where(p => nameSet.Contains(p.Item1)).ToList();
					return (tools, scores, null);
				}
				catch (ToolIndexNotReadyException ex)
				{
					return (Array.Empty<string>(), Array.Empty<(string,double)>(), ex.Message ?? "index_not_ready");
				}
			}
			else
			{
				var res = GetClassicToolCallSchema(options);
				var tools = res.ToolsJson?.ToList() ?? new List<string>();
				// 单点鉴权（Classic）：统一在此执行“等级 + 研究”过滤（异步安全）。
				var nameSet = new HashSet<string>(tools.Select(ExtractName).Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.OrdinalIgnoreCase);
				// 等级过滤
				int maxLv = Math.Max(1, options?.MaxToolLevel ?? 3);
				var candidates = _allTools.Where(t => nameSet.Contains(t?.Name ?? string.Empty))
					.Where(t => (t?.Level ?? 1) <= 3 && (t?.Level ?? 1) <= maxLv)
					.ToList();
				// 研究过滤（异步）
				if (_world != null)
				{
					foreach (var t in candidates)
					{
						if (t is IResearchGatedTool g && g.RequiredResearchDefNames != null)
						{
							foreach (var def in g.RequiredResearchDefNames)
							{
								if (string.IsNullOrWhiteSpace(def)) continue;
								var ok = await _world.IsResearchFinishedAsync(def, ct).ConfigureAwait(false);
								if (!ok) { nameSet.Remove(t.Name); break; }
							}
						}
					}
				}
				// 应用最终集合
				tools = tools.Where(j => nameSet.Contains(ExtractName(j) ?? string.Empty)).ToList();
				var names = tools.Take(5).Select(j => ExtractName(j)).Where(n => !string.IsNullOrEmpty(n)).Select(n => (n, 1.0)).ToList() ?? new List<(string,double)>();
				return (tools, names, null);
			}
		}

		private static string ExtractName(string toolJson)
		{
			if (string.IsNullOrEmpty(toolJson)) return null;
			var key = "\"Name\":";
			var idx = toolJson.IndexOf(key);
			if (idx < 0) return null;
			idx += key.Length;
			while (idx < toolJson.Length && (toolJson[idx] == ' ' || toolJson[idx] == '\t' || toolJson[idx] == '"' || toolJson[idx] == '\'' || toolJson[idx] == ':')) idx++;
			var end = idx;
			while (end < toolJson.Length && toolJson[end] != '"' && toolJson[end] != '\'' && toolJson[end] != ',' && toolJson[end] != '}') end++;
			return toolJson.Substring(idx, end - idx).Trim('"','\'',' ');
		}

		private Task<List<ToolEmbeddingRecord>> BuildRecordsAsync(CancellationToken ct)
		{
			return _embeddingDataSource.BuildRecordsAsync(_allTools, ct);
		}
	}

	internal sealed class ToolIndexNotReadyException : Exception
	{
		public ToolIndexNotReadyException(string message) : base(message) { }
	}
}


