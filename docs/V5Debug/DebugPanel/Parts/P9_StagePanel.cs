using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.History;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P9_StagePanel
	{
		private static string _actName = "GroupChat";
		private static string _participants = "pawn:1,pawn:2";
		private static string _scenario = "示例场景";
		private static string _locale = "zh-Hans";

		public static void Draw(Listing_Standard listing, IStageService stage, IHistoryService history)
		{
			Text.Font = GameFont.Medium;
			listing.Label(KeysP9.Title);
			Text.Font = GameFont.Small;
			listing.GapLine();

			listing.Label("Registered Acts:");
			listing.Label(string.Join(", ", stage.ListActs()));
			listing.Label("Registered Triggers:");
			listing.Label(string.Join(", ", stage.ListTriggers()));

			// Running tickets
			var running = stage.QueryRunning();
			listing.Label($"Running: {running.Count}");
			foreach (var r in running.Take(10))
			{
				listing.Label($"- {r.TicketId} {r.ConvKey} exp={r.LeaseExpiresUtc:HH:mm:ss}");
			}

			listing.GapLine();
			listing.Label("Stage Log (agent:stage) preview:");
			try
			{
				var th = history.GetThreadAsync("agent:stage", 1, 50).GetAwaiter().GetResult();
				var text = string.Join("\n", (th?.Entries ?? System.Array.Empty<RimAI.Core.Source.Modules.History.Models.HistoryEntry>()).Select(e => $"{e.Timestamp:HH:mm:ss} {e.Content}"));
				var outRect = listing.GetRect(160f);
				Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.06f));
				var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(160f, Text.CalcHeight(text, outRect.width - 16f) + 8f));
				var scroll = Vector2.zero;
				Widgets.BeginScrollView(outRect, ref scroll, viewRect);
				Widgets.Label(viewRect, text);
				Widgets.EndScrollView();
			}
			catch { }

			listing.GapLine();
			listing.Label("Start Act (Debug, bypass arbitration):");
			_actName = Widgets.TextField(listing.GetRect(24f), _actName ?? string.Empty);
			_participants = Widgets.TextField(listing.GetRect(24f), _participants ?? string.Empty);
			_scenario = Widgets.TextField(listing.GetRect(24f), _scenario ?? string.Empty);
			_locale = Widgets.TextField(listing.GetRect(24f), _locale ?? string.Empty);
			if (listing.ButtonText("Start"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var parts = (_participants ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
						var req = new StageExecutionRequest
						{
							Ticket = new StageTicket { Id = Guid.NewGuid().ToString("N"), ConvKey = string.Join("|", parts.OrderBy(x => x)), ParticipantIds = parts, ExpiresAtUtc = DateTime.UtcNow.AddSeconds(10) },
							ScenarioText = _scenario,
							Locale = _locale,
							Origin = "Debug"
						};
						var r = await stage.StartAsync(_actName, req, CancellationToken.None);
						Log.Message(KeysP9.Title + " Start result: " + r.Reason + ", completed=" + r.Completed + ", len=" + (r.FinalText?.Length ?? 0));
					}
					catch (Exception ex) { Log.Error(KeysP9.Title + " Start failed: " + ex.Message); }
				});
			}

			listing.GapLine();
			if (listing.ButtonText("Run Active Triggers Once"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						await stage.RunActiveTriggersOnceAsync(CancellationToken.None);
						Log.Message(KeysP9.Title + " Active triggers run once");
					}
					catch (Exception ex) { Log.Error(KeysP9.Title + " Triggers run failed: " + ex.Message); }
				});
			}

			if (listing.ButtonText("Clear Idempotency Cache"))
			{
				try { stage.ClearIdempotencyCache(); Log.Message(KeysP9.Title + " Idempotency cache cleared"); }
				catch (Exception ex) { Log.Error(KeysP9.Title + " Clear cache failed: " + ex.Message); }
			}
		}
	}

	internal static class KeysP9
	{
		public const string Title = "[RimAI.Core][P9.Stage]";
	}
}


