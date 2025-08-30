using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Stage;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.History;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class TestTab
	{
		private static string _status;
		private static CancellationTokenSource _cts;

		public static void Draw(Rect inRect, ChatController controller, IWorldDataService world, RimAI.Core.Source.Infrastructure.ServiceContainer container)
		{
			// Header
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "RimAI.ChatUI.Tabs.Test".Translate());
			Text.Font = GameFont.Small;
			var y = inRect.y + 36f;

			// First button: Trigger Group Chat (Dev-only)
			bool dev = Prefs.DevMode;
			GUI.enabled = dev;
			var btnRect = new Rect(inRect.x, y, 160f, 28f);
			if (Widgets.ButtonText(btnRect, "RimAI.ChatUI.Test.TriggerGroupChat".Translate()))
			{
				_status = "RimAI.ChatUI.Test.Working".Translate();
				_cts?.Cancel();
				_cts = new CancellationTokenSource();
				var token = _cts.Token;
				_ = Task.Run(async () =>
				{
					try
					{
						var stage = container.Resolve<IStageService>();
						var ids = await world.GetAllColonistLoadIdsAsync(token).ConfigureAwait(false);
						var list = ids?.ToList() ?? new List<int>();
						if (list.Count < 2) { _status = "RimAI.ChatUI.Test.GroupChat.NotEnough".Translate(); return; }
						// choose 2..4 participants
						var rnd = new System.Random(unchecked(Environment.TickCount ^ list.Count));
						int count = Math.Max(2, Math.Min(5, 2 + rnd.Next(0, 4)));
						var selected = list.OrderBy(_ => rnd.Next()).Take(count).ToList();
						var participants = selected.Select(x => $"pawn:{x}").ToList();
						// convKey: participants + current player
						var pids = new List<string>(participants);
						try
						{
							var player = controller?.State?.ParticipantIds?.FirstOrDefault(x => x != null && x.StartsWith("player:"));
							if (!string.IsNullOrEmpty(player)) { pids.Add(player); }
						}
						catch { }
						pids.Sort(StringComparer.Ordinal);
						var convKey = string.Join("|", pids);
						var intent = new StageIntent { ActName = "GroupChat", ParticipantIds = participants, Origin = "ChatUI-Test", ScenarioText = string.Empty, Locale = "zh-Hans", Seed = DateTime.UtcNow.Ticks.ToString() };
						var decision = await stage.SubmitIntentAsync(intent, token).ConfigureAwait(false);
						if (decision == null || decision.Ticket == null) { _status = "RimAI.ChatUI.Test.GroupChat.Failed".Translate(); return; }
						_status = "RimAI.ChatUI.Test.GroupChat.Done".Translate(participants.Count);
					}
					catch (Exception ex)
					{
						try { _status = "RimAI.ChatUI.Test.GroupChat.Error".Translate(ex.GetType().Name); } catch { _status = "Error"; }
					}
				}, token);
			}
			GUI.enabled = true;

			// Second button: Dump all history convKeys (always enabled)
			var btnRect2 = new Rect(inRect.x + 170f, y, 220f, 28f);
			if (Widgets.ButtonText(btnRect2, "输出历史服务所有键"))
			{
				try
				{
					var history = container.Resolve<IHistoryService>();
					var keys = history?.GetAllConvKeys() ?? new List<string>();
					var preview = string.Join("\n", keys);
					_status = $"历史键（{keys.Count}）:\n{preview}";
					try { GUIUtility.systemCopyBuffer = _status; } catch { }
				}
				catch (Exception ex)
				{
					_status = $"获取失败: {ex.GetType().Name}";
				}
			}

			// Status line
			if (!string.IsNullOrEmpty(_status))
			{
				Widgets.Label(new Rect(inRect.x, Mathf.Max(btnRect.yMax, btnRect2.yMax) + 6f, inRect.width, 400f), _status);
			}
		}
	}
}


