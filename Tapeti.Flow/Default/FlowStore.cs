using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    public class FlowStore : IFlowStore
    {
        private static readonly ConcurrentDictionary<Guid, FlowState> FlowStates = new ConcurrentDictionary<Guid, FlowState>();
        private static readonly ConcurrentDictionary<Guid, Guid> ContinuationLookup = new ConcurrentDictionary<Guid, Guid>();

        private readonly IFlowRepository<FlowState> repository;


        public FlowStore(IFlowRepository<FlowState> repository) 
        {
            this.repository = repository;
        }


        public async Task Load()
        {
            FlowStates.Clear();
            ContinuationLookup.Clear();

            foreach (var flowStateRecord in await repository.GetStates())
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
            var isNew = false;
            var flowState = FlowStates.GetOrAdd(flowID, id =>
            {
                isNew = true;
                return new FlowState();
            });

            var result = new FlowStateLock(this, flowState, flowID, isNew);
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

            public Guid FlowID => flowID;

            public Task<FlowState> GetFlowState()
            {
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    return Task.FromResult(flowState.Clone());
                }
            }

            public async Task StoreFlowState(FlowState newFlowState)
            {
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    foreach (var removedContinuation in flowState.Continuations.Keys.Where(k => !newFlowState.Continuations.ContainsKey(k)))
                    {
                        Guid removedValue;
                        ContinuationLookup.TryRemove(removedContinuation, out removedValue);
                    }

                    foreach (var addedContinuation in newFlowState.Continuations.Where(c => !flowState.Continuations.ContainsKey(c.Key)))
                    {
                        ContinuationLookup.TryAdd(addedContinuation.Key, flowID);
                    }

                    flowState.Assign(newFlowState);
                }

                if (isNew)
                {
                    isNew = false;
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
                lock (flowState)
                {
                    if (isDisposed)
                        throw new ObjectDisposedException("FlowStateLock");

                    foreach (var removedContinuation in flowState.Continuations.Keys)
                    {
                        Guid removedValue;
                        ContinuationLookup.TryRemove(removedContinuation, out removedValue);
                    }

                    FlowState removedFlow;
                    FlowStates.TryRemove(flowID, out removedFlow);
                }

                if (!isNew)
                    await owner.repository.DeleteState(flowID);
            }
        }


        private static FlowStateRecord ToFlowStateRecord(Guid flowID, FlowState flowState)
        {
            return new FlowStateRecord
            {
                FlowID = flowID,
                Metadata = JsonConvert.SerializeObject(flowState.Metadata),
                Data = flowState.Data,
                ContinuationMetadata = flowState.Continuations.ToDictionary(
                    kv => kv.Key, 
                    kv => JsonConvert.SerializeObject(kv.Value))
            };
        }

        private static FlowState ToFlowState(FlowStateRecord flowStateRecord)
        {
            return new FlowState
            {
                Metadata = JsonConvert.DeserializeObject<FlowMetadata>(flowStateRecord.Metadata),
                Data = flowStateRecord.Data,
                Continuations = flowStateRecord.ContinuationMetadata.ToDictionary(
                    kv => kv.Key, 
                    kv => JsonConvert.DeserializeObject<ContinuationMetadata>(kv.Value))
            };
        }
    }
}
