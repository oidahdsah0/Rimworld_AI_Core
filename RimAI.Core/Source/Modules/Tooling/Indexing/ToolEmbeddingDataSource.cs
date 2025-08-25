using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;

namespace RimAI.Core.Source.Modules.Tooling.Indexing
{
	internal sealed class ToolEmbeddingDataSource
	{
		private readonly ILLMService _llm;
		private readonly string _provider;
		private readonly string _model;
		private readonly string _instruction;

		public ToolEmbeddingDataSource(ILLMService llm, string provider, string model, string instruction)
		{
			_llm = llm;
			_provider = provider ?? "auto";
			_model = model ?? "auto";
			_instruction = instruction ?? string.Empty;
		}

		public async Task<List<ToolEmbeddingRecord>> BuildRecordsAsync(IEnumerable<IRimAITool> tools, CancellationToken ct)
		{
			var records = new List<ToolEmbeddingRecord>();
			if (tools == null) return records;
			try
			{
				var toolNames = tools.Select(x => x.Name ?? string.Empty).ToList();
				Verse.Log.Message($"[RimAI.Core][P4] tools=[{string.Join(", ", toolNames)}] count={toolNames.Count}");
			}
			catch { }

			foreach (var t in tools)
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
}
