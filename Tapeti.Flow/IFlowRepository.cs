using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    /// <summary>
    /// Provides persistency for flow states.
    /// </summary>
    public interface IFlowRepository
    {
        /// <summary>
        /// Load the previously persisted flow states.
        /// </summary>
        /// <returns>A list of flow states, where the key is the unique Flow ID and the value is the deserialized T.</returns>
        Task<List<KeyValuePair<Guid, T>>> GetStates<T>();

        /// <summary>
        /// Stores a new flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID"></param>
        /// <param name="state"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        Task CreateState<T>(Guid flowID, T state, DateTime timestamp);

        /// <summary>
        /// Updates an existing flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID"></param>
        /// <param name="state"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        Task UpdateState<T>(Guid flowID, T state);

        /// <summary>
        /// Delete a flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID"></param>
        Task DeleteState(Guid flowID);
    }
}
