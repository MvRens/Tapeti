using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    public class NonPersistentFlowRepository : IFlowRepository
    {
        Task<List<KeyValuePair<Guid, T>>> IFlowRepository.GetStates<T>()
        {
            return Task.FromResult(new List<KeyValuePair<Guid, T>>());
        }

        public Task CreateState<T>(Guid flowID, T state, DateTime timestamp)
        {
            return Task.CompletedTask;
        }

        public Task UpdateState<T>(Guid flowID, T state)
        {
            return Task.CompletedTask;
        }

        public Task DeleteState(Guid flowID)
        {
            return Task.CompletedTask;
        }
    }
}
