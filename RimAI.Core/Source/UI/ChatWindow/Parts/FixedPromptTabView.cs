using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Persona;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class FixedPromptTabView
	{
		public void Draw(Rect inRect, Pawn pawn, IPersonaService persona, System.Action backToChat)
		{
			string entityId = pawn != null && pawn.thingIDNumber != 0 ? ($"pawn:{pawn.thingIDNumber}") : null;
			if (string.IsNullOrEmpty(entityId))
			{
				Widgets.Label(inRect, "RimAI.ChatUI.Common.NoPawnSelected".Translate());
				return;
			}
			Find.WindowStack.Add(new FixedPromptEditor(entityId, persona));
			backToChat?.Invoke();
		}
	}
}


