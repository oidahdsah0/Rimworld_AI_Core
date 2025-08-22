using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.UI.ServerChatWindow.Parts;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.UI.ServerChatWindow
{
	public sealed class ServerChatWindow : Window
	{
		private readonly ServiceContainer _container;
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;
		private readonly IPromptService _prompting;
		private readonly IServerService _server;
		private readonly IOrchestrationService _orchestration;

		private ServerChatController _controller;
		private Vector2 _scrollLeft = Vector2.zero;
		private Vector2 _scrollTranscript = Vector2.zero;
		private string _inputText = string.Empty;
		private static string _cachedPlayerId;
		private CancellationTokenSource _tempCts;
		private ServerHistoryTabs _historyTabs;
		private InterServerCommsPanel _interComms;
		private ServerPersonaEditor _personaEditor;
		private enum Panel { Chat, History, Persona, InterComms }
		private Panel _panel = Panel.Chat;

		public override Vector2 InitialSize => new Vector2(980f, 620f);

		public ServerChatWindow(string serverEntityId)
		{
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
			absorbInputAroundWindow = false;
			closeOnClickedOutside = false;
			closeOnAccept = false;
			closeOnCancel = true;

			_container = RimAICoreMod.Container;
			_llm = _container.Resolve<ILLMService>();
			_history = _container.Resolve<IHistoryService>();
			_prompting = _container.Resolve<IPromptService>();
			_server = _container.Resolve<IServerService>();
			_orchestration = _container.Resolve<IOrchestrationService>();

			var playerId = GetOrCreatePlayerSessionId();
			var (convKey, pids) = ServerChatController.BuildConvForServer(serverEntityId, playerId);
			_controller = new ServerChatController(_llm, _history, _prompting, _server, _orchestration, convKey, pids, serverEntityId);
			_ = _controller.StartAsync();
			_tempCts = new CancellationTokenSource();
			_ = PollTemperatureAsync(_tempCts.Token);
		}

		public override void DoWindowContents(Rect inRect)
		{
			var leftW = inRect.width * (1f / 6f) + 45f;
			var leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
			var rightRect = new Rect(leftRect.xMax + 8f, inRect.y, inRect.width - leftW - 8f, inRect.height);
			var titleH = 72f;
			var indicatorH = 20f;
			var inputH = 60f;
			var titleRect = new Rect(rightRect.x, rightRect.y, rightRect.width, titleH);
			var transcriptRect = new Rect(rightRect.x, titleRect.yMax + 4f, rightRect.width, rightRect.height - titleH - indicatorH - inputH - 26f);
			var indicatorRect = new Rect(rightRect.x, transcriptRect.yMax + 4f, rightRect.width, indicatorH);
			var inputRect = new Rect(rightRect.x, indicatorRect.yMax + 12f, rightRect.width, inputH);

			DrawLeftSidebar(leftRect);
			DrawHeader(titleRect);
			switch (_panel)
			{
				case Panel.Chat:
					DrawTranscript(transcriptRect);
					DrawIndicators(indicatorRect);
					DrawInputRow(inputRect);
					break;
				case Panel.History:
					if (_historyTabs == null) _historyTabs = new ServerHistoryTabs();
					_historyTabs.Draw(new Rect(rightRect.x, rightRect.y + 32f, rightRect.width, rightRect.height - 32f), _controller?.State?.ConvKey);
					break;
				case Panel.Persona:
					if (_personaEditor == null) _personaEditor = new ServerPersonaEditor();
					_personaEditor.Draw(new Rect(rightRect.x, rightRect.y + 32f, rightRect.width, rightRect.height - 32f), _server, _controller?.State?.SelectedServerEntityId);
					break;
				case Panel.InterComms:
					if (_interComms == null) _interComms = new InterServerCommsPanel();
					_interComms.Draw(new Rect(rightRect.x, rightRect.y + 32f, rightRect.width, rightRect.height - 32f), _controller?.State?.SelectedServerEntityId);
					break;
			}
		}

		private void DrawLeftSidebar(Rect rect)
		{
			Widgets.DrawMenuSection(rect);
			var list = _server.List();
			var view = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f - 120f);
			Widgets.BeginScrollView(view, ref _scrollLeft, new Rect(0f, 0f, view.width - 16f, Math.Max(view.height, list.Count * 60f + 4f)));
			float y = 2f;
			foreach (var s in list)
			{
				var row = new Rect(0f, y, view.width - 20f, 56f);
				if (Widgets.ButtonText(row, $"#{Safe3(s.SerialHex12)} Lv{s.Level} {ShortPersona(s)}"))
				{
					var playerId = GetOrCreatePlayerSessionId();
					var (ck, pids) = ServerChatController.BuildConvForServer(s.EntityId, playerId);
					_controller = new ServerChatController(_llm, _history, _prompting, _server, _orchestration, ck, pids, s.EntityId);
					_ = _controller.StartAsync();
				}
				y += 60f;
			}
			Widgets.EndScrollView();

			var btnH = 26f; var pad = 6f; float bx = rect.x + 6f; float by = rect.yMax - pad - btnH * 4 - 6f;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.BackToChat".Translate())) { _panel = Panel.Chat; }
			by += btnH + pad;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.Persona".Translate())) { _panel = Panel.Persona; }
			by += btnH + pad;
			GUI.color = new Color(0.95f, 0.35f, 0.35f);
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.InterComms".Translate())) { _panel = Panel.InterComms; }
			GUI.color = Color.white;
			by += btnH + pad;
			if (Widgets.ButtonText(new Rect(bx, by, rect.width - 12f, btnH), "RimAI.ServerChatUI.Button.History".Translate())) { _panel = Panel.History; }
		}

		private void DrawHeader(Rect rect)
		{
			var s = _controller?.State?.SelectedServerEntityId;
			var rec = string.IsNullOrWhiteSpace(s) ? null : _server.Get(s);
			var title = rec == null ? "RimAI.ServerChatUI.Header.None".Translate().ToString() : $"#{Safe3(rec.SerialHex12)} Lv{rec.Level} {ShortPersona(rec)}";
			Text.Font = GameFont.Medium; Widgets.Label(rect, title); Text.Font = GameFont.Small;
			// 右侧温度柱状图
			var chartRect = new Rect(rect.xMax - 220f, rect.y + 4f, 200f, rect.height - 8f);
			float[] samples = null;
			try { samples = _controller?.State?.TemperatureSeries?.Samples?.ToArray(); } catch { samples = System.Array.Empty<float>(); }
			ServerTemperatureChart.Draw(chartRect, samples ?? System.Array.Empty<float>());
		}

		private void DrawTranscript(Rect rect)
		{
			Widgets.DrawMenuSection(rect);
			var state = _controller?.State; if (state == null) return;
			while (state.PendingInitMessages.TryDequeue(out var m)) state.Messages.Add(m);
			float contentW = rect.width - 16f; float textW = contentW - 12f; float total = 0f;
			for (int i = 0; i < state.Messages.Count; i++) { var m = state.Messages[i]; var label = $"[{m.DisplayName} {m.TimestampUtc.ToLocalTime():HH:mm:ss}] {m.Text}"; total += Mathf.Max(24f, Text.CalcHeight(label, textW)) + 6f; }
			var view = new Rect(rect.x, rect.y, rect.width, rect.height);
			var inner = new Rect(0f, 0f, view.width - 16f, Math.Max(view.height, total + 8f));
			Widgets.BeginScrollView(view, ref _scrollTranscript, inner);
			float y = 4f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var m = state.Messages[i];
				var label = $"[{m.DisplayName} {m.TimestampUtc.ToLocalTime():HH:mm:ss}] {m.Text}";
				var rowH = Mathf.Max(24f, Text.CalcHeight(label, textW)) + 4f;
				Widgets.Label(new Rect(4f, y, textW, rowH), label);
				y += rowH + 2f;
			}
			Widgets.EndScrollView();
		}

		private void DrawIndicators(Rect rect)
		{
			Widgets.DrawBoxSolid(rect, new Color(0,0,0,0.05f));
			var busy = _controller?.State?.IsBusy == true;
			var text = busy ? "RimAI.Common.Busy".Translate().ToString() : "RimAI.Common.Idle".Translate().ToString();
			Widgets.Label(rect, text);
		}

		private void DrawInputRow(Rect rect)
		{
			var box = new Rect(rect.x, rect.y, rect.width - 260f, rect.height);
			_inputText = Widgets.TextField(box, _inputText ?? string.Empty);
			if (Widgets.ButtonText(new Rect(rect.xMax - 252f, rect.y, 80f, rect.height), "RimAI.Common.Smalltalk".Translate())) { _ = OnSendSmalltalkAsync(); }
			if (Widgets.ButtonText(new Rect(rect.xMax - 168f, rect.y, 80f, rect.height), "RimAI.Common.Command".Translate())) { _ = OnSendCommandAsync(); }
			if (Widgets.ButtonText(new Rect(rect.xMax - 84f, rect.y, 80f, rect.height), "RimAI.Common.Cancel".Translate())) { _controller.Cancel(); }
		}

		private async System.Threading.Tasks.Task OnSendSmalltalkAsync()
		{
			var text = _inputText?.Trim(); if (string.IsNullOrEmpty(text)) return; _inputText = string.Empty;
			await _controller.SendSmalltalkAsync(text);
		}

		private async System.Threading.Tasks.Task OnSendCommandAsync()
		{
			var text = _inputText?.Trim(); if (string.IsNullOrEmpty(text)) return; _inputText = string.Empty;
			await _controller.SendCommandAsync(text);
		}

		private static string GetOrCreatePlayerSessionId()
		{
			if (!string.IsNullOrEmpty(_cachedPlayerId)) return _cachedPlayerId;
			_cachedPlayerId = $"player:{Guid.NewGuid().ToString("N").Substring(0, 8)}";
			return _cachedPlayerId;
		}

		public override void PreClose()
		{
			base.PreClose();
			try { _tempCts?.Cancel(); } catch { }
		}

		private async System.Threading.Tasks.Task PollTemperatureAsync(System.Threading.CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					var s = _controller?.State?.SelectedServerEntityId;
					if (!string.IsNullOrWhiteSpace(s))
					{
						var v = _server.GetRecommendedSamplingTemperature(s);
						v = Mathf.Clamp(v, 0.5f, 2.0f);
						_controller?.State?.TemperatureSeries?.Push(v);
					}
				}
				catch { }
				await System.Threading.Tasks.Task.Delay(2500, ct);
			}
		}

		private static string Safe3(string serial)
		{
			if (string.IsNullOrWhiteSpace(serial)) return "---";
			return serial.Length <= 3 ? serial : serial.Substring(0, 3);
		}

		private static string ShortPersona(ServerRecord s)
		{
			try
			{
				if (s == null) return string.Empty;
				if (s.ServerPersonaSlots != null)
				{
					foreach (var slot in s.ServerPersonaSlots)
					{
						if (slot != null && slot.Enabled && !string.IsNullOrWhiteSpace(slot.PresetKey)) return slot.PresetKey;
					}
				}
				if (!string.IsNullOrWhiteSpace(s.BaseServerPersonaPresetKey)) return s.BaseServerPersonaPresetKey;
				return string.IsNullOrWhiteSpace(s.BaseServerPersonaOverride) ? string.Empty : "Custom";
			}
			catch { return string.Empty; }
		}
	}
}


