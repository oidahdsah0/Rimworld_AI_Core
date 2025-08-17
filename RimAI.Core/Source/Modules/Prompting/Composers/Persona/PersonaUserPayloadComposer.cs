using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting.Composers.Persona
{
	internal sealed class PersonaUserPayloadComposer : IPromptComposer, IProvidesUserPayload
	{
		private readonly PromptScope _scope;
		public PersonaUserPayloadComposer(PromptScope scope)
		{
			_scope = scope;
		}

		public PromptScope Scope => _scope;
		public int Order => 9999; // 放到最后，仅提供User
		public string Id => _scope == PromptScope.PersonaBiography ? "persona_biography_user" : "persona_ideology_user";

		public Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			// 不生成 SystemLines/Blocks
			return Task.FromResult(new ComposerOutput { SystemLines = System.Array.Empty<string>(), ContextBlocks = System.Array.Empty<ContextBlock>() });
		}

		PromptScope IProvidesUserPayload.Scope => _scope;

		public Task<string> BuildUserPayloadAsync(PromptBuildContext ctx, CancellationToken ct)
		{
			var sb = new StringBuilder();

			// 复用 ChatUI 各 composer 的输出行，拼为单段文本
			var tmpComposers = new List<IPromptComposer>();
			// 通用基础信息
			tmpComposers.Add(new ChatUI.PawnIdentityComposer());
			tmpComposers.Add(new ChatUI.PawnBackstoryComposer());
			tmpComposers.Add(new ChatUI.PawnTraitsComposer());
			tmpComposers.Add(new ChatUI.PawnSkillsComposer());
			tmpComposers.Add(new ChatUI.PersonaJobComposer());
			// 需求：
			// - 在 PersonaBiography 的 user 段放入“世界观四段”（排除传记）
			// - 在 PersonaIdeology 的 user 段放入“个人传记”（排除世界观四段）
			if (_scope == PromptScope.PersonaBiography)
			{
				// 仅加入世界观四段
				tmpComposers.Add(new ChatUI.PersonaIdeologyComposer());
			}
			else if (_scope == PromptScope.PersonaIdeology)
			{
				// 仅加入个人传记
				tmpComposers.Add(new ChatUI.PersonaBiographyComposer());
			}
			// 其他上下文
			tmpComposers.Add(new ChatUI.PawnSocialRelationsComposer());
			tmpComposers.Add(new ChatUI.HistoryRecapComposer());
			tmpComposers.Add(new ChatUI.PawnSocialHistoryComposer());
			tmpComposers.Add(new ChatUI.ColonyStatusComposer());
			tmpComposers.Add(new Common.GameLogComposer(_scope, 30));
			tmpComposers.Add(new ChatUI.EnvBeautyComposer());
			tmpComposers.Add(new ChatUI.EnvTerrainComposer());
			tmpComposers.Add(new ChatUI.CurrentJobComposer());
			tmpComposers.Add(new ChatUI.ApparelComposer());
			tmpComposers.Add(new ChatUI.NeedsComposer());
			tmpComposers.Add(new ChatUI.NeedStatesComposer());

			foreach (var c in tmpComposers.OrderBy(c => c.Order))
			{
				if (ct.IsCancellationRequested) break;
				var outp = c.ComposeAsync(ctx, ct).GetAwaiter().GetResult();
				// 这些 ChatUI composer 都是把可读行放在 SystemLines 中
				if (outp?.SystemLines != null)
				{
					foreach (var line in outp.SystemLines)
					{
						if (!string.IsNullOrWhiteSpace(line))
						{
							if (sb.Length > 0) sb.AppendLine();
							sb.Append(line);
						}
					}
				}
				// 同时合并 ContextBlocks（Activities 等），进入 user 段
				if (outp?.ContextBlocks != null)
				{
					foreach (var b in outp.ContextBlocks)
					{
						var title = b?.Title ?? string.Empty;
						var text = b?.Text ?? string.Empty;
						if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(text)) continue;
						bool singleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
						if (sb.Length > 0) sb.AppendLine();
						if (!string.IsNullOrWhiteSpace(title) && singleLine)
						{
							sb.Append(title + " " + text);
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
							sb.Append(text ?? string.Empty);
						}
					}
				}
			}

			return Task.FromResult(sb.ToString());
		}
	}
}


