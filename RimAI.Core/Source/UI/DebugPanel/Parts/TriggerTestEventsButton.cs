using System.Threading.Tasks;
using RimAI.Core.Contracts.Eventing;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class TriggerTestEventsButton : IDebugPanelButton
    {
        public string Label => "Trigger Test Events";

        private sealed class TestEvent : IEvent
        {
            public string Id { get; } = System.Guid.NewGuid().ToString();
            public System.DateTime Timestamp { get; } = System.DateTime.UtcNow;
            public EventPriority Priority { get; }
            private readonly string _description;

            public TestEvent(EventPriority priority, string description)
            {
                Priority = priority;
                _description = description;
            }

            public string Describe() => _description;
        }

        public void Execute(DebugPanelContext ctx)
        {
            var eventBus = ctx.Get<IEventBus>();
            Task.Run(() =>
            {
                try
                {
                    ctx.AppendOutput("Publishing 5 test events (3 Low, 1 High, 1 Critical)...");
                    eventBus.Publish(new TestEvent(EventPriority.Low, "A trade caravan has arrived."));
                    eventBus.Publish(new TestEvent(EventPriority.Low, "A new colonist, 'Steve', has joined."));
                    eventBus.Publish(new TestEvent(EventPriority.High, "A psychic drone has started for female colonists."));
                    eventBus.Publish(new TestEvent(EventPriority.Low, "Component assembly finished."));
                    eventBus.Publish(new TestEvent(EventPriority.Critical, "A raid from the 'Savage Tribe' is attacking the colony."));
                    ctx.AppendOutput("Events published.");
                }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Trigger Test Events failed: {ex.Message}");
                }
            });
        }
    }
}


