using System.Threading.Tasks;
using NUnit.Framework;
using RimAI.Core.Services;
using RimAI.Core.Tests.Fakes;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Tests
{
    [TestFixture]
    public class WorldAndCommandTests
    {
        private ISchedulerService _scheduler;
        private WorldDataService _worldData;
        private CommandService _commandService;

        [SetUp]
        public void Setup()
        {
            _scheduler = new FakeSchedulerService();
            _worldData = new WorldDataService(_scheduler);
            _commandService = new CommandService(_scheduler);
        }

        [Test]
        public async Task WorldDataService_ReturnsGameTick()
        {
            var tick = await _worldData.GetCurrentGameTickAsync();
            // 在 Fake 环境下返回 0（因为 Verse 未初始化），但应保证 Task 完成
            Assert.That(tick, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public async Task CommandService_ExecuteReturnsSuccess()
        {
            var result = await _commandService.ExecuteCommandAsync("dummy", null);
            Assert.IsTrue(result.IsSuccess);
        }
    }
}