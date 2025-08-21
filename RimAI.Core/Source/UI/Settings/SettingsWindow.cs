using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.Settings
{
	internal sealed class SettingsWindow
	{
		private readonly SettingsController _controller;
		private Vector2 _scrollPos = Vector2.zero;

		public SettingsWindow(SettingsController controller)
		{
			_controller = controller ?? throw new ArgumentNullException(nameof(controller));
		}

		public string GetCategory()
		{
			try { return "RimAI.Settings.Category".Translate().ToString(); } catch { return "RimAI.Core"; }
		}

		public void DoWindowContents(Rect inRect)
		{
			if (_controller == null) return;
			var viewRect = new Rect(0, 0, inRect.width - 16f, _controller.GetTotalHeight(inRect.width));
			Widgets.BeginScrollView(inRect, ref _scrollPos, viewRect, true);
			try
			{
				float curY = 0f;
				foreach (var section in _controller.GetSections())
				{
					var h = section.GetHeight(viewRect.width);
					var rect = new Rect(0, curY, viewRect.width, h);
					DrawSectionFrame(rect, section);
					curY += h + 12f;
				}
			}
			finally
			{
				Widgets.EndScrollView();
			}
		}

		private static void DrawSectionFrame(Rect rect, ISettingsSection section)
		{
			Widgets.DrawBoxSolid(rect, new Color(0.13f, 0.13f, 0.13f, 0.25f));
			var inner = rect.ContractedBy(8f);
			GUI.BeginGroup(inner);
			try
			{
				var title = section.GetTitle();
				var titleHeight = Text.CalcHeight(title, inner.width);
				Widgets.Label(new Rect(0, 0, inner.width, titleHeight), title);
				Widgets.DrawLineHorizontal(0, titleHeight + 4f, inner.width);
				var contentRect = new Rect(0, titleHeight + 10f, inner.width, inner.height - titleHeight - 10f);
				section.Draw(contentRect);
			}
			finally
			{
				GUI.EndGroup();
			}
		}
	}

	internal interface ISettingsSection
	{
		string Id { get; }
		string GetTitle();
		float GetHeight(float width);
		void Draw(Rect rect);
	}

	internal sealed class SettingsController
	{
		private readonly List<ISettingsSection> _sections = new List<ISettingsSection>();

		public void Register(ISettingsSection section)
		{
			if (section == null) return;
			_sections.Add(section);
		}

		public IEnumerable<ISettingsSection> GetSections() => _sections;

		public float GetTotalHeight(float width)
		{
			float sum = 0f;
			for (int i = 0; i < _sections.Count; i++)
			{
				sum += _sections[i].GetHeight(width) + 12f;
			}
			return Math.Max(sum, 10f);
		}
	}
}


