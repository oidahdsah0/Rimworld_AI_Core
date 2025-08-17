using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.Persona.Biography;
using RimAI.Core.Source.Modules.Persona.FixedPrompt;
using RimAI.Core.Source.Modules.Persona.Ideology;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P7_PersonaPanel
	{
		private static string _entityId = string.Empty;
		private static string _jobName = string.Empty;
		private static string _jobDesc = string.Empty;
		private static string _fixedText = string.Empty;
		private static string _ideologyWorld = string.Empty;
		private static string _ideologyValues = string.Empty;
		private static string _ideologyCode = string.Empty;
		private static string _ideologyTraits = string.Empty;
		private static Vector2 _bioScroll = Vector2.zero;
		private static readonly List<BiographyItem> _bioDraft = new List<BiographyItem>();
		private static CancellationTokenSource _cts;

		public static void Draw(Listing_Standard listing, IPersonaService persona, RimAI.Core.Source.Modules.Persona.Job.IPersonaJobService job, IBiographyService bio, IIdeologyService ideology, IFixedPromptService fixedSvc)
		{
			Text.Font = GameFont.Medium;
			listing.Label(Keys.P7 + " Persona");
			Text.Font = GameFont.Small;
			listing.GapLine();

			if (listing.ButtonText("Use Selected Pawn/Thing as EntityId"))
			{
				_entityId = ResolveSelectedEntityId();
				Log.Message(Keys.P7 + $" entityId={_entityId}");
			}
			listing.Label($"EntityId: {_entityId}");

			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("Job");
			Text.Font = GameFont.Small;
			_jobName = listing.TextEntryLabeled("Name:", _jobName);
			_jobDesc = listing.TextEntryLabeled("Description:", _jobDesc);
			if (listing.ButtonText("Save Job"))
			{
				persona.Upsert(_entityId, e => e.SetJob(_jobName, _jobDesc));
				Log.Message(Keys.P7 + " Saved Job");
			}
			// 生成描述按钮已移除

			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("Ideology");
			Text.Font = GameFont.Small;
			_ideologyWorld = listing.TextEntryLabeled("Worldview:", _ideologyWorld);
			_ideologyValues = listing.TextEntryLabeled("Values:", _ideologyValues);
			_ideologyCode = listing.TextEntryLabeled("Code:", _ideologyCode);
			_ideologyTraits = listing.TextEntryLabeled("Traits:", _ideologyTraits);
			if (listing.ButtonText("Save Ideology"))
			{
				persona.Upsert(_entityId, e => e.SetIdeology(_ideologyWorld, _ideologyValues, _ideologyCode, _ideologyTraits));
				Log.Message(Keys.P7 + " Saved Ideology");
			}

			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("Fixed Prompt");
			Text.Font = GameFont.Small;
			_fixedText = Widgets.TextArea(listing.GetRect(90f), _fixedText);
			if (listing.ButtonText("Save Fixed Prompt"))
			{
				persona.Upsert(_entityId, e => e.SetFixedPrompt(_fixedText));
				Log.Message(Keys.P7 + " Saved Fixed Prompt");
			}

			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("Biography (Draft -> Adopt)");
			Text.Font = GameFont.Small;
			if (listing.ButtonText("Generate Draft (3-5 items)"))
			{
				CancelIfRunning();
				_cts = new CancellationTokenSource();
				_ = Task.Run(async () =>
				{
					try
					{
						var items = await bio.GenerateDraftAsync(_entityId, _cts.Token);
						_bioDraft.Clear();
						_bioDraft.AddRange(items);
						Log.Message(Keys.P7 + $" bio.draft.count={items.Count}");
					}
					catch (OperationCanceledException) { Log.Message(Keys.P7 + " bio.gen.cancelled"); }
					catch (Exception ex) { Log.Warning(Keys.P7 + " bio.gen.failed: " + ex.Message); }
				});
			}
			var box = listing.GetRect(120f);
			Widgets.DrawBoxSolid(box, new Color(0f, 0f, 0f, 0.05f));
			var view = new Rect(0f, 0f, box.width - 16f, Math.Max(120f, _bioDraft.Count * 22f + 4f));
			Widgets.BeginScrollView(box, ref _bioScroll, view);
			var l = new Listing_Standard();
			l.Begin(view);
			foreach (var item in _bioDraft.ToList())
			{
				l.Label("- " + item.Text);
				if (l.ButtonText("Adopt"))
				{
					persona.Upsert(_entityId, e => e.AddOrUpdateBiography(item.Id, item.Text, item.Source));
				}
			}
			l.End();
			Widgets.EndScrollView();

			listing.GapLine();
			if (listing.ButtonText("Preview Persona Block"))
			{
				var opts = new PersonaComposeOptions();
				var text = persona.ComposePersonaBlock(_entityId, opts, out var audit);
				Log.Message(Keys.P7 + $" block.preview len={audit.TotalChars}, segs={audit.Segments.Count}");
				Messages.Message("Persona Block length=" + audit.TotalChars, MessageTypeDefOf.PositiveEvent, false);
			}
		}

		private static string ResolveSelectedEntityId()
		{
			var sel = Find.Selector?.SingleSelectedThing;
			if (sel is Pawn p)
			{
				return "pawn:" + p.thingIDNumber;
			}
			if (sel is Thing t)
			{
				return "thing:" + t.thingIDNumber;
			}
			return string.Empty;
		}

		private static void CancelIfRunning()
		{
			try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
			_cts = null;
		}
	}
}


