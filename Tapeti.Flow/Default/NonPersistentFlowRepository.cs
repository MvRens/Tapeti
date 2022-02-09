using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation for IFlowRepository. Does not persist any state, relying on the FlowStore's cache instead.
    /// </summary>
    public class NonPersistentFlowRepository : IFlowRepository
    {
        ValueTask<IEnumerable<FlowRecord<T>>> IFlowRepository.GetStates<T>()
        {
            return new ValueTask<IEnumerable<FlowRecord<T>>>(Enumerable.Empty<FlowRecord<T>>());
        }

        /// <inheritdoc />
        public ValueTask CreateState<T>(Guid flowID, T state, DateTime timestamp)
        {
            return default;
        }

        /// <inheritdoc />
        public ValueTask UpdateState<T>(Guid flowID, T state)
        {
            return default;
        }

        /// <inheritdoc />
        public ValueTask DeleteState(Guid flowID)
        {
            return default;
        }
    }
}
