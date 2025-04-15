using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Default implementation of IFlowStore.
    /// </summary>
    /// <remarks>
    /// Does not persist the flows across restarts and does not support running multiple instances of a service
    /// consuming from a single queue and is therefore not recommended as the implementation for
    /// <see cref="IDurableFlowStore"/>.
    /// </remarks>
    public class InMemoryFlowStore : IDynamicFlowStore, IDurableFlowStore
    {
        private class CachedFlowState
        {
            public readonly FlowState FlowState;
            public readonly DateTime CreationTime;


            public CachedFlowState(FlowState flowState, DateTime creationTime)
            {
                FlowState = flowState;
                CreationTime = creationTime;
            }
        }

        private readonly ConcurrentDictionary<Guid, CachedFlowState> flowStates = new();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new();
        private readonly LockCollection<Guid> locks = new(EqualityComparer<Guid>.Default);


        /// <inheritdoc cref="InMemoryFlowStore"/>
        public InMemoryFlowStore()
        {
        }


        /// <inheritdoc />
        public ValueTask Load()
        {
            return default;
        }


        /// <inheritdoc />
        public ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID)
        {
            var flowID = newFlowID ?? Guid.NewGuid();
            return GetFlowStateLock(flowID, true);
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID)
        {
            return continuationLookup.TryGetValue(continuationID, out var flowID)
                ? await GetFlowStateLock(flowID, false)
                : null;
        }


        private async ValueTask<IFlowStateLock> GetFlowStateLock(Guid flowID, bool isNew)
        {
            var flowLock = await locks.GetLock(flowID).ConfigureAwait(false);
            var cachedFlowState = isNew ? null : flowStates.GetValueOrDefault(flowID);

            return new FlowStateLock(this, flowID, flowLock, cachedFlowState);
        }


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(DateTime? maxCreationTime)
        {
            IEnumerable<KeyValuePair<Guid, CachedFlowState>> query = flowStates;

            if (maxCreationTime is not null)
                query = query.Where(p => p.Value.CreationTime <= maxCreationTime);

            return ValueTask.FromResult<IEnumerable<ActiveFlow>>(query
                .Select(p => new ActiveFlow(p.Key, p.Value.CreationTime))
                .ToArray());
        }


        private ValueTask RemoveState(Guid flowID)
        {
            if (!flowStates.TryRemove(flowID, out var cachedFlowState))
                return default;

            foreach (var continuationID in cachedFlowState.FlowState.Continuations.Keys)
                continuationLookup.TryRemove(continuationID, out _);

            return default;
        }


        private CachedFlowState StoreFlowState(Guid flowID, CachedFlowState? cachedFlowState, FlowState newFlowState)
        {
            // Update the lookup dictionary for the ContinuationIDs
            if (cachedFlowState?.FlowState != null)
            {
                foreach (var removedContinuation in cachedFlowState.FlowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                    continuationLookup.TryRemove(removedContinuation, out _);
            }

            foreach (var addedContinuation in newFlowState.Continuations.Where(c => cachedFlowState?.FlowState == null || !cachedFlowState.FlowState.Continuations.ContainsKey(c.Key)))
                continuationLookup.TryAdd(addedContinuation.Key, flowID);

            var newCachedFlowState = new CachedFlowState(newFlowState, cachedFlowState?.CreationTime ?? DateTime.UtcNow);
            flowStates[flowID] = newCachedFlowState;

            return newCachedFlowState;
        }


        private class FlowStateLock : IFlowStateLock
        {
            private readonly InMemoryFlowStore owner;
            private volatile IDisposable? flowLock;
            private CachedFlowState? cachedFlowState;

            public Guid FlowID { get; }


            public FlowStateLock(InMemoryFlowStore owner, Guid flowID, IDisposable flowLock, CachedFlowState? cachedFlowState)
            {
                this.owner = owner;
                this.flowLock = flowLock;
                this.cachedFlowState = cachedFlowState;

                FlowID = flowID;
            }

            public void Dispose()
            {
                var l = flowLock;
                flowLock = null;
                l?.Dispose();
            }

            public ValueTask<FlowState?> GetFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return new ValueTask<FlowState?>(cachedFlowState?.FlowState);
            }

            public ValueTask StoreFlowState(FlowState flowState, bool persistent)
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                cachedFlowState = owner.StoreFlowState(FlowID, cachedFlowState, flowState);
                return default;
            }

            public ValueTask DeleteFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return owner.RemoveState(FlowID);
            }
        }
    }
}
