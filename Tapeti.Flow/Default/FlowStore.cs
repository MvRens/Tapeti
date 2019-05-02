using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowStore : IFlowStore
    {
        private readonly ConcurrentDictionary<Guid, FlowState> flowStates = new ConcurrentDictionary<Guid, FlowState>();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new ConcurrentDictionary<Guid, Guid>();
        private readonly LockCollection<Guid> locks = new LockCollection<Guid>(EqualityComparer<Guid>.Default);

        private readonly IFlowRepository repository;

        private volatile bool inUse;
        private volatile bool loaded;

        public FlowStore(IFlowRepository repository) 
        {
            this.repository = repository;
        }


        public async Task Load()
        {
            if (inUse)
                throw new InvalidOperationException("Can only load the saved state once.");

            inUse = true;

            flowStates.Clear();
            continuationLookup.Clear();

            foreach (var flowStateRecord in await repository.GetStates<FlowState>())
            {
                flowStates.TryAdd(flowStateRecord.Key, flowStateRecord.Value);

                foreach (var continuation in flowStateRecord.Value.Continuations)
                    continuationLookup.GetOrAdd(continuation.Key, flowStateRecord.Key);
            }

            loaded = true;
        }


        public Task<Guid?> FindFlowID(Guid continuationID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            return Task.FromResult(continuationLookup.TryGetValue(continuationID, out var result) ? result : (Guid?)null);
        }


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
            private readonly Guid flowID;
            private volatile IDisposable flowLock;
            private FlowState flowState;


            public FlowStateLock(FlowStore owner, Guid flowID, IDisposable flowLock)
            {
                this.owner = owner;
                this.flowID = flowID;
                this.flowLock = flowLock;

                owner.flowStates.TryGetValue(flowID, out flowState);
            }

            public void Dispose()
            {
                var l = flowLock;
                flowLock = null;
                l?.Dispose();
            }

            public Guid FlowID => flowID;

            public Task<FlowState> GetFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return Task.FromResult(flowState?.Clone());
            }

            public async Task StoreFlowState(FlowState newFlowState)
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                // Ensure no one has a direct reference to the protected state in the dictionary
                newFlowState = newFlowState.Clone();

                // Update the lookup dictionary for the ContinuationIDs
                if (flowState != null)
                {
                    foreach (var removedContinuation in flowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                        owner.continuationLookup.TryRemove(removedContinuation, out _);
                }

                foreach (var addedContinuation in newFlowState.Continuations.Where(c => flowState == null || !flowState.Continuations.ContainsKey(c.Key)))
                {
                    owner.continuationLookup.TryAdd(addedContinuation.Key, flowID);
                }

                var isNew = flowState == null;
                flowState = newFlowState;
                owner.flowStates[flowID] = newFlowState;

                // Storing the flowstate in the underlying repository
                if (isNew)
                {
                    var now = DateTime.UtcNow;
                    await owner.repository.CreateState(flowID, flowState, now);
                }
                else
                {
                    await owner.repository.UpdateState(flowID, flowState);
                }
            }

            public async Task DeleteFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                if (flowState != null)
                {
                    foreach (var removedContinuation in flowState.Continuations.Keys)
                        owner.continuationLookup.TryRemove(removedContinuation, out _);

                    owner.flowStates.TryRemove(flowID, out _);

                    if (flowState != null)
                    {
                        flowState = null;
                        await owner.repository.DeleteState(flowID);
                    }
                }
            }
        }
    }
}
