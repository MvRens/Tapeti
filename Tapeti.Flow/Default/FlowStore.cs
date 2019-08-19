using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    /// <inheritdoc />
    /// <summary>
    /// Default implementation of IFlowStore.
    /// </summary>
    public class FlowStore : IFlowStore
    {
        private class CachedFlowState
        {
            public readonly FlowState FlowState;
            public readonly bool IsPersistent;

            public CachedFlowState(FlowState flowState, bool isPersistent)
            {
                FlowState = flowState;
                IsPersistent = isPersistent;
            }
        }

        private readonly ConcurrentDictionary<Guid, CachedFlowState> flowStates = new ConcurrentDictionary<Guid, CachedFlowState>();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new ConcurrentDictionary<Guid, Guid>();
        private readonly LockCollection<Guid> locks = new LockCollection<Guid>(EqualityComparer<Guid>.Default);

        private readonly IFlowRepository repository;

        private volatile bool inUse;
        private volatile bool loaded;


        /// <inheritdoc />
        public FlowStore(IFlowRepository repository) 
        {
            this.repository = repository;
        }


        /// <inheritdoc />
        public async Task Load()
        {
            if (inUse)
                throw new InvalidOperationException("Can only load the saved state once.");

            inUse = true;

            flowStates.Clear();
            continuationLookup.Clear();

            foreach (var flowStateRecord in await repository.GetStates<FlowState>())
            {
                flowStates.TryAdd(flowStateRecord.Key, new CachedFlowState(flowStateRecord.Value, true));

                foreach (var continuation in flowStateRecord.Value.Continuations)
                    continuationLookup.GetOrAdd(continuation.Key, flowStateRecord.Key);
            }

            loaded = true;
        }


        /// <inheritdoc />
        public Task<Guid?> FindFlowID(Guid continuationID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            return Task.FromResult(continuationLookup.TryGetValue(continuationID, out var result) ? result : (Guid?)null);
        }


        /// <inheritdoc />
        public async Task<IFlowStateLock> LockFlowState(Guid flowID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store should be loaded before storing flows.");

            inUse = true;

            var flowStatelock = new FlowStateLock(this, flowID, await locks.GetLock(flowID));
            return flowStatelock;
        }


        private class FlowStateLock : IFlowStateLock
        {
            private readonly FlowStore owner;
            private volatile IDisposable flowLock;
            private CachedFlowState cachedFlowState;

            public Guid FlowID { get; }


            public FlowStateLock(FlowStore owner, Guid flowID, IDisposable flowLock)
            {
                this.owner = owner;
                FlowID = flowID;
                this.flowLock = flowLock;

                owner.flowStates.TryGetValue(flowID, out cachedFlowState);
            }

            public void Dispose()
            {
                var l = flowLock;
                flowLock = null;
                l?.Dispose();
            }

            public Task<FlowState> GetFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return Task.FromResult(cachedFlowState.FlowState?.Clone());
            }

            public async Task StoreFlowState(FlowState newFlowState, bool persistent)
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                // Ensure no one has a direct reference to the protected state in the dictionary
                newFlowState = newFlowState.Clone();

                // Update the lookup dictionary for the ContinuationIDs
                if (cachedFlowState != null)
                {
                    foreach (var removedContinuation in cachedFlowState.FlowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                        owner.continuationLookup.TryRemove(removedContinuation, out _);
                }

                foreach (var addedContinuation in newFlowState.Continuations.Where(c => cachedFlowState == null || !cachedFlowState.FlowState.Continuations.ContainsKey(c.Key)))
                {
                    owner.continuationLookup.TryAdd(addedContinuation.Key, FlowID);
                }

                var isNew = cachedFlowState == null;
                var wasPersistent = cachedFlowState?.IsPersistent ?? false;

                cachedFlowState = new CachedFlowState(newFlowState, persistent);
                owner.flowStates[FlowID] = cachedFlowState;

                if (persistent)
                {
                    // Storing the flowstate in the underlying repository
                    if (isNew)
                    {
                        var now = DateTime.UtcNow;
                        await owner.repository.CreateState(FlowID, cachedFlowState.FlowState, now);
                    }
                    else
                    {
                        await owner.repository.UpdateState(FlowID, cachedFlowState.FlowState);
                    }
                }
                else if (wasPersistent)
                {
                    // We transitioned from a durable queue to a dynamic queue,
                    // remove the persistent state but keep the in-memory version
                    await owner.repository.DeleteState(FlowID);
                }
            }

            public async Task DeleteFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                if (cachedFlowState != null)
                {
                    foreach (var removedContinuation in cachedFlowState.FlowState.Continuations.Keys)
                        owner.continuationLookup.TryRemove(removedContinuation, out _);

                    owner.flowStates.TryRemove(FlowID, out var removedFlowState);
                    cachedFlowState = null;

                    if (removedFlowState.IsPersistent)
                        await owner.repository.DeleteState(FlowID);
                }
            }
        }
    }
}
