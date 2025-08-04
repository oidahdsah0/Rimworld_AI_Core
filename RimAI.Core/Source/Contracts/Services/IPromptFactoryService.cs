using System.Threading.Tasks;
using System.Collections.Generic;
using RimAI.Framework.Contracts;

#nullable enable

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 负责根据用户输入、系统 Persona 提示与动态上下文组装 UnifiedChatRequest。
    /// </summary>
    public interface IPromptFactoryService
    {
        /// <summary>
        /// 构建完整的聊天请求。
        /// </summary>
        /// <param name="userInput">用户原始文本</param>
        /// <param name="personaSystemPrompt">Persona 提供的系统提示</param>
        Task<UnifiedChatRequest> BuildPromptAsync(string userInput, string personaSystemPrompt, IEnumerable<string>? participants = null);
    }
}