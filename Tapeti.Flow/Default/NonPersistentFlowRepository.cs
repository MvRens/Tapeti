using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    public class NonPersistentFlowRepository<T> : IFlowRepository<T>
    {
        Task<List<KeyValuePair<Guid, T>>> IFlowRepository<T>.GetStates()
        {
            return Task.FromResult(new List<KeyValuePair<Guid, T>>());
        }

        public Task CreateState(Guid flowID, T state, DateTime timestamp)
        {
            return Task.CompletedTask;
        }

        public Task UpdateState(Guid flowID, T state)
        {
            return Task.CompletedTask;
        }

        public Task DeleteState(Guid flowID)
        {
            return Task.CompletedTask;
        }
    }
}
