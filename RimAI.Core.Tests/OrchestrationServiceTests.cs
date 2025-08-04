using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using RimAI.Core.Services;
using RimAI.Core.Tests.Fakes;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Tests
{
    [TestFixture]
    public class OrchestrationServiceTests
    {
        private OrchestrationService _orchestrationNoTool;
        private OrchestrationService _orchestrationWithTool;

        [SetUp]
        public void Setup()
        {
            var promptFactory = new FakePromptFactoryService();
            var history = new HistoryService(new FakeSchedulerService());

            // 无工具场景：FakeLLM 不返回 tool_calls
            _orchestrationNoTool = new OrchestrationService(
                new FakeLLMService(false, "HI"),
                promptFactory,
                new ToolRegistryService(new List<RimAI.Core.Contracts.Tools.IRimAITool>()),
                history);

            // 有工具场景
            var echoTool = new FakeEchoTool();
            _orchestrationWithTool = new OrchestrationService(
                new FakeLLMService(true, "DONE"),
                promptFactory,
                new ToolRegistryService(new List<RimAI.Core.Contracts.Tools.IRimAITool> { echoTool }),
                history);
        }

        [Test]
        public async Task Simple_QA_No_Tool()
        {
            var chunks = new List<string>();
            await foreach (var res in _orchestrationNoTool.ExecuteToolAssistedQueryAsync("你好", "你是助手"))
            {
                if (res.IsSuccess && res.Value.ContentDelta != null) chunks.Add(res.Value.ContentDelta);
            }
            Assert.AreEqual("HI", string.Concat(chunks));
        }

        [Test]
        public async Task Tool_Call_Path()
        {
            var text = string.Concat(await _orchestrationWithTool.ExecuteToolAssistedQueryAsync("echo", "system").SelectAwait(r => new ValueTask<string>(r.IsSuccess && r.Value.ContentDelta!=null ? r.Value.ContentDelta:"" )).ToListAsync());
            Assert.AreEqual("DONE", text);
        }
    }
}
