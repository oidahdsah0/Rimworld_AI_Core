namespace RimAI.Core.Source.Modules.Tooling
{
	internal interface IRimAITool
	{
		string Name { get; }
		string Description { get; }
		string ParametersJson { get; } // JSON Schema
		string DisplayName { get; } // 新增：给玩家看的短名称（可本地化）
		string BuildToolJson();
	}
}



