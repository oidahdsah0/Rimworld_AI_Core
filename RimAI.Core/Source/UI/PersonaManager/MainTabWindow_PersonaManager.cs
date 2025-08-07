using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure;
using UnityEngine;
using RimWorld;
using Verse;

namespace RimAI.Core.UI.PersonaManager
{
    /// <summary>
    /// 人格管理窗口（P8 原型）。
    /// 允许玩家创建、编辑、删除自定义 Persona，并立即生效。
    /// </summary>
    public class MainTabWindow_PersonaManager : MainTabWindow
    {
        private readonly IPersonaService _personaService;

        private Vector2 _scrollPos = Vector2.zero;
        private string _newName = string.Empty;
        private string _newPrompt = string.Empty;
        private string _error = string.Empty;
        private int _selectedIndex = -1;
        private List<Persona> _cachedList;

        private const float RowHeight = 28f;
        private const float NameColWidth = 160f;
        private const float PromptColWidth = 400f;
        private const float Padding = 10f;

        public MainTabWindow_PersonaManager()
        {
            _personaService = CoreServices.Locator.Get<IPersonaService>();
            forcePause = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;
            UpdateCache();
        }

        public override Vector2 InitialSize => new Vector2(720f, 480f);

        private void UpdateCache()
        {
            _cachedList = _personaService.GetAll().OrderBy(p => p.Name).ToList();
            if (_selectedIndex >= _cachedList.Count)
                _selectedIndex = -1;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Anchor = TextAnchor.UpperLeft;
            var listRect = new Rect(inRect.x, inRect.y, NameColWidth + PromptColWidth + 40f, inRect.height - 140f);

            // 列表
            var viewHeight = _cachedList.Count * RowHeight;
            var viewRect = new Rect(0, 0, listRect.width - 16f, viewHeight);
            Widgets.BeginScrollView(listRect, ref _scrollPos, viewRect);

            for (int i = 0; i < _cachedList.Count; i++)
            {
                var y = i * RowHeight;
                var rowRect = new Rect(0, y, viewRect.width, RowHeight);
                if (i % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                if (Widgets.RadioButtonLabeled(new Rect(4, y + 4, NameColWidth, RowHeight - 4), _cachedList[i].Name, _selectedIndex == i))
                {
                    _selectedIndex = i;
                    _newName = _cachedList[i].Name;
                    _newPrompt = _cachedList[i].SystemPrompt;
                    _error = string.Empty;
                }

                // 提示词预览（截断）
                var promptPreview = _cachedList[i].SystemPrompt.Length > 40 ? _cachedList[i].SystemPrompt.Substring(0, 40) + "…" : _cachedList[i].SystemPrompt;
                Widgets.Label(new Rect(NameColWidth + 8, y + 4, PromptColWidth, RowHeight - 4), promptPreview);
            }

            Widgets.EndScrollView();

            // 编辑面板
            float editY = listRect.yMax + Padding;
            Widgets.Label(new Rect(inRect.x, editY, 60f, 24f), "名称");
            _newName = Widgets.TextField(new Rect(inRect.x + 60f, editY, 200f, 24f), _newName);

            editY += 30f;
            Widgets.Label(new Rect(inRect.x, editY, 60f, 24f), "系统提示");
            _newPrompt = Widgets.TextArea(new Rect(inRect.x + 60f, editY, inRect.width - 80f, 60f), _newPrompt);

            // 错误信息
            if (!string.IsNullOrEmpty(_error))
            {
                Widgets.Label(new Rect(inRect.x, editY + 70f, inRect.width - 20f, 24f), $"<color=red>{_error}</color>");
            }

            // 操作按钮
            var btnY = inRect.yMax - 32f;
            if (Widgets.ButtonText(new Rect(inRect.x, btnY, 120f, 28f), "保存/更新"))
            {
                TrySave();
            }
            if (Widgets.ButtonText(new Rect(inRect.x + 130f, btnY, 80f, 28f), "删除"))
            {
                TryDelete();
            }
            if (Widgets.ButtonText(new Rect(inRect.x + 220f, btnY, 80f, 28f), "新增"))
            {
                _newName = string.Empty;
                _newPrompt = string.Empty;
                _selectedIndex = -1;
                _error = string.Empty;
            }
        }

        private void TrySave()
        {
            if (string.IsNullOrWhiteSpace(_newName) || string.IsNullOrWhiteSpace(_newPrompt))
            {
                _error = "名称和提示词均不能为空";
                return;
            }

            var persona = new Persona(_newName.Trim(), _newPrompt.Trim());
            bool ok;
            if (_selectedIndex >= 0 && _selectedIndex < _cachedList.Count && string.Equals(_cachedList[_selectedIndex].Name, persona.Name, StringComparison.OrdinalIgnoreCase))
            {
                ok = _personaService.Update(persona);
            }
            else
            {
                ok = _personaService.Add(persona);
                if (!ok) _error = "名称已存在";
            }
            if (ok) { _error = string.Empty; UpdateCache(); }
        }

        private void TryDelete()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _cachedList.Count) return;
            var name = _cachedList[_selectedIndex].Name;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"确认删除人格 '{name}'?", () =>
            {
                _personaService.Delete(name);
                _selectedIndex = -1;
                UpdateCache();
            }));

        }
    }
}
