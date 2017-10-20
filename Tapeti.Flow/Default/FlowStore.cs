using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowStore : IFlowStore
    {
        private readonly ConcurrentDictionary<Guid, FlowState> FlowStates = new ConcurrentDictionary<Guid, FlowState>();
        private readonly ConcurrentDictionary<Guid, Guid> ContinuationLookup = new ConcurrentDictionary<Guid, Guid>();
        private readonly LockCollection<Guid> Locks = new LockCollection<Guid>(EqualityComparer<Guid>.Default);

        private readonly IFlowRepository repository;

        private volatile bool InUse = false;

        public FlowStore(IFlowRepository repository) 
        {
            this.repository = repository;
        }


        public async Task Load()
        {
            if (InUse)
                throw new InvalidOperationException("Can only load the saved state once.");

            InUse = true;

            FlowStates.Clear();
            ContinuationLookup.Clear();

            foreach (var flowStateRecord in await repository.GetStates<FlowState>())
            {
                FlowStates.TryAdd(flowStateRecord.Key, flowStateRecord.Value);

                foreach (var continuation in flowStateRecord.Value.Continuations)
                    ContinuationLookup.GetOrAdd(continuation.Key, flowStateRecord.Key);
            }
        }


        public Task<Guid?> FindFlowID(Guid continuationID)
        {
            Guid result;
            return Task.FromResult(ContinuationLookup.TryGetValue(continuationID, out result) ? result : (Guid?)null);
        }


        public async Task<IFlowStateLock> LockFlowState(Guid flowID)
        {
            InUse = true;

            var flowStatelock = new FlowStateLock(this, flowID, await Locks.GetLock(flowID));
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

                owner.FlowStates.TryGetValue(flowID, out flowState);
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
                    {
                        Guid removedValue;
                        owner.ContinuationLookup.TryRemove(removedContinuation, out removedValue);
                    }
                }

                foreach (var addedContinuation in newFlowState.Continuations.Where(c => flowState == null || !flowState.Continuations.ContainsKey(c.Key)))
                {
                    owner.ContinuationLookup.TryAdd(addedContinuation.Key, flowID);
                }

                var isNew = flowState == null;
                flowState = newFlowState;
                owner.FlowStates[flowID] = newFlowState;

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
                    {
                        Guid removedValue;
                        owner.ContinuationLookup.TryRemove(removedContinuation, out removedValue);
                    }

                    FlowState removedFlow;
                    owner.FlowStates.TryRemove(flowID, out removedFlow);

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
