using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation for IFlowRepository. Does not persist any state, relying on the FlowStore's cache instead.
    /// </summary>
    public class NonPersistentFlowRepository : IFlowRepository
    {
        Task<List<KeyValuePair<Guid, T>>> IFlowRepository.GetStates<T>()
        {
            return Task.FromResult(new List<KeyValuePair<Guid, T>>());
        }

        /// <inheritdoc />
        public Task CreateState<T>(Guid flowID, T state, DateTime timestamp)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task UpdateState<T>(Guid flowID, T state)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task DeleteState(Guid flowID)
        {
            return Task.CompletedTask;
        }
    }
}
