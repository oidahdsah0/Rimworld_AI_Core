# 🧠 AI 对话服务设计文档

本文档详细阐述了 RimAI 框架中两个核心对话服务的增强设计：**历史对话服务 (`HistoryService`)** 和 **提示词工厂服务 (`PromptFactoryService`)**。该设计旨在支持复杂、多层次、有记忆的 AI 对话场景。

---

## 1. 核心设计需求

该设计方案旨在满足以下核心业务需求：

1.  **多路对话支持**: 系统必须同时管理多个独立的对话，包括：
    *   **1v1 对话**: 玩家 vs. NPC, NPC vs. NPC。
    *   **多v多对话**: 多个实体在同一场对话中交互。
2.  **上下文关联检索**: 在获取对话历史时，必须能揪出所有包含当前对话者的相关对话。例如，A和B在1v1对话时，系统需要获取他们二人之间纯粹的1v1历史，以及所有包含他们二人的多人对话历史（如A、B、C的群聊）。
3.  **历史主次分层**: 检索出的历史记录必须能够被区分为：
    *   **主线历史 (Primary History)**: 当前直接参与者之间的对话。
    *   **附加历史 (Ancillary History)**: 包含当前参与者，但也有其他人在场的对话，作为背景参考。
4.  **跨对话访问**: 允许一个独立的对话场景（如法庭）访问并引用另一场不相关的对话历史（如犯罪嫌疑人的历史供词），作为背景资料。
5.  **游戏时间戳**: 所有的对话记录都必须带有精确到游戏刻度（Tick）的时间戳，以确保与游戏世界进程的同步。
6.  **持久化存储**: 所有对话历史都必须能随游戏存档一起保存和加载。

---

## 2. 历史对话服务 (`HistoryService`) 设计

`HistoryService` 的职责是存储、索引和检索所有对话历史。它是一个小型的对话搜索引擎。

### 2.1. 核心架构

为满足复杂的检索需求，`HistoryService` 采用“**主存储 + 倒排索引**”的双重数据结构。

1.  **主数据存储 (`_conversationStore`)**: 一个字典，用于存储所有对话的完整内容。
    *   **结构**: `Dictionary<string, List<ConversationEntry>>`
    *   **Key**: `ConversationId`，每场对话的唯一标识符。
    *   **Value**: 该对话的所有聊天记录列表。

2.  **倒排参与者索引 (`_participantIndex`)**: 一个字典，用于快速查找每个参与者参与过的所有对话。这是实现高效关联检索的核心。
    *   **结构**: `Dictionary<string, HashSet<string>>`
    *   **Key**: `ParticipantId`，每个参与者的唯一ID。
    *   **Value**: 一个集合，包含该参与者参与过的所有 `ConversationId`。

### 2.2. 数据模型

#### `ConversationEntry`
表示单条对话记录。
```csharp
public class ConversationEntry : IExposable
{
    // 发言者的唯一ID
    public string ParticipantId;
    // 发言者的角色标签 (e.g., "user", "assistant", "character")
    public string Role;
    // 发言内容
    public string Content;
    // 游戏内时间戳 (Ticks)
    public long GameTicksTimestamp;

    public void ExposeData()
    {
        Scribe_Values.Look(ref ParticipantId, "participantId");
        Scribe_Values.Look(ref Role, "role");
        Scribe_Values.Look(ref Content, "content");
        Scribe_Values.Look(ref GameTicksTimestamp, "gameTicksTimestamp", 0);
    }
}
```

#### `HistoricalContext`
结构化的历史上下文，用于返回给调用者，已预先分好主次。
```csharp
public class HistoricalContext
{
    /// <summary>
    /// 主线历史：当前对话者之间的直接对话记录。
    /// </summary>
    public List<ConversationEntry> PrimaryHistory { get; set; }

    /// <summary>
    /// 附加历史：包含了当前对话者，但也有其他人在场的对话记录。
    /// </summary>
    public List<ConversationEntry> AncillaryHistory { get; set; }
}
```

### 2.3. 接口定义 (`IHistoryService`)

```csharp
public interface IHistoryService : IPersistable
{
    /// <summary>
    /// 为一组参与者开始或获取一个对话ID。
    /// 如果是新对话，会自动创建并更新索引。
    /// </summary>
    /// <param name="participantIds">参与对话的所有实体ID列表。</param>
    /// <returns>这场对话的唯一ID (ConversationId)。</returns>
    string StartOrGetConversation(List<string> participantIds);

    /// <summary>
    /// 向指定的对话中添加一条记录。
    /// </summary>
    /// <param name="conversationId">对话ID。</param>
    /// <param name="entry">包含游戏时间戳的对话条目。</param>
    void AddEntry(string conversationId, ConversationEntry entry);

    /// <summary>
    /// 获取一个结构化的历史上下文，区分主线对话和附加参考对话。
    /// </summary>
    /// <param name="primaryParticipants">当前对话的直接参与者ID列表。</param>
    /// <param name="limit">每个历史列表的记录条数上限。</param>
    /// <returns>一个包含主次历史的结构化对象。</returns>
    HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10);
}
```

### 2.4. 核心逻辑

#### 索引机制
`ConversationId` 通过对参与者ID列表进行**排序**和**拼接**生成，确保其唯一性和稳定性。当新对话创建时，会同步更新 `_participantIndex`，将新的 `ConversationId` 添加到每个参与者的条目下。

#### 检索逻辑 (`GetHistoricalContextFor`)
1.  接收当前对话的参与者列表，如 `["A", "B"]`。
2.  **确定主线历史**: 生成精确匹配的ID（如`"A_B"`），从 `_conversationStore` 中获取其对话记录，作为 `PrimaryHistory`。
3.  **查找所有相关对话**:
    *   从 `_participantIndex` 分别获取A和B参与的所有对话ID集合。
    *   对这些集合**求交集**，得到所有同时包含A和B的对话ID（如 `{"A_B", "A_B_C"}`）。
4.  **筛选并合并附加历史**:
    *   从交集结果中移除主线ID (`"A_B"`)。
    *   用剩下的ID（`"A_B_C"`）去 `_conversationStore` 中取出所有记录。
    *   将这些记录合并，并按 `GameTicksTimestamp` 排序，作为 `AncillaryHistory`。
5.  返回填充好的 `HistoricalContext` 对象。

### 2.5. 持久化
该服务必须实现 `IPersistable` 接口。在 `ExposeData()` 方法中，**`_conversationStore` 和 `_participantIndex` 都必须使用 `Scribe` 系统进行读写**，以确保所有历史和索引都能随存档保存和加载。

---

## 3. 提示词工厂服务 (`PromptFactoryService`) 设计

`PromptFactoryService` 的职责是消费 `HistoryService` 提供的结构化历史，并结合其他上下文，智能地组装成一个可直接发送给大型语言模型（LLM）的、结构化的提示词负载。

### 3.1. 数据模型

#### `PromptBuildConfig`
定义了构建一个提示词所需要的所有输入信息。
```csharp
public class PromptBuildConfig
{
    // 用于从HistoryService获取上下文
    public List<string> CurrentParticipants { get; set; }
    // 系统提示词，定义AI的角色和行为准则
    public string SystemPrompt { get; set; }
    // 场景提示词
    public SceneContext Scene { get; set; }
    // 其他附加游戏数据
    public AncillaryData OtherData { get; set; }
    // 历史记录获取上限
    public int HistoryLimit { get; set; } = 10;
}

public class SceneContext { /* 时间、地点、人物、事件... */ }
public class AncillaryData { /* 天气、参考资料... */ }
```

#### `PromptPayload` 和 `ChatMessage`
定义了最终输出的、LLM友好的格式，与OpenAI的API格式兼容。
```csharp
public class PromptPayload
{
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public string Role { get; set; } // "system", "user", "assistant"
    public string Content { get; set; }
    public string Name { get; set; } // 可选，用于标记发言者
}
```

### 3.2. 接口定义 (`IPromptFactoryService`)

```csharp
public interface IPromptFactoryService
{
    /// <summary>
    /// 根据结构化配置，异步构建一个完整的、可直接发送给LLM的提示词负载。
    /// </summary>
    /// <param name="config">结构化的提示词构建配置。</param>
    /// <returns>一个结构化的消息列表，类似于OpenAI的格式。</returns>
    Task<PromptPayload> BuildStructuredPromptAsync(PromptBuildConfig config);
}
```

### 3.3. 核心逻辑 (智能组装)

当 `BuildStructuredPromptAsync` 被调用时，其内部工作流如下：
1.  **获取结构化历史**: 使用 `config.CurrentParticipants` 调用 `historyService.GetHistoricalContextFor()`，得到 `HistoricalContext` 对象。
2.  **初始化Payload**: 创建一个 `PromptPayload`，并首先添加 `SystemPrompt` 对应的 `ChatMessage` (`Role: "system"`)。
3.  **时间戳格式化**: **(关键步骤)** `PromptFactoryService` 的一项核心职责是调用游戏引擎的工具类（如`GenDate`），将从 `ConversationEntry` 中获取的 `long` 类型 `GameTicksTimestamp` 转换为人类和AI可读的字符串（例如：`[时间: 2503年春季第5天, 13时]`）。
4.  **组装附加历史**:
    *   将 `historicalContext.AncillaryHistory` 格式化为一段单一、易于理解的文本摘要。在格式化时，必须使用上述转换后的可读时间戳。
    *   这段摘要将作为一条特殊的 `ChatMessage`（例如 `Role: "user"` 或 `system`，内容前缀为 `[背景参考资料]`）插入到 `Messages` 列表的靠前位置。
5.  **组装主线历史**:
    *   遍历 `historicalContext.PrimaryHistory` 中的 `ConversationEntry`。
    *   将每条记录转换为对应的 `ChatMessage`，并在内容前附加上格式化好的可读时间戳。
    *   将这些 `ChatMessage` 添加到 `Messages` 列表中。
6.  **组装场景和其他数据**:
    *   将 `config.Scene` 和 `config.OtherData` 的信息格式化。
    *   可以作为一条额外的 `ChatMessage` 插入，也可以融入 `SystemPrompt`。推荐作为独立的上下文消息。
7.  **返回负载**: 返回最终组装好的 `PromptPayload` 对象。

---

## 4. 端到端协同工作流

1.  **对话触发**: 玩家与NPC "Zorg" 开始1v1对话。
2.  **获取历史**: 游戏逻辑请求 `PromptFactory` 构建提示词，传入参与者 `["player_id", "zorg_id"]`。
3.  **智能检索**: `PromptFactory` 调用 `HistoryService`，后者通过倒排索引和主存储，返回一个 `HistoricalContext` 对象，其中包含了玩家与Zorg的1v1主线历史，以及他们共同参与过的多人对话（附加历史）。
4.  **智能组装**: `PromptFactory` 将附加历史格式化为背景资料（包含可读时间戳），将主线历史作为核心对话流（每条记录都附加可读时间戳），再结合系统、场景等提示词，组装成一个 `PromptPayload`。
5.  **AI调用**: `PromptPayload` 被发送给 `LLMService`。
6.  **记录更新**: 获得AI（Zorg）的回复后，游戏逻辑调用 `historyService.StartOrGetConversation` 获取 `ConversationId`，然后调用 `historyService.AddEntry`，将带有游戏时间戳（`long`类型）的新回复存入主线历史中。

这个闭环确保了AI既能专注于当前对话，又能参考所有相关的过去经验，从而实现真正有深度、有记忆的互动。 