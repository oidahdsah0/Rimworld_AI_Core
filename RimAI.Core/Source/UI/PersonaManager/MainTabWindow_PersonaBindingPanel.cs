using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.Persona;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.PersonaManager
{
    public class MainTabWindow_PersonaBindingPanel : Window
    {
        private readonly IPersonaService _personaService;
        private readonly IPersonaBindingService _bindingService;

        private Vector2 _scroll = Vector2.zero;
        private string _pawnIdInput = string.Empty;
        private int _personaIndex = -1;

        public MainTabWindow_PersonaBindingPanel()
        {
            _personaService = CoreServices.Locator.Get<IPersonaService>();
            _bindingService = CoreServices.Locator.Get<IPersonaBindingService>();
            forcePause = false; doCloseX = true; draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(720f, 520f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Anchor = TextAnchor.UpperLeft;

            // Bind section
            Widgets.Label(new Rect(inRect.x, inRect.y, 80f, 24f), "PawnId");
            _pawnIdInput = Widgets.TextField(new Rect(inRect.x + 80f, inRect.y, 260f, 24f), _pawnIdInput);

            var personas = _personaService.GetAll().OrderBy(p => p.Name).ToList();
            var names = personas.Select(p => p.Name).ToList();
            if (_personaIndex < 0 && names.Count > 0) _personaIndex = 0;
            Widgets.Label(new Rect(inRect.x + 360f, inRect.y, 80f, 24f), "Persona");
            if (Widgets.ButtonText(new Rect(inRect.x + 440f, inRect.y, 180f, 24f), _personaIndex >= 0 && _personaIndex < names.Count ? names[_personaIndex] : "<选择>"))
            {
                var floatMenu = new List<FloatMenuOption>();
                for (int i = 0; i < names.Count; i++)
                {
                    int idx = i;
                    floatMenu.Add(new FloatMenuOption(names[i], () => _personaIndex = idx));
                }
                Find.WindowStack.Add(new FloatMenu(floatMenu));
            }

            if (Widgets.ButtonText(new Rect(inRect.x + 630f, inRect.y, 80f, 24f), "绑定"))
            {
                if (!string.IsNullOrWhiteSpace(_pawnIdInput) && _personaIndex >= 0 && _personaIndex < names.Count)
                {
                    _bindingService.Bind(_pawnIdInput.Trim(), names[_personaIndex], 0);
                }
            }

            // List section
            var listRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 80f);
            var list = _bindingService.GetAllBindings();
            var viewHeight = list.Count * 28f + 10f;
            var viewRect = new Rect(0, 0, listRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(listRect, ref _scroll, viewRect);
            float y = 0f;
            foreach (var b in list)
            {
                if (Widgets.ButtonText(new Rect(0, y, 120f, 24f), "解除"))
                {
                    _bindingService.Unbind(b.PawnId);
                }
                Widgets.Label(new Rect(130f, y, 220f, 24f), $"Pawn: {b.PawnId}");
                Widgets.Label(new Rect(360f, y, 200f, 24f), $"Persona: {b.PersonaName}#{b.Revision}");
                y += 28f;
            }
            Widgets.EndScrollView();
        }
    }
}


