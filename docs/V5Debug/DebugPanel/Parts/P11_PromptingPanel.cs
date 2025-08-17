using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P11_PromptingPanel
	{
		public static void Draw(Listing_Standard listing, IPromptService prompting)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P11] Prompting");
			Text.Font = GameFont.Small;
			listing.GapLine();

			var pawn = Find.Selector?.SingleSelectedThing as Pawn;
			var pawnInfo = pawn != null ? $"pawn:{pawn.thingIDNumber} {pawn?.LabelCap?.ToString()}" : "(未选择)";
			listing.Label($"当前选中小人：{pawnInfo}");

			if (listing.ButtonText("Build ChatUI Prompt (Smalltalk)"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var participantIds = new System.Collections.Generic.List<string>();
						if (pawn != null && pawn.thingIDNumber != 0) participantIds.Add($"pawn:{pawn.thingIDNumber}");
						participantIds.Add("player:debug");
						participantIds.Sort(StringComparer.Ordinal);
						var convKey = string.Join("|", participantIds);

						var req = new PromptBuildRequest
						{
							Scope = PromptScope.ChatUI,
							ConvKey = convKey,
							ParticipantIds = participantIds,
							PawnLoadId = pawn != null ? (int?)pawn.thingIDNumber : null,
							IsCommand = false,
							Locale = null,
							UserInput = "这是一个调试输入"
						};

						var sw = System.Diagnostics.Stopwatch.StartNew();
						var result = await prompting.BuildAsync(req, CancellationToken.None).ConfigureAwait(false);
						sw.Stop();

						var sb = new System.Text.StringBuilder();
						sb.AppendLine("[RimAI.Core][P11] Prompt Result (Smalltalk)");
						sb.AppendLine($"conv={convKey} elapsed={sw.ElapsedMilliseconds} ms");
						sb.AppendLine("--- SystemPrompt ---");
						var sys = result?.SystemPrompt ?? string.Empty;
						if (!string.IsNullOrEmpty(sys))
						{
							var lines = sys.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
							var special = new System.Collections.Generic.List<string>();
							var filtered = new System.Text.StringBuilder();
							sb.AppendLine(sys);
							if (special.Count > 0)
							{
								sb.AppendLine("--- Special Info ---");
								foreach (var sline in special) sb.AppendLine(sline);
								sb.AppendLine();
							}
						}
						else
						{
							sb.AppendLine(string.Empty);
						}
						sb.AppendLine("--- Activities ---");
						if (result?.ContextBlocks != null)
						{
							foreach (var b in result.ContextBlocks)
							{
								var title = b?.Title;
								var text = b?.Text;
								bool textIsSingleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
								if (!string.IsNullOrWhiteSpace(title) && textIsSingleLine)
								{
									sb.AppendLine(title + " " + text);
								}
								else
								{
									if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
									if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
								}
								sb.AppendLine();
							}
						}
						sb.AppendLine("--- UserPrefixedInput ---");
						sb.AppendLine(result?.UserPrefixedInput ?? string.Empty);
						Log.Message(sb.ToString());
					}
					catch (OperationCanceledException)
					{
						Log.Warning("[RimAI.Core][P11] Prompt 构建被取消");
					}
					catch (Exception ex)
					{
						Log.Warning($"[RimAI.Core][P11] Build ChatUI Prompt 失败: {ex.Message}");
					}
				});
			}

			if (listing.ButtonText("Build ChatUI Prompt (Command)"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var participantIds = new System.Collections.Generic.List<string>();
						if (pawn != null && pawn.thingIDNumber != 0) participantIds.Add($"pawn:{pawn.thingIDNumber}");
						participantIds.Add("player:debug");
						participantIds.Sort(StringComparer.Ordinal);
						var convKey = string.Join("|", participantIds);

						var req = new PromptBuildRequest
						{
							Scope = PromptScope.ChatUI,
							ConvKey = convKey,
							ParticipantIds = participantIds,
							PawnLoadId = pawn != null ? (int?)pawn.thingIDNumber : null,
							IsCommand = true,
							Locale = null,
							UserInput = "（命令）请汇报殖民地状态"
						};

						var sw = System.Diagnostics.Stopwatch.StartNew();
						var result = await prompting.BuildAsync(req, CancellationToken.None).ConfigureAwait(false);
						sw.Stop();

						var sb = new System.Text.StringBuilder();
						sb.AppendLine("[RimAI.Core][P11] Prompt Result (Command)");
						sb.AppendLine($"conv={convKey} elapsed={sw.ElapsedMilliseconds} ms");
						sb.AppendLine("--- SystemPrompt ---");
						var sys = result?.SystemPrompt ?? string.Empty;
						if (!string.IsNullOrEmpty(sys))
						{
							var lines = sys.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
							var special = new System.Collections.Generic.List<string>();
							var filtered = new System.Text.StringBuilder();
							sb.AppendLine(sys);
							if (special.Count > 0)
							{
								sb.AppendLine("--- Special Info ---");
								foreach (var sline in special) sb.AppendLine(sline);
								sb.AppendLine();
							}
						}
						else
						{
							sb.AppendLine(string.Empty);
						}
						sb.AppendLine("--- Activities ---");
						if (result?.ContextBlocks != null)
						{
							foreach (var b in result.ContextBlocks)
							{
								var title = b?.Title;
								var text = b?.Text;
								bool textIsSingleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
								if (!string.IsNullOrWhiteSpace(title) && textIsSingleLine)
								{
									sb.AppendLine(title + " " + text);
								}
								else
								{
									if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
									if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
								}
								sb.AppendLine();
							}
						}
						sb.AppendLine("--- UserPrefixedInput ---");
						sb.AppendLine(result?.UserPrefixedInput ?? string.Empty);
						Log.Message(sb.ToString());
					}
					catch (OperationCanceledException)
					{
						Log.Warning("[RimAI.Core][P11] Prompt 构建被取消");
					}
					catch (Exception ex)
					{
						Log.Warning($"[RimAI.Core][P11] Build ChatUI Prompt 失败: {ex.Message}");
					}
				});
			}
		}
	}
}


