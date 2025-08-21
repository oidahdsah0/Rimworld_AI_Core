using System.Threading;
using System.Threading.Tasks;

using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.Snapshots;

namespace RimAI.Core.Source.Modules.Persistence
{
	// P6 提前提供的最小文件 IO 接口，供 P4 索引存取使用
	// 仅 Persistence 模块允许触达 System.IO/Verse。
	internal interface IPersistenceService
	{
		// 组合入口
		void SaveAll(PersistenceSnapshot snapshot);
		PersistenceSnapshot LoadAll();

		// 统计
		PersistenceStats GetLastStats();

		// Debug 导出/导入
		string ExportAllToJson();
		void ImportAllFromJson(string json);

		// Debug/in-memory 操作（不触达 Scribe）
		PersistenceSnapshot GetLastSnapshotForDebug();
		void ReplaceLastSnapshotForDebug(PersistenceSnapshot snapshot);

		// 将文本写入配置根目录下的相对路径（必要时自动创建目录）
		Task WriteTextUnderConfigAsync(string relativePath, string content, CancellationToken ct = default);

		// 从配置根目录下读取文本（不存在则返回 null）
		Task<string> ReadTextUnderConfigOrNullAsync(string relativePath, CancellationToken ct = default);

		// 从 Mod 根目录下读取文本（不存在则返回 null），仅供读取内置默认配置/模板使用
		Task<string> ReadTextUnderModRootOrNullAsync(string relativePath, CancellationToken ct = default);

		// 配置根目录的绝对路径（只读）
		string GetConfigRootDirectory();

		// 确保配置根目录下的相对子目录存在，返回目录的绝对路径
		string EnsureDirectoryUnderConfig(string relativeDir);
	}
}



