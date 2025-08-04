using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Architecture.Caching
{
    /// <summary>
    /// 为统一聊天请求生成稳定的缓存键。
    /// </summary>
    public static class CacheKeyUtil
    {
        /// <summary>
        /// 生成基于 <see cref="UnifiedChatRequest"/> 的 SHA256 键。
        /// 规则：对请求进行标准化 JSON 序列化，再追加模型名（若有），最后计算 SHA256。
        /// </summary>
        public static string GenerateChatRequestKey(UnifiedChatRequest request)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Formatting = Formatting.None
            };
            var serialized = JsonConvert.SerializeObject(request, settings);
            var bytes = Encoding.UTF8.GetBytes(serialized);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder();
            foreach (var b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
    }
}