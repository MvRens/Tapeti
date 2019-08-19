using System;
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
        /// If using an IFlowRepository that requires an update (such as creating tables) make
        /// sure it is called before calling Load.
        /// </summary>
        Task Load();

        /// <summary>
        /// Looks up the FlowID corresponding to a ContinuationID. For internal use.
        /// </summary>
        /// <param name="continuationID"></param>
        Task<Guid?> FindFlowID(Guid continuationID);

        /// <summary>
        /// Acquires a lock on the flow with the specified FlowID.
        /// </summary>
        /// <param name="flowID"></param>
        Task<IFlowStateLock> LockFlowState(Guid flowID);
    }


    /// <inheritdoc />
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
        Task<FlowState> GetFlowState();

        /// <summary>
        /// Stores the new flow state.
        /// </summary>
        /// <param name="flowState"></param>
        /// <param name="persistent"></param>
        Task StoreFlowState(FlowState flowState, bool persistent);

        /// <summary>
        /// Disposes of the flow state corresponding to this Flow ID.
        /// </summary>
        Task DeleteFlowState();
    }
}
