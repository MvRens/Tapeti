using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tapeti.Flow.Default;

// ReSharper disable UnusedMember.Global

namespace Tapeti.Flow
{
    /// <summary>
    /// Provides a way to store and load flow state.
    /// </summary>
    public interface IFlowStore
    {
        /// <summary>
        /// Must be called during application startup before subscribing or starting a flow.
        /// If using an IFlowStore that requires an update (such as creating tables) make
        /// sure it is called before calling Load.
        /// </summary>
        ValueTask Load();

        /// <summary>
        /// Acquires a lock on a new flow.
        /// </summary>
        ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID = null);

        /// <summary>
        /// Acquires a lock on the flow with the specified ContinuationID.
        /// </summary>
        /// <param name="continuationID"></param>
        ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID);

        /// <summary>
        /// Returns information about the currently active flows.
        /// </summary>
        /// <remarks>
        /// This is intended for monitoring purposes and should be treated as a snapshot.
        /// </remarks>
        /// <param name="minimumAge">The minimum age of the flow before it is included in the result. Set to TimeSpan.Zero to return all active flows.</param>
        ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(TimeSpan minimumAge);
    }


    /// <summary>
    /// Represents an <see cref="IFlowStore"/> which handles flows which are part of a durable queue and must persist.
    /// </summary>
    public interface IDurableFlowStore : IFlowStore
    {
    }


    /// <summary>
    /// Represents an <see cref="IFlowStore"/> which handles flows which are part of a dynamic queue and should not be persisted.
    /// </summary>
    public interface IDynamicFlowStore : IFlowStore
    {
    }


    /// <summary>
    /// Represents a lock on the flow state, to provide thread safety.
    /// </summary>
    public interface IFlowStateLock : IDisposable
    {
        /// <summary>
        /// The unique ID of the flow state.
        /// </summary>
        Guid FlowID { get; }

        /// <summary>
        /// Acquires a copy of the flow state.
        /// </summary>
        ValueTask<FlowState?> GetFlowState();

        /// <summary>
        /// Stores the new flow state.
        /// </summary>
        /// <param name="flowState"></param>
        /// <param name="persistent"></param>
        ValueTask StoreFlowState(FlowState flowState, bool persistent);

        /// <summary>
        /// Disposes of the flow state corresponding to this Flow ID.
        /// </summary>
        ValueTask DeleteFlowState();
    }


    /// <summary>
    /// Contains information about an active flow, as returned by <see cref="IFlowStore.GetActiveFlows"/>.
    /// </summary>
    public class ActiveFlow
    {
        /// <summary>
        /// The ID of the active flow.
        /// </summary>
        public Guid FlowID { get; }

        /// <summary>
        /// The time when the flow was initially created.
        /// </summary>
        public DateTime CreationTime { get; }


        /// <summary>
        /// Create a new instance of an ActiveFlow.
        /// </summary>
        /// <param name="flowID">The ID of the active flow.</param>
        /// <param name="creationTime">The time when the flow was initially created.</param>
        public ActiveFlow(Guid flowID, DateTime creationTime)
        {
            FlowID = flowID;
            CreationTime = creationTime;
        }
    }
}
