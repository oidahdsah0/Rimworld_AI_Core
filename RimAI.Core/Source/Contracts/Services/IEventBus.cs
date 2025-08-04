using System;
using RimAI.Core.Contracts.Events;

namespace RimAI.Core.Contracts.Services
{
    public interface IEventBus
    {
        void Publish(IEvent evt);
        void Subscribe(Action<IEvent> handler);
        void Unsubscribe(Action<IEvent> handler);
    }
}
