namespace RimAI.Core.Source.Modules.Tooling
{
	internal interface IRimAITool
	{
		string Name { get; }
		string Description { get; }
		string ParametersJson { get; } // JSON Schema
		string DisplayName { get; } // 新增：给玩家看的短名称（可本地化）
		// 工具等级：1/2/3 为游戏可用；4 为开发级（仅开发/测试，不在游戏内出现）
		int Level { get; }
		string BuildToolJson();
	}
}



