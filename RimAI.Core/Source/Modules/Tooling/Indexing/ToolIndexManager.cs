using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Persistence;

namespace RimAI.Core.Source.Modules.Tooling.Indexing
{
	internal enum ToolIndexState { Empty, Ready, Stale, Building, Error }

	internal sealed class ToolIndexManager
	{
		private readonly ILLMService _llm;
		private readonly IPersistenceService _persistence;
		private readonly ToolIndexStorage _storage;

		private readonly object _gate = new();
		private ToolIndexState _state = ToolIndexState.Empty;
		private ToolIndexSnapshot _snapshot;
		private Exception _lastError;

		public ToolIndexManager(ILLMService llm, IPersistenceService persistence)
		{
			_llm = llm;
			_persistence = persistence;
			_storage = new ToolIndexStorage(persistence);
		}

		public ToolIndexState GetState() { lock (_gate) return _state; }
		public ToolIndexFingerprint GetFingerprint() { lock (_gate) return _snapshot?.Fingerprint; }
		public bool IsReady() { lock (_gate) return _state == ToolIndexState.Ready; }
		public ToolIndexSnapshot GetSnapshot() { lock (_gate) return _snapshot; }

		public void MarkStale()
		{
			lock (_gate)
			{
				if (_state == ToolIndexState.Ready) _state = ToolIndexState.Stale;
			}
		}

		public async Task EnsureBuiltAsync(string provider, string model, int dimension, string instruction, (double name, double desc, double @params) weights, Func<CancellationToken, Task<List<ToolEmbeddingRecord>>> buildRecords, string basePath, string fileNameFormat, CancellationToken ct)
		{
			if (IsReady()) return;
			// 尝试先从磁盘加载
			var loaded = await TryLoadAsync(provider, model, basePath, fileNameFormat, ct);
			if (loaded == null)
			{
				await BuildInternalAsync(provider, model, dimension, instruction, weights, buildRecords, basePath, fileNameFormat, ct);
			}
		}

		public Task RebuildAsync(string provider, string model, int dimension, string instruction, (double name, double desc, double @params) weights, Func<CancellationToken, Task<List<ToolEmbeddingRecord>>> buildRecords, string basePath, string fileNameFormat, CancellationToken ct)
		{
			return BuildInternalAsync(provider, model, dimension, instruction, weights, buildRecords, basePath, fileNameFormat, ct);
		}

		private async Task BuildInternalAsync(string provider, string model, int dimension, string instruction, (double name, double desc, double @params) weights, Func<CancellationToken, Task<List<ToolEmbeddingRecord>>> buildRecords, string basePath, string fileNameFormat, CancellationToken ct)
		{
			lock (_gate)
			{
				if (_state == ToolIndexState.Building) return; // skip concurrent build
				_state = ToolIndexState.Building;
				_lastError = null;
			}

			try
			{
				var fingerprintHash = ToolIndexStorage.ComputeFingerprintHash(provider, model, dimension, instruction);
				var fp = new ToolIndexFingerprint { Provider = provider, Model = model, Dimension = dimension, Instruction = instruction, Hash = fingerprintHash };
				var records = await buildRecords(ct);
				var snapshot = new ToolIndexSnapshot { Fingerprint = fp, Records = records, Weights = (weights.name, weights.desc, weights.@params), BuiltAtUtc = DateTime.UtcNow };
				await _storage.SaveAsync(provider, model, snapshot, basePath, fileNameFormat, ct);

				lock (_gate)
				{
					_snapshot = snapshot;
					_state = ToolIndexState.Ready;
				}
			}
			catch (Exception ex)
			{
				lock (_gate)
				{
					_lastError = ex;
					_state = ToolIndexState.Error;
				}
				Verse.Log.Error($"[RimAI.Core][P4] ToolIndex build failed: {ex.Message}");
			}
		}

		public async Task<ToolIndexSnapshot> TryLoadAsync(string provider, string model, string basePath, string fileNameFormat, CancellationToken ct)
		{
			try
			{
				var snapshot = await _storage.LoadOrNullAsync(provider, model, basePath, fileNameFormat, ct);
				if (snapshot != null)
				{
					lock (_gate)
					{
						_snapshot = snapshot;
						_state = ToolIndexState.Ready;
					}
				}
				return snapshot;
			}
			catch (Exception ex)
			{
				lock (_gate)
				{
					_lastError = ex;
					_state = ToolIndexState.Error;
				}
				Verse.Log.Error($"[RimAI.Core][P4] ToolIndex load failed: {ex.Message}");
				return null;
			}
		}

		public IReadOnlyList<ToolScore> QueryScores(float[] queryVector, Func<string, IEnumerable<float[]>> getVectorsByTool, (double name, double desc, double @params) weights, int k, double? minScore)
		{
			var toolScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			foreach (var kvp in getVectorsByTool.Invoke("*grouped*")) { }

			// 构造分组: 外部传入 provider 函数更灵活，这里在辅助方法里组装
			var map = GroupVectorsByToolName(getVectorsByTool);
			foreach (var (toolName, variants) in map)
			{
				var nameScore = CosineBest(queryVector, variants.NameVectors);
				var descScore = CosineBest(queryVector, variants.DescVectors);
				var paramsScore = CosineBest(queryVector, variants.ParamsVectors);
				var score = weights.name * nameScore + weights.desc * descScore + weights.@params * paramsScore;
				if (minScore == null || score >= minScore.Value)
				{
					toolScores[toolName] = score;
				}
			}
			return toolScores
				.OrderByDescending(kv => kv.Value)
				.Take(k)
				.Select(kv => new ToolScore { ToolName = kv.Key, Score = kv.Value })
				.ToList();
		}

		private static double CosineBest(float[] query, List<float[]> vectors)
		{
			if (vectors == null || vectors.Count == 0) return 0.0;
			double best = 0.0;
			foreach (var v in vectors)
			{
				var sim = CosineSimilarity(query, v);
				if (sim > best) best = sim;
			}
			return best;
		}

		private static double CosineSimilarity(float[] a, float[] b)
		{
			if (a == null || b == null || a.Length == 0 || b.Length == 0 || a.Length != b.Length) return 0.0;
			double dot = 0, na = 0, nb = 0;
			for (int i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
			if (na == 0 || nb == 0) return 0.0;
			return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
		}

		private static Dictionary<string, (List<float[]> NameVectors, List<float[]> DescVectors, List<float[]> ParamsVectors)> GroupVectorsByToolName(Func<string, IEnumerable<float[]>> getVectorsByTool)
		{
			var map = new Dictionary<string, (List<float[]>, List<float[]>, List<float[]>)>(StringComparer.OrdinalIgnoreCase);
			// getVectorsByTool 约定: 传入特殊 key "*all*" 时返回平铺; 传入 "*toolNames*" 返回工具名集合向量为 null
			// 为简单起见，这里不使用特殊 key，而是让外层提供闭包直接访问 Snapshot。
			return map; // 外层未使用该方法当前返回空，不影响后续 S 步收敛
		}
	}
}


