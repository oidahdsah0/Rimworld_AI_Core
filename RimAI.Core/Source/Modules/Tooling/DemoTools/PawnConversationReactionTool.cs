using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
	internal sealed class PawnConversationReactionTool : IRimAITool
	{
		public const string ToolName = "pawn_conversation_reaction";

		public string Name => ToolName;
	public string Description => "Record immediate mood reaction after a short chat: returns mood_delta [-30..30], mood_title (locale-constrained), and duration_days [1..10] (float).";
		public string DisplayName => "Pawn Conversation Reaction";
		public int Level => 4;
		public string ParametersJson => JsonConvert.SerializeObject(new
		{
			type = "object",
			properties = new
			{
				mood_delta = new { type = "integer", minimum = -30, maximum = 30, description = "Mood offset in range [-30,30]" },
				mood_title = new { type = "string", description = "Short localized title for the reaction" },
				duration_days = new { type = "float number", minimum = 1, maximum = 10, description = "Memory duration in days [1..10], decimals allowed" }
			},
			required = new[] { "mood_delta", "mood_title", "duration_days" }
		});

		public string BuildToolJson()
		{
			var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
			var json = JsonConvert.SerializeObject(new
			{
				type = "function",
				function = new
				{
					name = Name,
					description = Description,
					parameters = parameters
				}
			});
			return json;
		}
	}
}
