using System.Threading.Tasks;
using NUnit.Framework;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Services;
using RimAI.Core.Tests.Fakes;

namespace RimAI.Core.Tests
{
    [TestFixture]
    public class PersistenceTests
    {
        [Test]
        public async Task HistoryService_State_Saved_And_Restored()
        {
            var scheduler = new FakeSchedulerService();
            var history = new HistoryService(scheduler);
            var participants = new[] { "pawnX", "pawnY" };
            await history.RecordEntryAsync(participants, new ConversationEntry { Role = "user", Content = "Hello" });

            // 模拟保存
            var snapshot = (HistoryStateSnapshot)history.GetStateForPersistence();

            // 模拟加载到新的 HistoryService
            var history2 = new HistoryService(scheduler);
            history2.LoadStateFromPersistence(snapshot);

            var ctx = await history2.GetHistoryAsync(participants, 10);
            Assert.AreEqual(1, ctx.Mainline.Count);
            Assert.AreEqual("Hello", ctx.Mainline[0].Content);
        }
    }
}
