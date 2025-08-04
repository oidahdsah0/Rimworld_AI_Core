using System;
using System.Threading.Tasks;
using NUnit.Framework;
using RimAI.Core.Contracts.Data;
using RimAI.Core.Services;
using RimAI.Core.Tests.Fakes;

namespace RimAI.Core.Tests
{
    [TestFixture]
    public class HistoryServiceTests
    {
        private HistoryService _history;

        [SetUp]
        public void Setup()
        {
            _history = new HistoryService(new FakeSchedulerService());
        }

        [Test]
        public async Task Record_And_Query_Mainline()
        {
            var participants = new[] { "pawnA", "pawnB" };
            await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "user", Content = "你好" });
            await _history.RecordEntryAsync(participants, new ConversationEntry { Role = "assistant", Content = "Hi" });

            var ctx = await _history.GetHistoryAsync(participants, 10);
            Assert.AreEqual(2, ctx.Mainline.Count);
        }

        [Test]
        public async Task Background_History_Included()
        {
            var ab = new[] { "A", "B" };
            var abc = new[] { "A", "B", "C" };
            await _history.RecordEntryAsync(abc, new ConversationEntry { Role = "user", Content = "背景" });
            await _history.RecordEntryAsync(ab, new ConversationEntry { Role = "user", Content = "主线" });

            var ctx = await _history.GetHistoryAsync(ab, 10);
            Assert.AreEqual(1, ctx.Background.Count);
        }
    }
}