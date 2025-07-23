# 🏗️ RimAI 架构设计文档

*深入理解RimAI的企业级架构设计*

## 📐 设计原则

### 核心理念
- **依赖注入**: 通过ServiceContainer管理所有依赖关系
- **事件驱动**: 使用EventBus实现组件间解耦通信
- **分层架构**: 清晰的职责分离和层次结构
- **异步优先**: 所有I/O操作采用async/await模式
- **可扩展性**: 支持插件化的AI官员扩展

### SOLID原则应用
- **S** - 单一职责: 每个服务专注特定功能
- **O** - 开放封闭: 支持扩展新官员，无需修改核心
- **L** - 里氏替换: 所有官员都实现IAIOfficer接口
- **I** - 接口隔离: 细粒度的服务接口设计
- **D** - 依赖倒置: 依赖接口而非具体实现

## 🏛️ 核心架构组件

### 1. ServiceContainer (依赖注入容器)
```
ServiceContainer
├── 服务注册管理
├── 实例生命周期控制
├── 依赖关系解析
└── CoreServices静态访问门面
```

**职责**:
- 管理所有服务的注册和获取
- 提供统一的服务访问点
- 控制服务的生命周期
- 支持接口和具体类型双重注册

**设计价值**:
- **类型安全**: 编译时类型检查，避免运行时错误
- **生命周期管理**: 统一控制对象创建和销毁
- **测试友好**: 便于Mock和单元测试
- **配置集中**: 所有依赖关系在一处定义

### 2. CoreServices (统一服务门面)
```
CoreServices (静态门面)
├── AI服务访问 (Governor, LLMService, Analyzer)
├── 对话系统 (History, PromptFactory)
├── 基础架构 (Cache, EventBus, Persistence, SafeAccess)
├── 玩家身份管理 (PlayerStableId, PlayerDisplayName)
└── 系统状态监控 (AreServicesReady, GetStatusReport)
```

**设计思想**:
- **单一入口**: 所有服务访问的统一门面
- **类型安全**: 强类型的服务获取
- **空值安全**: 内置空值检查和保护
- **调试友好**: 提供服务状态诊断

**使用模式**:
```csharp
// ✅ 推荐：通过CoreServices门面访问
var governor = CoreServices.Governor;
var history = CoreServices.History;

// ❌ 废弃：直接使用单例模式
// var governor = Governor.Instance; // 不再支持

// 🛡️ 安全检查
if (CoreServices.AreServicesReady())
{
    // 安全使用服务
}
```

### 3. 玩家身份系统设计

**双重身份标识的设计思想**:
```
玩家身份系统
├── PlayerStableId (稳定标识)
│   ├── 来源: Faction.OfPlayer.GetUniqueLoadID()
│   ├── 特性: 永不改变，基于存档生成
│   └── 用途: 数据关联、历史记录、系统内部使用
└── PlayerDisplayName (显示名称)
    ├── 来源: SettingsManager.Settings.Player.Nickname
    ├── 特性: 用户可修改，存储在设置中
    └── 用途: UI显示、AI对话称呼、用户交互
```

**设计价值**:
- **数据一致性**: StableId确保历史数据不会因昵称更改而丢失
- **用户体验**: DisplayName让用户可以自定义在AI对话中的称呼
- **系统稳定性**: 内部数据关联使用不变的StableId
- **隐私保护**: 用户昵称与系统ID分离

**应用示例**:
```csharp
// 对话历史关联 - 使用稳定ID
var conversationId = CoreServices.History.StartOrGetConversation(
    new List<string> { CoreServices.PlayerStableId, "Governor" }
);

// AI对话显示 - 使用显示名称
var greeting = $"你好，{CoreServices.PlayerDisplayName}！";

// 数据持久化 - 使用稳定ID
var userData = await LoadUserData(CoreServices.PlayerStableId);
```

### 4. HistoryService (对话历史服务)
```
HistoryService 架构
├── 主数据存储 (_conversationStore)
│   ├── 结构: Dictionary<ConversationId, List<ConversationEntry>>
│   ├── 职责: 存储完整对话内容
│   └── 特性: 按对话ID组织，支持随存档持久化
├── 倒排参与者索引 (_participantIndex)
│   ├── 结构: Dictionary<ParticipantId, HashSet<ConversationId>>
│   ├── 职责: 快速查找参与者相关对话
│   └── 特性: 支持复杂关联查询和交集运算
└── 智能历史检索
    ├── 主线历史: 当前参与者的直接对话
    ├── 附加历史: 包含当前参与者的多方对话
    └── 时间排序: 按游戏时间戳精确排序
```

**设计创新**:
- **主存储+倒排索引**: 兼顾存储效率和查询性能
- **多维度历史**: 区分主线对话和背景参考
- **游戏时间戳**: 精确到游戏Tick的时间记录
- **自动持久化**: 无缝集成RimWorld存档系统

**检索算法**:
```
输入: 参与者列表 [A, B]
1. 生成主对话ID: "A_B"
2. 查询主线历史: _conversationStore["A_B"]
3. 查找相关对话: 
   - 获取A参与的所有对话: _participantIndex[A]
   - 获取B参与的所有对话: _participantIndex[B]
   - 计算交集: 同时包含A和B的对话
4. 构建分层上下文:
   - PrimaryHistory: 主对话记录
   - AncillaryHistory: 其他相关对话记录
输出: HistoricalContext (结构化历史上下文)
```

### 5. PromptFactoryService (提示词工厂服务)
```
PromptFactoryService 架构
├── 历史消费层
│   ├── 调用: HistoryService.GetHistoricalContextFor()
│   ├── 处理: 分层历史数据 (Primary + Ancillary)
│   └── 转换: 游戏时间戳 → 可读格式
├── 上下文组装层
│   ├── 系统提示词: AI角色和行为定义
│   ├── 场景上下文: 时间、地点、情况描述
│   ├── 附加数据: 天气、资源、参考信息
│   └── 历史对话: 格式化的对话记录
└── 结构化输出层
    ├── 格式: OpenAI兼容的ChatMessage列表
    ├── 角色映射: ParticipantId → Role标签
    ├── 时间标注: [时间: 2503年春季第5天, 13时]
    └── 分层标识: [背景参考资料] 标记
```

**设计价值**:
- **智能组装**: 自动处理复杂的上下文构建
- **格式标准化**: 输出标准的LLM API格式
- **时间感知**: 将游戏时间转换为AI可理解的格式
- **上下文分层**: 区分主要对话和背景信息

**组装流程**:
```
PromptBuildConfig 输入
    ↓
历史检索 (HistoryService)
    ↓
时间戳格式化 (GenDate工具)
    ↓
上下文分层组装:
├── SystemPrompt → ChatMessage (role: "system")
├── AncillaryHistory → ChatMessage (role: "system", content: "[背景参考资料]...")
├── PrimaryHistory → ChatMessage[] (role: "user"/"assistant")
├── SceneContext → ChatMessage (role: "system")
└── OtherData → ChatMessage (role: "system")
    ↓
PromptPayload 输出 (OpenAI格式)
```

### 6. EventBus (事件总线系统)
```
EventBusService
├── 事件发布 (PublishAsync)
├── 事件订阅 (Subscribe)
├── 类型安全的事件处理
└── 异步事件分发
```

**职责**:
- 实现发布-订阅模式
- 提供组件间解耦通信
- 支持异步事件处理
- 类型安全的事件系统

**事件流程**:
```
Component A → EventBus.PublishAsync(Event) → EventBus → Listeners → Component B/C/D
```

### 7. AI官员架构
```
IAIOfficer (接口)
└── OfficerBase (抽象基类)
    └── Governor (具体实现)
        └── 其他官员扩展...
```

**继承层次**:
- **IAIOfficer**: 定义官员基本契约
- **OfficerBase**: 提供通用功能实现
- **Governor**: 总督的具体实现
- **自定义官员**: 继承OfficerBase扩展

### 8. 服务层架构 (更新版)
```
Services Layer
├── 🧠 对话智能服务
│   ├── HistoryService      # 对话历史管理
│   ├── PromptFactoryService # 结构化提示词组装
│   └── LLMService          # AI模型调用
├── 🏗️ 基础架构服务
│   ├── CacheService        # 智能缓存
│   ├── EventBusService     # 事件通信
│   ├── PersistenceService  # 持久化存储
│   └── SafeAccessService   # RimWorld API安全访问
├── 📊 业务逻辑服务
│   └── ColonyAnalyzer      # 殖民地状态分析
└── 🎛️ 控制和管理
    └── ServiceContainer    # 依赖注入容器
```

### 9. SafeAccessService (RimWorld API安全访问层)
```
SafeAccessService
├── 并发安全访问 (重试机制)
├── 空值安全检查
├── 异常恢复处理
├── 性能监控统计
└── 统一错误处理
```

**职责**:
- 解决RimWorld API的并发修改异常问题
- 提供统一的重试机制和错误恢复
- 统计API访问失败率，便于监控和调试
- 为所有组件提供安全的RimWorld数据访问

**设计模式应用**:
- **门面模式**: 为复杂的RimWorld API提供简化接口
- **重试模式**: 自动处理临时性失败
- **空对象模式**: 返回安全的默认值而非null
- **监控模式**: 记录访问统计便于问题诊断

## 🔄 数据流设计

### 完整的AI对话处理流程
```
用户输入 → UI层 → CoreServices → Governor
    ↓
PromptFactoryService ← HistoryService (获取对话历史)
    ↓
PromptBuildConfig → PromptPayload (结构化提示词)
    ↓
LLMService → Framework API (AI调用)
    ↓
AI响应 → HistoryService (记录新对话)
    ↓
CacheService (缓存结果) → EventBus (发布事件)
    ↓
UI更新 (显示AI回复)
```

**关键数据流说明**:

1. **历史检索**: PromptFactoryService调用HistoryService获取结构化历史
2. **智能组装**: 将历史、上下文、场景组装成标准LLM格式
3. **AI交互**: LLMService处理实际的AI模型调用
4. **历史更新**: 将用户输入和AI回复都记录到HistoryService
5. **缓存优化**: 重复查询可以直接从缓存获取结果
6. **事件通知**: 整个过程的关键节点都发布事件

### SafeAccessService 在数据流中的作用
```
Services Layer → SafeAccessService → RimWorld API
    ↓                    ↓                 ↓
自动重试机制    →    统计监控    →    安全的数据访问
```

**数据安全保障**:
- 作为所有RimWorld数据访问的统一入口
- 在服务层和RimWorld API之间提供安全缓冲
- 确保数据流的稳定性和可预测性

### 错误处理流程
```
异常发生 → SafeAccessService重试 → 日志记录 → 用户友好提示 → 降级处理 → 系统恢复
```

## 🎯 关键设计决策

### 1. 为什么从静态单例迁移到依赖注入？

**原有问题**:
```csharp
// ❌ 旧架构：静态单例模式
public class Governor
{
    public static Governor Instance => _instance ??= new Governor();
    
    // 问题：
    // 1. 难以单元测试（无法Mock）
    // 2. 生命周期难以控制
    // 3. 依赖关系隐式且复杂
    // 4. 违反依赖倒置原则
}
```

**新架构优势**:
```csharp
// ✅ 新架构：依赖注入
public class Governor : OfficerBase
{
    // 通过ServiceContainer管理
    // 通过CoreServices访问
    
    // 优势：
    // 1. 便于测试和Mock
    // 2. 生命周期可控
    // 3. 依赖关系显式
    // 4. 符合SOLID原则
}
```

### 2. 为什么设计双重玩家身份系统？

**设计动机**:
- **数据一致性需求**: 对话历史不能因用户改昵称而丢失
- **用户体验需求**: 用户希望AI用自定义昵称称呼自己
- **系统稳定性需求**: 内部关联需要不变的标识符

**技术实现**:
```csharp
// 稳定标识：用于数据关联
public static string PlayerStableId => Faction.OfPlayer.GetUniqueLoadID();

// 显示名称：用于UI交互
public static string PlayerDisplayName => SettingsManager.Settings.Player.Nickname;
```

### 3. 为什么采用"主存储+倒排索引"的历史架构？

**性能考虑**:
- **查询效率**: 倒排索引支持O(1)的参与者查找
- **存储效率**: 主存储避免数据重复
- **复杂查询**: 支持交集运算和多维度检索

**扩展性考虑**:
- **多方对话**: 支持2人以上的群组对话
- **历史关联**: 一次查询获取所有相关对话
- **时间排序**: 精确的游戏时间戳排序

### 4. 为什么使用结构化提示词组装？

**传统问题**:
```csharp
// ❌ 旧方式：字符串拼接
var prompt = $"你是总督。当前状况：{status}。历史：{history}。请回复：{query}";
```

**新架构优势**:
```csharp
// ✅ 新方式：结构化组装
var config = new PromptBuildConfig
{
    CurrentParticipants = participants,
    SystemPrompt = systemPrompt,
    Scene = sceneContext,
    HistoryLimit = 10
};
var payload = await PromptFactory.BuildStructuredPromptAsync(config);
```

**技术价值**:
- **可维护性**: 结构化配置更易于维护
- **可扩展性**: 新的上下文类型易于添加
- **标准化**: 输出标准的LLM API格式
- **智能处理**: 自动处理时间戳转换和格式化

## 📊 性能设计

### 缓存策略
```
L1: 内存缓存 (CacheService)
├── 对话历史: 10-15分钟过期
├── AI结果: 5-10分钟过期  
├── 分析数据: 1-3分钟过期
└── 提示词组装: 2-5分钟过期
```

### 异步优化
- **并发控制**: CancellationToken支持
- **超时处理**: 防止长时间阻塞
- **资源管理**: 使用using管理生命周期
- **错误隔离**: 异常不影响其他操作

## 🔌 扩展点设计

### 1. AI官员扩展
```csharp
public class MedicalOfficer : OfficerBase
{
    public override OfficerRole Role => OfficerRole.Medical;
    
    protected override async Task<string> ExecuteAdviceRequest(CancellationToken token)
    {
        // 使用新架构服务
        var config = new PromptBuildConfig
        {
            CurrentParticipants = new List<string> { CoreServices.PlayerStableId, "MedicalOfficer" },
            SystemPrompt = "你是专业的医疗官...",
            Scene = await BuildMedicalContext(),
            HistoryLimit = 15
        };
        
        var payload = await CoreServices.PromptFactory.BuildStructuredPromptAsync(config);
        var medicalData = await GetMedicalDataAsync();
        payload.Messages.Add(new ChatMessage 
        { 
            Role = "user", 
            Content = $"当前医疗状况：{medicalData}" 
        });
        
        return await CoreServices.LLMService.SendMessageAsync(ConvertToPromptText(payload), token);
    }
}
```

### 2. 服务扩展
```csharp
public interface ICustomService
{
    Task<string> CustomOperationAsync();
}

// 注册到ServiceContainer
ServiceContainer.RegisterService<ICustomService>(new CustomService());

// 在CoreServices中添加访问器
public static ICustomService Custom => ServiceContainer.Instance.GetService<ICustomService>();
```

### 3. 事件扩展
```csharp
public class CustomEvent : IEvent
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; } = DateTime.Now;
    public string EventType => "Custom";
    
    // 自定义属性...
}

// 发布自定义事件
await CoreServices.EventBus.PublishAsync(new CustomEvent());
```

## 🛡️ 安全设计

### 1. 输入验证
- 用户输入清理和验证
- SQL注入防护
- XSS攻击防护

### 2. 错误处理
- 敏感信息不暴露
- 统一的错误响应格式
- 完整的错误日志记录

### 3. 资源保护
- 内存使用限制
- 请求频率限制
- 超时机制保护

## 📈 监控和诊断

### 日志系统
```
[ServiceContainer] 服务注册和获取日志
[CoreServices] 服务访问和状态检查日志
[HistoryService] 对话存储和检索日志
[PromptFactory] 提示词组装和优化日志
[EventBus] 事件发布和处理日志
[CacheService] 缓存命中和清理日志
[SafeAccessService] RimWorld API访问和重试日志
[Governor] AI请求和响应日志
[LLMService] 外部API调用日志
```

### 性能指标
- 服务响应时间
- 对话历史检索效率
- 提示词组装时间
- 缓存命中率
- 事件处理延迟
- RimWorld API访问成功率
- 内存使用情况
- 错误发生频率

## 🔮 架构演进

### 当前版本 (v2.0) - 企业级架构
- ✅ 依赖注入架构迁移
- ✅ CoreServices统一门面
- ✅ HistoryService对话历史系统
- ✅ PromptFactoryService结构化提示词
- ✅ 双重玩家身份系统
- ✅ SafeAccessService安全访问层
- ✅ 完整的事件总线系统

### 未来计划 (v3.0) - 智能化生态
- 🎯 多AI官员生态完善
- 🎯 智能插件系统
- 🎯 分布式部署支持
- 🎯 高级分析引擎
- 🎯 机器学习集成
- 🎯 跨存档智能分析

## 💡 最佳实践

### 1. 服务设计
- 保持接口简洁明确
- 优先异步设计
- 支持取消操作
- 提供完整日志

### 2. 历史服务使用
- 使用`PlayerStableId`进行数据关联
- 合理设置`HistoryLimit`避免性能问题
- 充分利用分层历史（Primary + Ancillary）
- 在构造函数中注册`IPersistable`

### 3. 提示词组装
- 使用`PromptBuildConfig`而非字符串拼接
- 合理构建`SceneContext`提供丰富上下文
- 利用历史检索增强对话连贯性
- 关注时间戳格式化的用户体验

### 4. 错误处理
- 使用try-catch包装关键操作
- 提供用户友好的错误信息
- 记录详细的错误上下文
- 实现优雅降级

### 5. 性能优化
- 合理使用缓存
- 避免阻塞操作
- 及时释放资源
- 监控关键指标

---
*🏗️ 这套企业级架构为RimAI提供了坚实的技术基础，支持复杂的AI对话系统和未来的功能扩展*
