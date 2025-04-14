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
        //private HashSet<string>? validatedMethods;

        //private readonly ITapetiConfig config;


        /// <inheritdoc cref="InMemoryFlowStore"/>
        public InMemoryFlowStore()
        {
        }


        /// <inheritdoc />
        public ValueTask Load()
        {
            return default;

            /* TODO move to helper
            validatedMethods = new HashSet<string>();
            try
            {
                foreach (var flowStateRecord in await repository.GetStates<FlowState>().ConfigureAwait(false))
                {
                    flowStates.TryAdd(flowStateRecord.FlowID, new CachedFlowState(flowStateRecord.FlowState, flowStateRecord.CreationTime, true));

                    foreach (var continuation in flowStateRecord.FlowState.Continuations)
                    {
                        ValidateContinuation(flowStateRecord.FlowID, continuation.Key, continuation.Value);
                        continuationLookup.GetOrAdd(continuation.Key, flowStateRecord.FlowID);
                    }
                }
            }
            finally
            {
                validatedMethods = null;
            }
            */
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


        /* TODO move to helper
        private void ValidateContinuation(Guid flowId, Guid continuationId, ContinuationMetadata metadata)
        {
            if (string.IsNullOrEmpty(metadata.MethodName))
                return;

            // We could check all the things that are required for a continuation or converge method, but this should suffice
            // for the common scenario where you change code without realizing that it's signature has been persisted
            // ReSharper disable once InvertIf
            if (validatedMethods!.Add(metadata.MethodName))
            {
                var methodInfo = MethodSerializer.Deserialize(metadata.MethodName);
                if (methodInfo == null)
                    throw new InvalidDataException($"Flow ID {flowId} references continuation method '{metadata.MethodName}' which no longer exists (continuation Id = {continuationId})");

                var binding = config.Bindings.ForMethod(methodInfo);
                if (binding == null)
                    throw new InvalidDataException($"Flow ID {flowId} references continuation method '{metadata.MethodName}' which no longer has a binding as a message handler (continuation Id = {continuationId})");
            }

            /* Disabled for now - the ConvergeMethodName does not include the assembly so we can't easily check it
            if (string.IsNullOrEmpty(metadata.ConvergeMethodName) || !validatedMethods.Add(metadata.ConvergeMethodName))
                return;
            
            var convergeMethodInfo = MethodSerializer.Deserialize(metadata.ConvergeMethodName);
            if (convergeMethodInfo == null)
                throw new InvalidDataException($"Flow ID {flowId} references converge method '{metadata.ConvergeMethodName}' which no longer exists (continuation Id = {continuationId})");

            // Converge methods are not message handlers themselves
            *
        }
        */


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(TimeSpan minimumAge)
        {
            var maximumDateTime = DateTime.UtcNow - minimumAge;

            return new ValueTask<IEnumerable<ActiveFlow>>(flowStates
                .Where(p => p.Value.CreationTime <= maximumDateTime)
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
