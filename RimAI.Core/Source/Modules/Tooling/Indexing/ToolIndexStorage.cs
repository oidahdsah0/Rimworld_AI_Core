using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.Persistence;

namespace RimAI.Core.Source.Modules.Tooling.Indexing
{
	internal sealed class ToolIndexStorage
	{
		private readonly IPersistenceService _persistence;

		public ToolIndexStorage(IPersistenceService persistence)
		{
			_persistence = persistence;
		}

		public static string ComputeFingerprintHash(string provider, string model, int dimension, string instruction)
		{
			var raw = $"{provider}|{model}|{dimension.ToString(CultureInfo.InvariantCulture)}|{instruction ?? string.Empty}";
			using var sha = SHA256.Create();
			var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
			// .NET Framework 4.7.2 没有 Convert.ToHexString
			var hex = BitConverter.ToString(bytes).Replace("-", string.Empty);
			return hex;
		}

		public async Task SaveAsync(string provider, string model, ToolIndexSnapshot snapshot, string basePath, string fileNameFormat, CancellationToken ct)
		{
			var relativeDir = basePath?.TrimEnd('/', '\\') ?? "Config/RimAI/Indices";
			var fileName = (fileNameFormat ?? "tools_index_{provider}_{model}.json")
				.Replace("{provider}", provider)
				.Replace("{model}", model);
			var relativePath = $"{relativeDir}/{fileName}".Replace('\\', '/');
			var json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
			await _persistence.WriteTextUnderConfigAsync(relativePath, json, ct);
		}

		public async Task<ToolIndexSnapshot> LoadOrNullAsync(string provider, string model, string basePath, string fileNameFormat, CancellationToken ct)
		{
			var relativeDir = basePath?.TrimEnd('/', '\\') ?? "Config/RimAI/Indices";
			var fileName = (fileNameFormat ?? "tools_index_{provider}_{model}.json")
				.Replace("{provider}", provider)
				.Replace("{model}", model);
			var relativePath = $"{relativeDir}/{fileName}".Replace('\\', '/');
			var json = await _persistence.ReadTextUnderConfigOrNullAsync(relativePath, ct);
			if (string.IsNullOrEmpty(json)) return null;
			return JsonConvert.DeserializeObject<ToolIndexSnapshot>(json);
		}
	}
}


