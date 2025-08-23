// Basic imports
using System;
using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
// Modules import
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.UI.ServerChatWindow.Parts;
using RimAI.Core.Source.UI.ChatWindow.Parts; // reuse TitleBar style indirectly
using RimAI.Core.Source.Modules.Stage; // for IStageService

namespace RimAI.Core.Source.UI.ServerChatWindow
{
	internal sealed class ServerChatWindow : Window
	{
		// Services
		private readonly ServiceContainer _container;
		private readonly ILLMService _llm;
		private readonly IWorldDataService _world;
		private readonly IHistoryService _history;
		private readonly IRecapService _recap;
		private readonly IOrchestrationService _orchestration;
		private readonly IPromptService _prompting;
		private readonly IServerService _server;
	private readonly IStageService _stage;

		// Parameters
		private readonly string _serverEntityId;
		private static string _cachedPlayerId;

		// UI State
		private Vector2 _scrollRoster = Vector2.zero;
		private ServerTab _activeTab = ServerTab.Chat;
		private Texture _serverAvatar;
		private List<ServerListItem> _servers = new List<ServerListItem>();
		private double _nextServersRefreshRealtime;
		private double _nextSelectedInfoRefreshRealtime;
		private readonly object _serversLock = new object();
		private string _currentConvKey;
		private int? _currentServerThingId;
		private int _currentServerLevel;
		private string _currentServerTitle;
		
		// AI Log Part 状态容器
		private ServerAiLogTab.State _aiLogState = new ServerAiLogTab.State();

		// Design params
		public override Vector2 InitialSize => new Vector2(960f, 600f);

		public ServerChatWindow(string serverEntityId)
		{
			// 获取服务器实体ID - 全局唯一
			_serverEntityId = serverEntityId;

			// 配置窗体基本元素
			doCloseX = true;        // 添加关闭X键
			draggable = true;       // 允许拖动
			preventCameraMotion = false;        // 不会暂停游戏
			absorbInputAroundWindow = false;    // 允许点击窗体外的控件
			closeOnClickedOutside = false;      // 确保点击窗口外部不会触发关闭
			closeOnAccept = false;  // 确保Enter不会触发关闭
			closeOnCancel = true;   // 确保ESC可以触发关闭

			// 初始化服务容器
			_container = RimAICoreMod.Container;

			// 自动跟随当前游戏语言，并复用“手动设置”的完整替换逻辑
			InitializeLocaleFromGameLanguage(); // 已初始化容器，无需传入

			// 解析服务
			_llm = _container.Resolve<ILLMService>();
			_world = _container.Resolve<IWorldDataService>();
			_history = _container.Resolve<IHistoryService>();
			_recap = _container.Resolve<IRecapService>();
			_orchestration = _container.Resolve<IOrchestrationService>();
			_prompting = _container.Resolve<IPromptService>();
			_server = _container.Resolve<IServerService>();
			_stage = _container.Resolve<IStageService>();

			// 若从按钮携带了服务器实体ID，则直接初始化当前会话与标题/icon
			try { InitializeFromEntityId(_serverEntityId); } catch { }

		}

		public override void DoWindowContents(Rect inRect)
		{
			// 周期性后台刷新服务器列表（每3秒一次），避免 UI 线程阻塞
			TryScheduleServersRefresh();

			// 若当前已选服务器但标题或图标缺失，则节流尝试补齐（每 1 秒一次）
			TryRefreshSelectedInfoIfMissing();

			// 简版布局：左侧为服务器列表，右侧标题 + 预留主体
			var leftW = inRect.width * (1f / 6f) + 55f;
			var rightW = inRect.width - leftW - 8f;
			var leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
			var rightRectOuter = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);
			var titleH = 70f;
			var titleRect = new Rect(rightRectOuter.x, rightRectOuter.y, rightRectOuter.width, titleH);

			// 左栏：沿用 CW 风格，但使用 SCW 独立实现，后续可自由改造
			List<ServerListItem> snapshot;
			lock (_serversLock) { snapshot = new List<ServerListItem>(_servers); }
			ServerLeftSidebarCard.Draw(
				leftRect,
				ref _activeTab,
				"RimAI.ChatUI.Server.Name".Translate(),
				string.Empty,
				ref _scrollRoster,
				onBackToChat: RefreshChatForCurrentKey,
				onSelectServer: OnSelectServer,
				items: snapshot,
				isStreaming: false,
				getIcon: GetServerIcon
			);

			// 右侧标题栏（无生命体征）：展示当前会话服务器标题 + 基本信息（无 RID）
			var sub = _currentServerThingId.HasValue ? $"ID:{_currentServerThingId.Value}  LV:{_currentServerLevel}" : string.Empty;
			var name = !string.IsNullOrWhiteSpace(_currentServerTitle) ? _currentServerTitle : (_currentServerThingId.HasValue ? $"AI Server L{_currentServerLevel}" : "AI Server");
			ServerConversationHeader.Draw(titleRect, _serverAvatar, name, sub);

			// 右侧主体区域：根据 Tab 渲染
			var bodyRect = new Rect(rightRectOuter.x, titleRect.yMax + 6f, rightRectOuter.width, rightRectOuter.height - titleRect.height - 6f);
			switch (_activeTab)
			{
				case ServerTab.Chat:
					// 暂未实现聊天主体；预留占位
					break;
				case ServerTab.Persona:
					DrawPersonaBody(bodyRect);
					break;
				case ServerTab.AiLog:
					ServerAiLogTab.Draw(bodyRect, _aiLogState, _history, _stage);
					break;
				case ServerTab.History:
					// 预留
					break;
			}
		}

		private void TryScheduleServersRefresh()
		{
			var now = Time.realtimeSinceStartup;
			if (now < _nextServersRefreshRealtime) return;
			_nextServersRefreshRealtime = now + 5f;
			_ = RefreshServersAsync();
		}

		private async System.Threading.Tasks.Task RefreshServersAsync()
		{
			try
			{
				var ids = await _world.GetPoweredAiServerThingIdsAsync().ConfigureAwait(false);
				var list = new List<ServerListItem>();
				foreach (var id in ids)
				{
					int lvl = 1;
					try { lvl = await _world.GetAiServerLevelAsync(id).ConfigureAwait(false); } catch { lvl = 1; }
					list.Add(new ServerListItem { ThingId = id, Level = lvl, DisplayName = $"AI Server L{lvl}#{id}" });
				}
				lock (_serversLock)
				{
					_servers = list;
				}
			}
			catch { }
		}

		// Persona 页面的简单状态
		private Vector2 _scrollPersona = Vector2.zero;
		private string _personaName = string.Empty; // 展示标题
		private string _personaContent = string.Empty; // 覆盖文本
		private string _personaPresetKey = string.Empty; // 选中的预设键

		private void DrawPersonaBody(Rect rect)
		{
			ServerPersonaTab.Draw(rect,
				ref _personaPresetKey,
				ref _personaName,
				ref _personaContent,
				ref _scrollPersona,
				OnSelectPersonaPreset,
				OnSavePersona,
				OnClearPersonaOverride);
		}

		private void OnSelectPersonaPreset(string key, string name, string content)
		{
			_personaPresetKey = key ?? string.Empty;
			_personaName = name ?? string.Empty;
			_personaContent = content ?? string.Empty;
			// 立即记录预设键（不阻塞 UI）
			try
			{
				if (_currentServerThingId.HasValue && !string.IsNullOrWhiteSpace(_personaPresetKey))
				{
					var entityId = $"thing:{_currentServerThingId.Value}";
					_server?.SetBaseServerPersonaPreset(entityId, _personaPresetKey);
				}
			}
			catch { }
		}

		private void OnSavePersona()
		{
			// 将当前选择与覆盖写入 ServerService（持久化走 snapshot）
			try
			{
				if (!_currentServerThingId.HasValue) return;
				var entityId = $"thing:{_currentServerThingId.Value}"; // 与 gizmo 一致
				_server?.SetBaseServerPersonaOverride(entityId, string.IsNullOrWhiteSpace(_personaContent) ? null : _personaContent);
				if (!string.IsNullOrWhiteSpace(_personaPresetKey)) _server?.SetBaseServerPersonaPreset(entityId, _personaPresetKey);
			}
			catch { }
		}

		private void OnClearPersonaOverride()
		{
			try
			{
				if (!_currentServerThingId.HasValue) return;
				var entityId = $"thing:{_currentServerThingId.Value}";
				_server?.SetBaseServerPersonaOverride(entityId, null);
				_personaContent = string.Empty;
			}
			catch { }
		}

		private void LoadPersonaStateForCurrent()
		{
			try
			{
				if (!_currentServerThingId.HasValue) return;
				var entityId = $"thing:{_currentServerThingId.Value}";
				var rec = _server?.Get(entityId);
				_personaPresetKey = rec?.BaseServerPersonaPresetKey ?? string.Empty;
				_personaContent = rec?.BaseServerPersonaOverride ?? string.Empty;
				// 根据 key 映射到标题（从本地化预设中取）
				try
				{
					var loc = _container.Resolve<RimAI.Core.Source.Infrastructure.Localization.ILocalizationService>();
					var cfgInternal = _container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
					var overrideLocale = cfgInternal?.GetPromptLocaleOverrideOrNull();
					var locale = string.IsNullOrWhiteSpace(overrideLocale) ? (loc?.GetDefaultLocale() ?? cfgInternal?.GetInternal()?.General?.Locale ?? "en") : overrideLocale;
					var presetMgr = _container.Resolve<RimAI.Core.Source.Modules.Server.IServerPromptPresetManager>();
					var preset = System.Threading.Tasks.Task.Run(() => presetMgr.GetAsync(locale)).GetAwaiter().GetResult();
					if (preset?.ServerPersonaOptions != null && !string.IsNullOrWhiteSpace(_personaPresetKey))
					{
						foreach (var o in preset.ServerPersonaOptions)
						{
							if (string.Equals(o.key, _personaPresetKey, System.StringComparison.OrdinalIgnoreCase))
							{
								_personaName = o.title ?? string.Empty;
								break;
							}
						}
					}
					else if (string.IsNullOrWhiteSpace(_personaPresetKey))
					{
						_personaName = string.Empty;
					}
				}
				catch { }
			}
			catch { }
		}

		private void RefreshChatForCurrentKey()
		{
			// 回到对话页并做一次轻量“刷新”占位：重置标题/图标为当前服务器快照（不触发耗时操作）
			_activeTab = ServerTab.Chat;
			if (_currentServerThingId.HasValue)
			{
				var id = _currentServerThingId.Value;
				_currentServerTitle = TryGetServerTitle(id) ?? _currentServerTitle;
				if (_serverAvatar == null)
					_serverAvatar = GetServerIcon(new ServerListItem { ThingId = id, Level = _currentServerLevel });
			}
			// 真实消息区刷新将在接入 Chat 主体时统一读取 _currentConvKey
		}

		private void TryRefreshSelectedInfoIfMissing()
		{
			if (!_currentServerThingId.HasValue) return;
			bool needTitle = string.IsNullOrWhiteSpace(_currentServerTitle);
			bool needIcon = _serverAvatar == null;
			if (!needTitle && !needIcon) return;
			var now = Time.realtimeSinceStartup;
			if (now < _nextSelectedInfoRefreshRealtime) return;
			_nextSelectedInfoRefreshRealtime = now + 1f;
			try
			{
				var id = _currentServerThingId.Value;
				if (needTitle)
				{
					_currentServerTitle = TryGetServerTitle(id) ?? _currentServerTitle;
				}
				if (needIcon)
				{
					_serverAvatar = GetServerIcon(new ServerListItem { ThingId = id, Level = _currentServerLevel });
				}
			}
			catch { }
		}

		private void OnSelectServer(ServerListItem item)
		{
			if (item == null) return;
			try
			{
				_currentServerThingId = item.ThingId;
				_currentServerLevel = item.Level;
				_currentConvKey = BuildConvKeyForServer(item.ThingId);
				_serverAvatar = GetServerIcon(item);
				_currentServerTitle = TryGetServerTitle(item.ThingId) ?? $"AI Server L{item.Level}";
				// 切换服务器后加载其人格持久化状态
				LoadPersonaStateForCurrent();
				// 切回聊天页（主界面），便于测试点击效果
				_activeTab = ServerTab.Chat;
			}
			catch { }
		}

		private Texture GetServerIcon(ServerListItem item)
		{
			if (item == null) return null;
			try
			{
				// 访问地图中对应 thing 的图标材质（主线程 API），采用快路径：直接从 Thing.def.uiIcon / uiIconColor
				foreach (var map in Verse.Find.Maps)
				{
					var things = map?.listerThings?.AllThings; if (things == null) continue;
					for (int i = 0; i < things.Count; i++)
					{
						var t = things[i]; if (t == null || t.thingIDNumber != item.ThingId) continue;
						var tex = t.def?.uiIcon;
						if (tex != null) return tex;
						// 退路：尝试 Graphic.MatAt 主纹理
						var g = t.Graphic; var mat = g?.MatSingle;
						try { return mat?.mainTexture; } catch { return null; }
					}
				}
			}
			catch { }
			return null;
		}

		private static string BuildConvKeyForServer(int thingId)
		{
			return ServerKeyUtil.BuildForThingId(thingId, GetOrCreatePlayerSessionId());
		}

		private void InitializeFromEntityId(string entityId)
		{
			var norm = ServerKeyUtil.NormalizeEntityId(entityId);
			if (string.IsNullOrWhiteSpace(norm)) return;
			var id = ServerKeyUtil.TryParseThingId(norm);
			if (id.HasValue)
			{
				_currentServerThingId = id.Value;
				_currentConvKey = BuildConvKeyForServer(id.Value);
				_currentServerLevel = 1; // 先给默认等级，避免阻塞
				_currentServerTitle = TryGetServerTitle(id.Value) ?? $"AI Server L{_currentServerLevel}";
				_serverAvatar = GetServerIcon(new ServerListItem { ThingId = id.Value, Level = _currentServerLevel });
				// 加载当前服务器人格状态
				LoadPersonaStateForCurrent();
				// 后台补齐等级信息
				_ = PopulateServerLevelAsync(id.Value);
			}
			else
			{
				// 回退：无法解析 thingId，则用规范化 entityId 构建 convKey
				_currentConvKey = ServerKeyUtil.BuildFallbackForEntity(norm, GetOrCreatePlayerSessionId());
			}
		}

		private async System.Threading.Tasks.Task PopulateServerLevelAsync(int thingId)
		{
			try
			{
				var lvl = await _world.GetAiServerLevelAsync(thingId).ConfigureAwait(false);
				_currentServerLevel = lvl <= 0 ? 1 : lvl;
			}
			catch { }
		}



		private static string TryGetServerTitle(int thingId)
		{
			try
			{
				foreach (var map in Verse.Find.Maps)
				{
					var things = map?.listerThings?.AllThings; if (things == null) continue;
					for (int i = 0; i < things.Count; i++)
					{
						var t = things[i]; if (t == null || t.thingIDNumber != thingId) continue;
						var label = t.LabelCap?.ToString();
						if (!string.IsNullOrWhiteSpace(label)) return label;
						return t.def?.label ?? t.def?.defName ?? null;
					}
				}
			}
			catch { }
			return null;
		}

		private void InitializeLocaleFromGameLanguage()
		{
			try // 自动跟随当前游戏语言，并复用“手动设置”的完整替换逻辑
			{
				var loc = _container.Resolve<ILocalizationService>();
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var gameLang = LanguageDatabase.activeLanguage?.folderName ?? "English";
				if (string.IsNullOrWhiteSpace(cfg?.GetPromptLocaleOverrideOrNull()))
				{
					loc?.SetDefaultLocale(gameLang);
				}
			}
			catch
			{
				// 忽略本地化初始化异常，避免阻断窗口
			}
		}

		private static string GetOrCreatePlayerSessionId()
		{
			if (!string.IsNullOrEmpty(_cachedPlayerId)) return _cachedPlayerId;
			_cachedPlayerId = $"player:{Guid.NewGuid().ToString("N").Substring(0, 8)}";
			return _cachedPlayerId;
		}

	}
}


