# 👨‍💻 RimAI 开发者指南

*基于依赖注入架构的完整开发教程*

## 🧠 核心概念

### 架构核心：依赖注入（DI）

RimAI Core 已从静态单例模式全面迁移到依赖注入架构。这是理解整个框架的关键。

#### 1. ServiceContainer - 依赖注入容器

`ServiceContainer` 是整个框架的心脏，负责管理所有服务的生命周期：

```csharp
// 服务注册（在框架内部进行）
ServiceContainer.Instance.RegisterService<IHistoryService>(new HistoryService());

// 服务获取（通过CoreServices门面）
var historyService = CoreServices.History;
```

**核心职责：**
- 服务注册与获取
- 生命周期管理
- 依赖关系解析
- 类型安全的服务访问

#### 2. CoreServices - 统一服务门面

`CoreServices` 是所有模块访问核心服务的**唯一、标准入口**：

```csharp
public static class CoreServices
{
    // AI服务
    public static Governor Governor => ServiceContainer.Instance?.GetService<Governor>();
    public static ILLMService LLMService => ServiceContainer.Instance?.GetService<ILLMService>();
    
    // 新增的核心服务
    public static IHistoryService History => ServiceContainer.Instance?.GetService<IHistoryService>();
    public static IPromptFactoryService PromptFactory => ServiceContainer.Instance?.GetService<IPromptFactoryService>();
    
    // 基础架构服务
    public static ICacheService CacheService => ServiceContainer.Instance?.GetService<ICacheService>();
    public static IEventBus EventBus => ServiceContainer.Instance?.GetService<IEventBus>();
    public static IPersistenceService PersistenceService => ServiceContainer.Instance?.GetService<IPersistenceService>();
    public static ISafeAccessService SafeAccessService => ServiceContainer.Instance?.GetService<ISafeAccessService>();
    public static IColonyAnalyzer Analyzer => ServiceContainer.Instance?.GetService<IColonyAnalyzer>();
    
    // 玩家身份标识
    public static string PlayerStableId => Faction.OfPlayer.GetUniqueLoadID();
    public static string PlayerDisplayName => SettingsManager.Settings.Player.Nickname;
    
    // 系统状态检查
    public static bool AreServicesReady() { /* ... */ }
}
```

**重要原则：**
- ✅ **始终通过 CoreServices 访问服务**
- ❌ **不再使用旧的 `.Instance` 静态属性**
- 🛡️ **在使用前检查 `AreServicesReady()`**

#### 3. 玩家身份处理

框架提供两种不同用途的玩家标识：

```csharp
// 稳定ID：用于后台数据关联，永不改变
string stableId = CoreServices.PlayerStableId; 
// 实际值："RimWorld.Faction_e1b2c3d4"

// 显示名称：用户可在设置中修改，用于UI和AI对话
string displayName = CoreServices.PlayerDisplayName; 
// 实际值："指挥官王小明"（用户设置的昵称）
```

**应用场景：**
- `PlayerStableId`：用于对话历史、数据持久化、系统内部关联
- `PlayerDisplayName`：用于UI显示、AI对话中的称呼

## 🏗️ 创建新服务完整教程

以 `HistoryService` 为例，展示如何从零开始创建一个完整的服务。

### 步骤1：定义服务接口

首先在 `Architecture/Interfaces/` 目录下定义接口：

```csharp
// IHistoryService.cs
using System.Collections.Generic;
using RimAI.Core.Architecture.Models;

namespace RimAI.Core.Architecture.Interfaces
{
    public interface IHistoryService : IPersistable
    {
        /// <summary>
        /// 为一组参与者开始或获取一个对话ID
        /// </summary>
        string StartOrGetConversation(List<string> participantIds);
        
        /// <summary>
        /// 向指定的对话中添加一条记录
        /// </summary>
        void AddEntry(string conversationId, ConversationEntry entry);
        
        /// <summary>
        /// 获取结构化的历史上下文，区分主线对话和附加参考对话
        /// </summary>
        HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10);
    }
}
```

### 步骤2：实现服务

在 `Services/` 目录下实现具体服务：

```csharp
// HistoryService.cs
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using Verse;

namespace RimAI.Core.Services
{
    public class HistoryService : IHistoryService
    {
        // 主数据存储：对话ID -> 对话记录列表
        private Dictionary<string, List<ConversationEntry>> _conversationStore 
            = new Dictionary<string, List<ConversationEntry>>();
        
        // 倒排索引：参与者ID -> 相关对话ID集合
        private Dictionary<string, HashSet<string>> _participantIndex 
            = new Dictionary<string, HashSet<string>>();

        public string StartOrGetConversation(List<string> participantIds)
        {
            if (participantIds == null || participantIds.Count == 0) return null;

            // 通过排序和拼接生成稳定的对话ID
            var sortedIds = participantIds.Distinct().OrderBy(id => id).ToList();
            var conversationId = string.Join("_", sortedIds);

            if (!_conversationStore.ContainsKey(conversationId))
            {
                _conversationStore[conversationId] = new List<ConversationEntry>();
                
                // 更新倒排索引
                foreach (var id in sortedIds)
                {
                    if (!_participantIndex.ContainsKey(id))
                        _participantIndex[id] = new HashSet<string>();
                    _participantIndex[id].Add(conversationId);
                }
            }

            return conversationId;
        }

        public void AddEntry(string conversationId, ConversationEntry entry)
        {
            if (string.IsNullOrEmpty(conversationId) || entry == null) return;

            if (_conversationStore.TryGetValue(conversationId, out var history))
            {
                entry.GameTicksTimestamp = CoreServices.SafeAccessService.GetTicksGameSafe();
                history.Add(entry);
            }
        }

        public HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10)
        {
            var context = new HistoricalContext();
            if (primaryParticipants == null || primaryParticipants.Count == 0) return context;

            var sortedPrimaryIds = primaryParticipants.Distinct().OrderBy(id => id).ToList();
            var primaryConversationId = string.Join("_", sortedPrimaryIds);

            // 1. 获取主线历史
            if (_conversationStore.TryGetValue(primaryConversationId, out var primaryHistory))
            {
                context.PrimaryHistory = primaryHistory
                    .OrderByDescending(e => e.GameTicksTimestamp)
                    .Take(limit)
                    .Reverse()
                    .ToList();
            }

            // 2. 通过倒排索引查找相关对话
            HashSet<string> relevantConversationIds = null;
            foreach (var id in sortedPrimaryIds)
            {
                if (_participantIndex.TryGetValue(id, out var conversations))
                {
                    if (relevantConversationIds == null)
                        relevantConversationIds = new HashSet<string>(conversations);
                    else
                        relevantConversationIds.IntersectWith(conversations);
                }
                else
                {
                    return context; // 如果任一参与者不在索引中，没有共同对话
                }
            }

            // 3. 收集附加历史（排除主线对话）
            if (relevantConversationIds != null)
            {
                var ancillaryHistory = new List<ConversationEntry>();
                foreach (var convId in relevantConversationIds)
                {
                    if (convId != primaryConversationId && _conversationStore.TryGetValue(convId, out var history))
                    {
                        ancillaryHistory.AddRange(history);
                    }
                }
                
                context.AncillaryHistory = ancillaryHistory
                    .OrderByDescending(e => e.GameTicksTimestamp)
                    .Take(limit)
                    .Reverse()
                    .ToList();
            }

            return context;
        }

        // 实现IPersistable接口，支持随存档保存
        public void ExposeData()
        {
            Scribe_Collections.Look(ref _conversationStore, "conversationStore", LookMode.Value, LookMode.Deep);
            Scribe_Collections.Look(ref _participantIndex, "participantIndex", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _conversationStore ??= new Dictionary<string, List<ConversationEntry>>();
                _participantIndex ??= new Dictionary<string, HashSet<string>>();
            }
        }
    }
}
```

### 步骤3：注册服务

在 `ServiceContainer.cs` 的 `RegisterDefaultServices()` 方法中注册：

```csharp
private void RegisterDefaultServices()
{
    // ... 其他服务注册 ...
    
    // 注册我们的新服务
    RegisterService<IHistoryService>(new HistoryService());
    
    // ... 继续注册其他服务 ...
}
```

### 步骤4：添加到CoreServices门面

在 `ServiceContainer.cs` 的 `CoreServices` 类中添加访问器：

```csharp
public static class CoreServices
{
    // ... 其他服务属性 ...
    
    public static IHistoryService History => ServiceContainer.Instance?.GetService<IHistoryService>();
    
    // ... 其他属性 ...
}
```

### 步骤5：持久化注册（可选）

如果服务实现了 `IPersistable`，需要在构造函数中自动注册：

```csharp
public HistoryService()
{
    // 自动注册到持久化服务
    CoreServices.PersistenceService?.RegisterPersistable(this);
}
```

## 🤖 与AI交互：新的提示词构建方式

废弃旧的字符串拼接方式，使用 `PromptFactoryService` 和 `PromptBuildConfig` 进行结构化提示词构建。

### 1. 基本AI交互流程

```csharp
public async Task<string> GetAIAdviceAsync(string userQuery)
{
    // 1. 检查服务就绪状态
    if (!CoreServices.AreServicesReady())
    {
        return "AI服务暂时不可用，请稍后重试。";
    }

    try
    {
        // 2. 构建结构化提示词配置
        var promptConfig = new PromptBuildConfig
        {
            CurrentParticipants = new List<string> 
            { 
                CoreServices.PlayerStableId, 
                "Governor" 
            },
            SystemPrompt = "你是RimWorld殖民地的AI总督，提供专业的管理建议。",
            Scene = new SceneContext 
            { 
                Situation = $"玩家询问：{userQuery}",
                Time = GetCurrentGameTime(),
                Location = GetCurrentMapInfo()
            },
            HistoryLimit = 10
        };

        // 3. 通过PromptFactory构建完整提示词
        var promptPayload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
        
        // 4. 添加当前用户输入
        promptPayload.Messages.Add(new ChatMessage 
        { 
            Role = "user", 
            Content = userQuery, 
            Name = CoreServices.PlayerDisplayName 
        });

        // 5. 发送给LLM服务
        var promptText = ConvertToPromptText(promptPayload);
        var response = await CoreServices.LLMService.SendMessageAsync(promptText);

        // 6. 记录对话历史
        var conversationId = CoreServices.History.StartOrGetConversation(promptConfig.CurrentParticipants);
        CoreServices.History.AddEntry(conversationId, new ConversationEntry
        {
            ParticipantId = CoreServices.PlayerStableId,
            Role = "user",
            Content = userQuery
        });
        CoreServices.History.AddEntry(conversationId, new ConversationEntry
        {
            ParticipantId = "Governor",
            Role = "assistant", 
            Content = response
        });

        return response;
    }
    catch (Exception ex)
    {
        Log.Error($"AI交互失败: {ex.Message}");
        return "抱歉，处理您的请求时出现了问题。";
    }
}

private string ConvertToPromptText(PromptPayload payload)
{
    return string.Join("\n", payload.Messages.Select(m => 
        $"{m.Role} ({m.Name ?? "System"}): {m.Content}"));
}
```

### 2. 高级提示词构建示例

```csharp
public class MedicalOfficer : OfficerBase
{
    protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
    {
        // 构建医疗专业的提示词配置
        var promptConfig = new PromptBuildConfig
        {
            CurrentParticipants = new List<string> { CoreServices.PlayerStableId, "MedicalOfficer" },
            SystemPrompt = @"你是殖民地的专业医疗官。你的职责是：
1. 监控殖民者健康状况
2. 提供医疗建议和治疗方案
3. 预防疾病爆发
4. 管理医疗资源

保持专业、准确、关注安全。",
            Scene = new SceneContext
            {
                Situation = "例行医疗状况检查",
                Location = await GetCurrentMedicalFacilities(),
                Participants = await GetMedicalStaff()
            },
            OtherData = new AncillaryData
            {
                ReferenceInfo = await GetMedicalSuppliesInventory(),
                Weather = await GetCurrentWeatherImpact()
            },
            HistoryLimit = 15 // 医疗历史需要更多上下文
        };

        // 使用PromptFactory构建结构化提示词
        var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
        
        // 添加当前医疗数据
        var medicalSummary = await GenerateMedicalSummary();
        payload.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = $"当前医疗状况：\n{medicalSummary}",
            Name = "System"
        });

        // 发送到LLM并返回
        var promptText = ConvertToPromptText(payload);
        return await CoreServices.LLMService.SendMessageAsync(promptText, cancellationToken);
    }
}
```

### 3. 对话历史的智能利用

```csharp
public async Task<string> ContinueConversationAsync(string newMessage, List<string> participants)
{
    // 获取历史上下文
    var historicalContext = CoreServices.History.GetHistoricalContextFor(participants, limit: 20);
    
    var promptConfig = new PromptBuildConfig
    {
        CurrentParticipants = participants,
        SystemPrompt = "基于之前的对话历史，继续这个对话。保持上下文一致性。",
        HistoryLimit = 0 // 我们手动处理历史
    };

    var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
    
    // 手动添加分层历史
    if (historicalContext.AncillaryHistory.Count > 0)
    {
        var ancillaryText = $"[背景对话记录]：\n{FormatHistory(historicalContext.AncillaryHistory)}";
        payload.Messages.Add(new ChatMessage { Role = "system", Content = ancillaryText });
    }
    
    // 添加主线对话历史
    foreach (var entry in historicalContext.PrimaryHistory)
    {
        payload.Messages.Add(new ChatMessage
        {
            Role = entry.Role,
            Content = entry.Content,
            Name = entry.ParticipantId
        });
    }
    
    // 添加新消息
    payload.Messages.Add(new ChatMessage 
    { 
        Role = "user", 
        Content = newMessage,
        Name = CoreServices.PlayerDisplayName
    });

    var response = await CoreServices.LLMService.SendMessageAsync(ConvertToPromptText(payload));
    
    // 更新历史
    var conversationId = CoreServices.History.StartOrGetConversation(participants);
    CoreServices.History.AddEntry(conversationId, new ConversationEntry
    {
        ParticipantId = CoreServices.PlayerStableId,
        Role = "user",
        Content = newMessage
    });
    CoreServices.History.AddEntry(conversationId, new ConversationEntry
    {
        ParticipantId = participants.FirstOrDefault(p => p != CoreServices.PlayerStableId) ?? "AI",
        Role = "assistant",
        Content = response
    });

    return response;
}
```

## 🎛️ 创建自定义AI官员

### 1. 实现OfficerBase

```csharp
using RimAI.Core.Officers.Base;
using RimAI.Core.Architecture.Models;

namespace MyMod.Officers
{
    public class SecurityOfficer : OfficerBase
    {
        public override string Name => "安全官";
        public override string Description => "负责殖民地安全防务和威胁分析";
        public override OfficerRole Role => OfficerRole.Security;
        public override string IconPath => "UI/Icons/Security";

        protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
        {
            var promptConfig = new PromptBuildConfig
            {
                CurrentParticipants = new List<string> { CoreServices.PlayerStableId, "SecurityOfficer" },
                SystemPrompt = @"你是殖民地安全官，负责：
1. 威胁评估和防御策略
2. 武器装备管理
3. 训练计划制定
4. 紧急响应预案

保持警惕、专业、注重安全。",
                Scene = await BuildSecurityContext(),
                HistoryLimit = 10
            };

            var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(promptConfig);
            
            // 添加当前威胁分析
            var threatAnalysis = await AnalyzeCurrentThreats();
            payload.Messages.Add(new ChatMessage
            {
                Role = "user",
                Content = $"当前威胁分析：\n{threatAnalysis}",
                Name = "SecuritySystem"
            });

            var promptText = ConvertToPromptText(payload);
            return await CoreServices.LLMService.SendMessageAsync(promptText, cancellationToken);
        }

        private async Task<SceneContext> BuildSecurityContext()
        {
            return new SceneContext
            {
                Situation = "例行安全评估",
                Location = await GetDefensePositions(),
                Participants = await GetSecurityPersonnel()
            };
        }

        private async Task<string> AnalyzeCurrentThreats()
        {
            var threats = await CoreServices.SafeAccessService.GetThreatsAsync();
            // 分析威胁逻辑
            return "威胁分析报告...";
        }
    }
}
```

### 2. 注册自定义官员

```csharp
// 在您的模组初始化中
public class MyModInitializer
{
    public static void Initialize()
    {
        // 注册到服务容器
        ServiceContainer.Instance.RegisterService<SecurityOfficer>(new SecurityOfficer());
        ServiceContainer.Instance.RegisterService<IAIOfficer>(SecurityOfficer.Instance, "SecurityOfficer");
        
        Log.Message("[MyMod] SecurityOfficer registered successfully.");
    }
}

// 在CoreServices中添加访问器（如果需要直接访问）
public static class MyModServices
{
    public static SecurityOfficer SecurityOfficer => 
        ServiceContainer.Instance.GetService<SecurityOfficer>();
}
```

## 💾 持久化数据开发

### 1. 实现IPersistable接口

```csharp
public class AITaskManager : IPersistable
{
    private List<string> _activeTasks = new List<string>();
    private Dictionary<string, TaskProgress> _taskProgress = new Dictionary<string, TaskProgress>();

    public AITaskManager()
    {
        // 在构造函数中自动注册到持久化服务
        CoreServices.PersistenceService?.RegisterPersistable(this);
    }

    public void ExposeData()
    {
        // 使用Scribe系统保存/加载数据
        Scribe_Collections.Look(ref _activeTasks, "activeTasks", LookMode.Value);
        Scribe_Collections.Look(ref _taskProgress, "taskProgress", LookMode.Value, LookMode.Deep);

        // 加载后的初始化
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _activeTasks ??= new List<string>();
            _taskProgress ??= new Dictionary<string, TaskProgress>();
        }
    }

    // 业务逻辑方法
    public void AddTask(string taskId, string description)
    {
        if (!_activeTasks.Contains(taskId))
        {
            _activeTasks.Add(taskId);
            _taskProgress[taskId] = new TaskProgress { Description = description, StartTime = DateTime.Now };
        }
    }
}
```

### 2. 全局设置管理

```csharp
public static class ModGlobalSettings
{
    private const string SETTINGS_KEY = "MyMod_GlobalConfig";
    
    public static async Task<ModConfig> LoadConfigAsync()
    {
        var config = await CoreServices.PersistenceService.LoadGlobalSettingAsync<ModConfig>(SETTINGS_KEY);
        return config ?? new ModConfig(); // 返回默认配置如果加载失败
    }
    
    public static async Task SaveConfigAsync(ModConfig config)
    {
        await CoreServices.PersistenceService.SaveGlobalSettingAsync(SETTINGS_KEY, config);
        Log.Message("[ModGlobalSettings] Configuration saved.");
    }
}

public class ModConfig
{
    public string ApiEndpoint { get; set; } = "";
    public bool EnableAdvancedFeatures { get; set; } = false;
    public Dictionary<string, string> CustomSettings { get; set; } = new Dictionary<string, string>();
}
```

## 🎨 UI开发最佳实践

### 1. 异步UI处理

```csharp
public class MyAIWindow : Window
{
    private string _responseText = "";
    private bool _isProcessing = false;

    public override void DoWindowContents(Rect inRect)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // 显示响应文本
        var textRect = listing.GetRect(200f);
        Widgets.TextArea(textRect, _responseText, true);

        listing.Gap(10f);

        // AI请求按钮
        GUI.enabled = !_isProcessing && CoreServices.AreServicesReady();
        if (listing.ButtonText(_isProcessing ? "处理中..." : "获取AI建议"))
        {
            HandleAIRequest();
        }
        GUI.enabled = true;

        listing.End();
    }

    private async void HandleAIRequest()
    {
        if (_isProcessing) return;

        _isProcessing = true;
        _responseText = "正在思考中...";

        try
        {
            var response = await CoreServices.Governor.ProvideAdviceAsync();
            _responseText = response;
        }
        catch (Exception ex)
        {
            _responseText = $"请求失败: {ex.Message}";
            Log.Error($"[MyAIWindow] AI请求失败: {ex}");
        }
        finally
        {
            _isProcessing = false;
        }
    }
}
```

### 2. 对话界面开发

```csharp
public class ConversationWindow : Window
{
    private List<ChatMessage> _messages = new List<ChatMessage>();
    private string _currentInput = "";
    private Vector2 _scrollPosition;

    public override void DoWindowContents(Rect inRect)
    {
        // 聊天历史显示区域
        var historyRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 60f);
        DrawChatHistory(historyRect);

        // 输入区域
        var inputRect = new Rect(inRect.x, historyRect.yMax + 10f, inRect.width - 80f, 30f);
        var sendRect = new Rect(inputRect.xMax + 10f, inputRect.y, 70f, 30f);

        _currentInput = Widgets.TextField(inputRect, _currentInput);
        
        if (Widgets.ButtonText(sendRect, "发送") && !string.IsNullOrWhiteSpace(_currentInput))
        {
            SendMessage();
        }
    }

    private void DrawChatHistory(Rect rect)
    {
        var viewRect = new Rect(0, 0, rect.width - 16f, _messages.Count * 40f);
        
        Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);
        
        var y = 0f;
        foreach (var message in _messages)
        {
            var messageRect = new Rect(0, y, viewRect.width, 35f);
            DrawMessage(messageRect, message);
            y += 40f;
        }
        
        Widgets.EndScrollView();
    }

    private void DrawMessage(Rect rect, ChatMessage message)
    {
        var color = message.Role == "user" ? Color.cyan : Color.white;
        GUI.color = color;
        
        var displayName = message.Name ?? (message.Role == "user" ? CoreServices.PlayerDisplayName : "AI");
        Widgets.Label(rect, $"{displayName}: {message.Content}");
        
        GUI.color = Color.white;
    }

    private async void SendMessage()
    {
        var userMessage = _currentInput;
        _currentInput = "";

        // 添加用户消息到历史
        _messages.Add(new ChatMessage 
        { 
            Role = "user", 
            Content = userMessage, 
            Name = CoreServices.PlayerDisplayName 
        });

        try
        {
            // 发送给AI并获取响应
            var response = await CoreServices.Governor.HandleUserQueryAsync(userMessage);
            
            // 添加AI响应到历史
            _messages.Add(new ChatMessage 
            { 
                Role = "assistant", 
                Content = response, 
                Name = "总督" 
            });
        }
        catch (Exception ex)
        {
            _messages.Add(new ChatMessage 
            { 
                Role = "system", 
                Content = $"错误: {ex.Message}", 
                Name = "系统" 
            });
        }
    }
}
```

## 🔍 调试和故障排除

### 1. 服务状态诊断

```csharp
public static class DiagnosticTool
{
    public static void RunFullDiagnostics()
    {
        Log.Message("=== RimAI 完整诊断开始 ===");
        
        CheckCoreServices();
        CheckServiceContainer();
        CheckHistoryService();
        CheckPromptFactory();
        CheckPersistence();
        
        Log.Message("=== RimAI 完整诊断完成 ===");
    }

    private static void CheckCoreServices()
    {
        Log.Message("--- 核心服务检查 ---");
        
        var serviceChecks = new Dictionary<string, object>
        {
            ["ServiceContainer"] = ServiceContainer.Instance,
            ["Governor"] = CoreServices.Governor,
            ["LLMService"] = CoreServices.LLMService,
            ["History"] = CoreServices.History,
            ["PromptFactory"] = CoreServices.PromptFactory,
            ["PersistenceService"] = CoreServices.PersistenceService,
            ["CacheService"] = CoreServices.CacheService,
            ["EventBus"] = CoreServices.EventBus,
            ["SafeAccessService"] = CoreServices.SafeAccessService,
            ["Analyzer"] = CoreServices.Analyzer
        };

        foreach (var (name, service) in serviceChecks)
        {
            var status = service != null ? "✅ 就绪" : "❌ 未就绪";
            Log.Message($"[诊断] {name}: {status}");
        }

        var overallStatus = CoreServices.AreServicesReady() ? "✅ 所有服务就绪" : "❌ 部分服务未就绪";
        Log.Message($"[诊断] 整体状态: {overallStatus}");
    }

    private static void CheckHistoryService()
    {
        Log.Message("--- 历史服务检查 ---");
        
        try
        {
            var history = CoreServices.History;
            if (history == null)
            {
                Log.Warning("[诊断] HistoryService 未初始化");
                return;
            }

            // 创建测试对话
            var testParticipants = new List<string> { "TestUser1", "TestUser2" };
            var conversationId = history.StartOrGetConversation(testParticipants);
            
            if (!string.IsNullOrEmpty(conversationId))
            {
                Log.Message($"[诊断] HistoryService 功能正常，测试对话ID: {conversationId}");
            }
            else
            {
                Log.Warning("[诊断] HistoryService 无法创建对话");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[诊断] HistoryService 错误: {ex.Message}");
        }
    }
}
```

### 2. 性能监控

```csharp
public static class PerformanceMonitor
{
    private static readonly Dictionary<string, List<long>> _timings = new Dictionary<string, List<long>>();

    public static async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> func)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            return await func();
        }
        finally
        {
            stopwatch.Stop();
            RecordTiming(operation, stopwatch.ElapsedMilliseconds);
        }
    }

    private static void RecordTiming(string operation, long milliseconds)
    {
        if (!_timings.ContainsKey(operation))
            _timings[operation] = new List<long>();

        _timings[operation].Add(milliseconds);

        // 每10次操作报告一次平均性能
        if (_timings[operation].Count % 10 == 0)
        {
            var avg = _timings[operation].Average();
            var max = _timings[operation].Max();
            var min = _timings[operation].Min();
            
            Log.Message($"[性能] {operation} - 平均: {avg:F1}ms, 最大: {max}ms, 最小: {min}ms");
        }
    }

    public static void LogPerformanceReport()
    {
        Log.Message("=== 性能报告 ===");
        foreach (var (operation, timings) in _timings)
        {
            if (timings.Count > 0)
            {
                var avg = timings.Average();
                var total = timings.Count;
                Log.Message($"{operation}: {total}次调用, 平均{avg:F1}ms");
            }
        }
    }
}

// 使用示例
public async Task<string> MonitoredAICall(string query)
{
    return await PerformanceMonitor.MeasureAsync("AI调用", async () =>
    {
        return await CoreServices.Governor.HandleUserQueryAsync(query);
    });
}
```

## 📋 最佳实践总结

### ✅ 应该做的
- 始终通过 `CoreServices` 门面访问服务
- 在使用AI功能前检查 `CoreServices.AreServicesReady()`
- 使用 `PromptBuildConfig` 构建结构化提示词
- 实现 `IPersistable` 接口来持久化重要数据
- 使用 `PlayerStableId` 进行数据关联，`PlayerDisplayName` 进行UI显示
- 在异步方法中正确处理异常和取消令牌
- 使用 `SafeAccessService` 安全访问RimWorld API

### ❌ 避免做的
- 使用已废弃的 `.Instance` 静态属性
- 直接拼接字符串构建提示词
- 在UI线程中进行同步的AI调用
- 忽略服务就绪状态检查
- 直接访问RimWorld集合而不使用SafeAccessService
- 混用稳定ID和显示名称

### 🛡️ 错误处理原则
- 每个AI调用都应该包装在try-catch中
- 为用户提供有意义的错误信息
- 记录详细的错误日志用于调试
- 在服务不可用时提供降级方案

---

*🚀 遵循这个开发指南，您将能够充分利用RimAI的新架构，创建强大、可靠的AI功能模块！*
