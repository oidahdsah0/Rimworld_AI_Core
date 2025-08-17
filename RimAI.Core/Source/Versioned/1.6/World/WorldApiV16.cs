using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using System.Reflection;
using UnityEngine;

namespace RimAI.Core.Source.Versioned._1_6.World
{
	internal static class WorldApiV16
	{
		public static string GetBackstoryTitle(Pawn pawn, bool childhood)
		{
			try
			{
				if (pawn?.story == null) return null;
				BackstoryDef bs = childhood ? pawn.story.Childhood : pawn.story.Adulthood;
				if (bs == null) return null;
				// 1) 反射优先调用 TitleShortCapFor/TitleCapFor（兼容不同版本与参数签名）
				try
				{
					var t = bs.GetType();
					foreach (var name in new[] { "TitleShortCapFor", "TitleCapFor" })
					{
						var mi = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
						if (mi == null) continue;
						var pars = mi.GetParameters();
						if (pars.Length == 1)
						{
							object arg = null;
							if (pars[0].ParameterType == typeof(Pawn)) arg = pawn;
							else if (pars[0].ParameterType == typeof(Gender)) arg = pawn?.gender ?? Gender.None;
							if (arg != null)
							{
								var s = mi.Invoke(bs, new object[] { arg }) as string;
								if (!string.IsNullOrWhiteSpace(s)) return s;
							}
						}
					}
				}
				catch { }
				// 2) 回退到字段/属性（带性别优先）
				try
				{
					string TryGet(string[] propOrFieldNames)
					{
						var tt = bs.GetType();
						foreach (var n in propOrFieldNames)
						{
							var pi = tt.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
							if (pi != null)
							{
								var v = pi.GetValue(bs) as string;
								if (!string.IsNullOrWhiteSpace(v)) return v;
							}
							var fi = tt.GetField(n, BindingFlags.Public | BindingFlags.Instance);
							if (fi != null)
							{
								var v = fi.GetValue(bs) as string;
								if (!string.IsNullOrWhiteSpace(v)) return v;
							}
						}
						return null;
					}
					// 性别敏感的短标题
					if (pawn?.gender == Gender.Female)
					{
						var s = TryGet(new[] { "titleShortFemale", "TitleShortFemale" }) ?? TryGet(new[] { "titleFemale", "TitleFemale" });
						if (!string.IsNullOrWhiteSpace(s)) return s.CapitalizeFirst();
					}
					// 常规短标题/标题
					{
						var s = TryGet(new[] { "titleShortCap", "TitleShortCap", "titleShort", "TitleShort" })
							?? TryGet(new[] { "titleCap", "TitleCap", "title", "Title", "label", "Label" });
						if (!string.IsNullOrWhiteSpace(s)) return s.CapitalizeFirst();
					}
				}
				catch { }
				// 3) 兜底：defName
				return bs.defName;
			}
			catch { return null; }
		}

		public static string GetPawnTitle(Pawn pawn)
		{
			try
			{
				if (pawn == null) return null;
				// Royalty（若存在）
				try
				{
					var royalty = pawn.GetType().GetProperty("royalty", BindingFlags.Public | BindingFlags.Instance)?.GetValue(pawn);
					if (royalty != null)
					{
						var most = royalty.GetType().GetProperty("MostSeniorTitle", BindingFlags.Public | BindingFlags.Instance)?.GetValue(royalty);
						var label = most?.GetType().GetProperty("label", BindingFlags.Public | BindingFlags.Instance)?.GetValue(most) as string;
						if (!string.IsNullOrWhiteSpace(label)) return label;
					}
				}
				catch { }
				// Story 自带 TitleShortCap
				try
				{
					var story = pawn.story;
					if (story != null)
					{
						var pi = story.GetType().GetProperty("TitleShortCap", BindingFlags.Public | BindingFlags.Instance);
						if (pi != null)
						{
							var s = pi.GetValue(story) as string;
							if (!string.IsNullOrWhiteSpace(s)) return s;
						}
					}
				}
				catch { }
				// 回退：成年/童年 Backstory 标题
				var t = GetBackstoryTitle(pawn, false) ?? GetBackstoryTitle(pawn, true);
				return t;
			}
			catch { return null; }
		}

		public static string GetCombinedDisabledWorkTagsCsv(Pawn pawn)
		{
			try
			{
				if (pawn == null) return null;
				WorkTags disabled = WorkTags.None;
				var story = pawn.story;
				if (story != null)
				{
					var child = story.Childhood; if (child != null) disabled |= child.workDisables;
					var adult = story.Adulthood; if (adult != null) disabled |= adult.workDisables;
					var traits = story.traits?.allTraits ?? new List<Trait>();
					foreach (var tr in traits)
					{
						try
						{
							var td = tr?.CurrentData;
							if (td == null) continue;
							var t = td.GetType();
							// workDisables / disabledWorkTags
							foreach (var pname in new[] { "workDisables", "disabledWorkTags" })
							{
								var pi = t.GetProperty(pname, BindingFlags.Public | BindingFlags.Instance);
								if (pi != null)
								{
									var val = pi.GetValue(td);
									if (val is WorkTags wt) { disabled |= wt; break; }
								}
							}
						}
						catch { }
					}
				}
				var names = DefDatabase<WorkTypeDef>.AllDefs
					.Where(wt => wt != null && (wt.workTags & disabled) != WorkTags.None)
					.Select(wt => wt.labelShort ?? wt.label ?? wt.defName)
					.Where(s => !string.IsNullOrWhiteSpace(s))
					.ToList();
				return names.Count == 0 ? null : string.Join(", ", names);
			}
			catch { return null; }
		}

		public static IReadOnlyList<RimAI.Core.Source.Modules.World.SocialEventItem> GetRecentSocialEvents(Pawn pawn, int maxCount)
		{
			var list = new List<RimAI.Core.Source.Modules.World.SocialEventItem>();
			try
			{
				var logs = Find.PlayLog?.AllEntries ?? new List<LogEntry>();
				for (int i = logs.Count - 1; i >= 0 && list.Count < Math.Max(0, maxCount); i--)
				{
					var entry = logs[i];
					if (entry is PlayLogEntry_Interaction inter && inter.Concerns(pawn))
					{
						Pawn other = null;
						try
						{
							other = inter.GetConcerns()?.OfType<Pawn>()?.FirstOrDefault(x => x != pawn);
						}
						catch { }
						var item = new RimAI.Core.Source.Modules.World.SocialEventItem
						{
							TimestampUtc = DateTime.UtcNow,
							WithName = other?.Name?.ToStringShort ?? other?.LabelCap ?? "Pawn",
							WithEntityId = other != null ? ($"pawn:{other.thingIDNumber}") : null,
							InteractionKind = inter.def?.label ?? inter.def?.defName ?? "Social",
							Outcome = null,
							GameTime = GetCurrentGameTime()
						};
						list.Add(item);
					}
				}
			}
			catch { }
			return list;
		}

		private static string GetCurrentGameTime()
		{
			try
			{
				var abs = Find.TickManager?.TicksAbs ?? 0;
				int tile = Find.CurrentMap?.Tile ?? 0;
				var longLat = Find.WorldGrid?.LongLatOf(tile) ?? Vector2.zero;
				return GenDate.DateFullStringAt(abs, longLat);
			}
			catch { return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"); }
		}
	}
}


