using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Flow
{
    public class FlowStore : IFlowStore
    {
        private readonly IFlowRepository repository;
        private readonly ConcurrentDictionary<Guid, FlowState> flowStates = new ConcurrentDictionary<Guid, FlowState>();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new ConcurrentDictionary<Guid, Guid>();


        public FlowStore(IFlowRepository repository) 
        {
            this.repository = repository;
        }


        public async Task Load()
        {
            flowStates.Clear();
            continuationLookup.Clear();

            foreach (var state in await repository.GetAllStates())
            {
                flowStates.GetOrAdd(state.FlowID, new FlowState
                {
                    Metadata = state.Metadata,
                    Data = state.Data,
                    Continuations = state.Continuations
                });

                foreach (var continuation in state.Continuations)
                    continuationLookup.GetOrAdd(continuation.Key, state.FlowID);
            }
        }


        public Task<Guid?> FindFlowStateID(Guid continuationID)
        {
            Guid result;
            return Task.FromResult(continuationLookup.TryGetValue(continuationID, out result) ? result : (Guid?)null);
        }


        public async Task<IFlowStateLock> LockFlowState(Guid flowStateID)
        {
            var isNew = false;
            var flowState = flowStates.GetOrAdd(flowStateID, id =>
            {
                isNew = true;
                return new FlowState();
            });

            var result = new FlowStateLock(this, flowState, flowStateID, isNew);
            await result.Lock();

            return result;
        }


        private class FlowStateLock : IFlowStateLock
        {
            private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1);

            private readonly FlowStore owner;
            private readonly FlowState flowState;
            private readonly Guid flowID;
            private bool isNew;
            private bool isDisposed;


            public FlowStateLock(FlowStore owner, FlowState flowState, Guid flowID, bool isNew)
            {
                this.owner = owner;
                this.flowState = flowState;
                this.flowID = flowID;
                this.isNew = isNew;
            }


            public Task Lock()
            {
                return semaphore.WaitAsync();
            }


            public void Dispose()
            {
                lock (flowState)
                {
                    if (!isDisposed)
                    {
                        semaphore.Release();
                        semaphore.Dispose();
                    }

                    isDisposed = true;
                }
            }

            public Guid FlowStateID => flowID;

            public Task<FlowState> GetFlowState()
            {
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    return Task.FromResult(new FlowState
                    {
                        Data = flowState.Data,
                        Metadata = flowState.Metadata,
                        Continuations = flowState.Continuations.ToDictionary(kv => kv.Key, kv => kv.Value)
                    });
                }
            }

            public async Task StoreFlowState(FlowState newFlowState)
            {
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    foreach (
                        var removedContinuation in
                            flowState.Continuations.Keys.Where(
                                k => !newFlowState.Continuations.ContainsKey(k)))
                    {
                        Guid removedValue;
                        owner.continuationLookup.TryRemove(removedContinuation, out removedValue);
                    }

                    foreach (
                        var addedContinuation in
                            newFlowState.Continuations.Where(
                                c => !flowState.Continuations.ContainsKey(c.Key)))
                    {
                        owner.continuationLookup.TryAdd(addedContinuation.Key, flowID);
                    }

                    flowState.Metadata = newFlowState.Metadata;
                    flowState.Data = newFlowState.Data;
                    flowState.Continuations = newFlowState.Continuations.ToDictionary(kv => kv.Key, kv => kv.Value);
                }
                if (isNew)
                {
                    isNew = false;
                    var now = DateTime.UtcNow;
                    await
                        owner.repository.CreateState(flowID, now, flowState.Metadata, flowState.Data, flowState.Continuations);
                }
                else
                {
                    await owner.repository.UpdateState(flowID, flowState.Metadata, flowState.Data, flowState.Continuations);
                }
            }

            public async Task DeleteFlowState()
            {
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    foreach (var removedContinuation in flowState.Continuations.Keys)
                    {
                        Guid removedValue;
                        owner.continuationLookup.TryRemove(removedContinuation, out removedValue);
                    }
                    FlowState removedFlow;
                    owner.flowStates.TryRemove(flowID, out removedFlow);
                }
                if (!isNew)
                    await owner.repository.DeleteState(flowID);
            }

        }
    }
}
