using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Infrastructure.Cache;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;
using Verse;

namespace RimAI.Core.Modules.Embedding
{
    /// <summary>
    /// S2 改造：调用 RimAI.Framework 的 Embedding API，不重复实现 Provider 选择/并发/翻译等逻辑。
    /// 仅在 Core 层提供轻量缓存与健壮性封装。
    /// </summary>
    internal sealed class EmbeddingService : IEmbeddingService
    {
        private readonly ICacheService _cache;
        private readonly IConfigurationService _config;

        public EmbeddingService(ICacheService cache, IConfigurationService config)
        {
            _cache = cache;
            _config = config;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            text ??= string.Empty;
            var key = $"embed:sha256:{ComputeSha256(text)}";
            if (_cache.TryGet(key, out float[] cached) && cached != null)
            {
                return cached;
            }

            var req = new UnifiedEmbeddingRequest { Inputs = new List<string> { text } };
            var res = await RimAIApi.GetEmbeddingsAsync(req, CancellationToken.None);
            if (!res.IsSuccess)
                throw new Exception($"Embedding Error: {res.Error}");

            var data = res.Value?.Data;
            if (data == null || data.Count == 0 || data[0]?.Embedding == null)
                throw new Exception("Embedding response is empty.");

            var vec = data[0].Embedding.ToArray();
            _cache.Set(key, vec, TimeSpan.FromMinutes(Math.Max(1, _config.Current.Embedding.CacheMinutes)));
            return vec;
        }

        public Task<bool> IsAvailableAsync()
        {
            // 轻量可用性检查：读取 RimAI Framework 的设置，确保已配置提供商，避免不必要的请求。
            try
            {
                var mod = LoadedModManager.GetMod<RimAI.Framework.UI.RimAIFrameworkMod>();
                var settings = mod?.GetSettings<RimAI.Framework.UI.RimAIFrameworkSettings>();
                if (settings == null) return Task.FromResult(false);
                var providerId = string.IsNullOrEmpty(settings.ActiveEmbeddingProviderId)
                    ? settings.ActiveChatProviderId
                    : settings.ActiveEmbeddingProviderId;
                return Task.FromResult(!string.IsNullOrEmpty(providerId));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private static string ComputeSha256(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}


