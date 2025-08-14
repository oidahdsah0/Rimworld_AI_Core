namespace RimAI.Core.Source.Modules.Persona.FixedPrompt
{
	internal interface IFixedPromptService
	{
		RimAI.Core.Source.Modules.Persona.FixedPromptSnapshot Get(string entityId);
		void Set(string entityId, string text);
	}
}


