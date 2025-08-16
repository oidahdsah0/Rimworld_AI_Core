using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.ChatUI
{
	internal sealed class EnvironmentMatrixComposer : IPromptComposer
	{
		public PromptScope Scope => PromptScope.ChatUI;
		public int Order => 95; // 靠后，位于社交关系之后、历史之前
		public string Id => "env_matrix";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var sysLines = new List<string>();
			var env = ctx?.EnvMatrix;
			if (env != null)
			{
				// 1) 周围美化度（独立条目）
				var titleBeauty = ctx?.L?.Invoke("prompt.section.env_beauty", "[环境-周围美化度]") ?? "[环境-周围美化度]";
				var avgStr = (env.BeautyAverage).ToString("F1");
				sysLines.Add(titleBeauty + avgStr);

				// 2) 周围地貌（独立条目）：形如 [环境-周围地貌]Sand-12;Soil-8;
				var titleTerrain = ctx?.L?.Invoke("prompt.section.env_terrain", "[环境-周围地貌]") ?? "[环境-周围地貌]";
				var listStr = string.Empty;
				if (env.TerrainCounts != null && env.TerrainCounts.Count > 0)
				{
					var top = System.Math.Min(6, env.TerrainCounts.Count);
					var parts = new System.Collections.Generic.List<string>(top);
					for (int i = 0; i < top; i++)
					{
						var item = env.TerrainCounts[i];
						if (!string.IsNullOrWhiteSpace(item?.Terrain))
							parts.Add(item.Terrain + "-" + item.Count.ToString());
					}
					listStr = string.Join(";", parts);
					if (!string.IsNullOrEmpty(listStr)) listStr += ";";
				}
				sysLines.Add(titleTerrain + listStr);
			}
			return Task.FromResult(new ComposerOutput { SystemLines = sysLines, ContextBlocks = System.Array.Empty<ContextBlock>() });
		}
	}
}


