using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Persona;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class FixedPromptEditor : Window
	{
		private readonly IPersonaService _persona;
		private readonly string _entityId;
		private string _text;
		private Vector2 _scroll = Vector2.zero;

		public override Vector2 InitialSize => new Vector2(640f, 420f);

		public FixedPromptEditor(string entityId, IPersonaService persona)
		{
			_entityId = entityId;
			_persona = persona;
			doCloseX = true;
			draggable = true;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = false;
			closeOnAccept = false;
			closeOnCancel = false;
			_text = _persona?.Get(_entityId)?.FixedPrompts?.Text ?? string.Empty;
		}

		public override void DoWindowContents(Rect inRect)
		{
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "RimAI.ChatUI.Tabs.FixedPrompt".Translate());
			Text.Font = GameFont.Small;
			var contentRect = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 80f);
			var buttonsRect = new Rect(inRect.x, inRect.yMax - 40f, inRect.width, 36f);

			// 富文本框（多行）
			var viewRect = new Rect(0f, 0f, contentRect.width - 16f, Mathf.Max(contentRect.height, Text.CalcHeight(_text ?? string.Empty, contentRect.width - 16f) + 12f));
			Widgets.BeginScrollView(contentRect, ref _scroll, viewRect);
			_text = Widgets.TextArea(viewRect, _text ?? string.Empty);
			Widgets.EndScrollView();

			// 按钮行：保存、清空、取消
			var bw = 110f; var sp = 8f;
			var rSave = new Rect(buttonsRect.x, buttonsRect.y, bw, buttonsRect.height);
			var rClear = new Rect(rSave.xMax + sp, buttonsRect.y, bw, buttonsRect.height);
			var rCancel = new Rect(rClear.xMax + sp, buttonsRect.y, bw, buttonsRect.height);

			if (Widgets.ButtonText(rSave, "RimAI.Common.Save".Translate()))
			{
				try { _persona?.Upsert(_entityId, e => e.SetFixedPrompt(_text ?? string.Empty)); }
				catch { }
				Close();
			}
			if (Widgets.ButtonText(rClear, "RimAI.Common.Clear".Translate()))
			{
				_text = string.Empty;
			}
			if (Widgets.ButtonText(rCancel, "RimAI.Common.Cancel".Translate()))
			{
				Close();
			}
		}
	}
}


