namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetAnimalManagementTool : IRimAITool
    {
        public string Name => "get_animal_management";
    public string Description => "Get a livestock management overview: herd counts and distribution, training proficiency, and feed pressure (in days).";
        public string ParametersJson => "{\n  \"type\": \"object\",\n  \"properties\": {},\n  \"required\": []\n}";
    public string DisplayName => "tool.display.get_animal_management";
        public int Level => 1;
        public string BuildToolJson()
        {
            return "{\n  \"type\": \"function\",\n  \"function\": {\n    \"name\": \"get_animal_management\",\n    \"description\": \"Get a livestock management overview: herd counts and distribution, training proficiency, and feed pressure (in days).\",\n    \"parameters\": {\n      \"type\": \"object\",\n      \"properties\": {},\n      \"required\": []\n    }\n  }\n}";
        }
    }
}
