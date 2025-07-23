using System.Collections.Generic;

namespace RimAI.Core.Architecture.Models
{
    /// <summary>
    /// 定义了构建一个提示词所需要的所有输入信息。
    /// </summary>
    public class PromptBuildConfig
    {
        /// <summary>
        /// 用于从HistoryService获取上下文的当前对话参与者。
        /// </summary>
        public List<string> CurrentParticipants { get; set; }

        /// <summary>
        /// 要使用的提示词模板ID
        /// </summary>
        public string SystemPromptTemplateId { get; set; }
        
        /// <summary>
        /// 用于填充模板的上下文数据字典
        /// </summary>
        public Dictionary<string, object> TemplateContext { get; set; }
        
        /// <summary>
        /// 系统提示词，定义AI的角色和行为准则。
        /// 如果提供了 TemplateId，此项将被忽略。
        /// </summary>
        public string SystemPrompt { get; set; }

        /// <summary>
        /// 场景提示词，描述当前环境。
        /// </summary>
        public SceneContext Scene { get; set; }

        /// <summary>
        /// 其他附加游戏数据。
        /// </summary>
        public AncillaryData OtherData { get; set; }

        /// <summary>
        /// 历史记录获取上限。
        /// </summary>
        public int HistoryLimit { get; set; } = 10;
    }

    /// <summary>
    /// 场景上下文，描述对话发生时的具体环境。
    /// </summary>
    public class SceneContext
    {
        public string Scenario { get; set; } // 舞台/剧本
        public string Time { get; set; }
        public string Location { get; set; }
        public List<string> Participants { get; set; } // 谁在场
        public string Situation { get; set; } // 正在发生什么
    }

    /// <summary>
    /// 其他附加游戏数据。
    /// </summary>
    public class AncillaryData
    {
        public string Weather { get; set; }
        public string ReferenceInfo { get; set; } // 额外资料
    }

    /// <summary>
    /// 定义了最终输出的、LLM友好的格式，与OpenAI的API格式兼容。
    /// </summary>
    public class PromptPayload
    {
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    /// <summary>
    /// 表示一条单独的聊天消息，用于构建发送给LLM的负载。
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } // "system", "user", "assistant"
        public string Content { get; set; }
        public string Name { get; set; } // 可选，用于标记发言者
    }
} 