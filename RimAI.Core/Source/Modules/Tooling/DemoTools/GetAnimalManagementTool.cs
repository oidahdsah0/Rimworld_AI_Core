namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetAnimalManagementTool : IRimAITool
    {
        public string Name => "get_animal_management";
        public string Description => "获取牲畜管理概览：数量分布、训练掌握度以及口粮压力（天数）。";
        public string ParametersJson => "{\n  \"type\": \"object\",\n  \"properties\": {},\n  \"required\": []\n}";
        public string DisplayName => "牲畜管理";
        public int Level => 1;
        public string BuildToolJson()
        {
            return "{\n  \"type\": \"function\",\n  \"function\": {\n    \"name\": \"get_animal_management\",\n    \"description\": \"获取牲畜管理概览：数量分布、训练掌握度以及口粮压力（天数）。\",\n    \"parameters\": {\n      \"type\": \"object\",\n      \"properties\": {},\n      \"required\": []\n    }\n  }\n}";
        }
    }
}
