using System.Threading.Tasks;
using RimAI.Core.Architecture.Models;

namespace RimAI.Core.Architecture.Interfaces
{
    public interface IPromptFactoryService
    {
        /// <summary>
        /// 根据结构化配置，异步构建一个完整的、可直接发送给LLM的提示词负载。
        /// </summary>
        /// <param name="config">结构化的提示词构建配置。</param>
        /// <returns>一个结构化的消息列表，类似于OpenAI的格式。</returns>
        Task<PromptPayload> BuildStructuredPromptAsync(PromptBuildConfig config);
    }
} 