using System.Threading.Tasks;
using RimAI.Core.Contracts.Events;

namespace RimAI.Core.Contracts.Services
{
    public interface IEventAggregatorService
    {
        Task HandleEventAsync(IEvent evt);
    }
}
