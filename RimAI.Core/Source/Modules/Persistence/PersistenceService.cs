using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core.Source.Modules.Persistence
{
	internal sealed class PersistenceService : IPersistenceService
	{
		private readonly string _root;

		public PersistenceService()
		{
			// 使用 RimWorld 存档目录作为配置根，避免写入 Mod 目录
			var baseDir = GenFilePaths.SaveDataFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			_root = Path.Combine(baseDir, "RimAI");
			Directory.CreateDirectory(_root);
		}

		public string GetConfigRootDirectory() => _root;

		public string EnsureDirectoryUnderConfig(string relativeDir)
		{
			var abs = Path.Combine(_root, relativeDir ?? string.Empty);
			Directory.CreateDirectory(abs);
			return abs;
		}

		public async Task WriteTextUnderConfigAsync(string relativePath, string content, CancellationToken ct = default)
		{
			var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
			var abs = Path.Combine(_root, normalized);
			var dir = Path.GetDirectoryName(abs);
			if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
			var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
			using (var fs = new FileStream(abs, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous))
			{
				await fs.WriteAsync(bytes, 0, bytes.Length, ct);
				await fs.FlushAsync(ct);
			}
		}

		public async Task<string> ReadTextUnderConfigOrNullAsync(string relativePath, CancellationToken ct = default)
		{
			var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("\\", Path.DirectorySeparatorChar.ToString());
			var abs = Path.Combine(_root, normalized);
			if (!File.Exists(abs)) return null;
			using (var fs = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.Asynchronous))
			using (var sr = new StreamReader(fs, Encoding.UTF8))
			{
				return await sr.ReadToEndAsync();
			}
		}
	}
}



