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
        Task<IEnumerable<FlowRecord<T>>> GetStates<T>();

        /// <summary>
        /// Stores a new flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID">The unique ID of the flow.</param>
        /// <param name="state">The flow state to be stored.</param>
        /// <param name="timestamp">The time when the flow was initially created.</param>
        /// <returns></returns>
        Task CreateState<T>(Guid flowID, T state, DateTime timestamp);

        /// <summary>
        /// Updates an existing flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID">The unique ID of the flow.</param>
        /// <param name="state">The flow state to be stored.</param>
        Task UpdateState<T>(Guid flowID, T state);

        /// <summary>
        /// Delete a flow state. Guaranteed to be run in a lock for the specified flow ID.
        /// </summary>
        /// <param name="flowID">The unique ID of the flow.</param>
        Task DeleteState(Guid flowID);
    }


    /// <summary>
    /// Contains information about a persisted flow state.
    /// </summary>
    public class FlowRecord<T>
    {
        /// <summary>
        /// The unique ID of the flow.
        /// </summary>
        public Guid FlowID { get; }

        /// <summary>
        /// The time when the flow was initially created.
        /// </summary>
        public DateTime CreationTime { get; }

        /// <summary>
        /// The stored flow state.
        /// </summary>
        public T FlowState { get; }


        /// <summary>
        /// Creates a new instance of a FlowRecord.
        /// </summary>
        /// <param name="flowID"></param>
        /// <param name="creationTime"></param>
        /// <param name="flowState"></param>
        public FlowRecord(Guid flowID, DateTime creationTime, T flowState)
        {
            FlowID = flowID;
            CreationTime = creationTime;
            FlowState = flowState;
        }
    }
}
