namespace RimAI.Core.Source.Modules.Tooling
{
	internal interface IRimAITool
	{
		string Name { get; }
		string Description { get; }
		string ParametersJson { get; } // JSON Schema
		string BuildToolJson();
	}
}



