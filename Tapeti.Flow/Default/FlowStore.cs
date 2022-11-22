using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Default implementation of IFlowStore.
    /// </summary>
    public class FlowStore : IFlowStore
    {
        private class CachedFlowState
        {
            public readonly FlowState FlowState;
            public readonly DateTime CreationTime;
            public readonly bool IsPersistent;

            public CachedFlowState(FlowState flowState, DateTime creationTime, bool isPersistent)
            {
                FlowState = flowState;
                CreationTime = creationTime;
                IsPersistent = isPersistent;
            }
        }

        private readonly ConcurrentDictionary<Guid, CachedFlowState> flowStates = new();
        private readonly ConcurrentDictionary<Guid, Guid> continuationLookup = new();
        private readonly LockCollection<Guid> locks = new(EqualityComparer<Guid>.Default);
        private HashSet<string> validatedMethods;

        private readonly IFlowRepository repository;
        private readonly ITapetiConfig config;

        private volatile bool inUse;
        private volatile bool loaded;


        /// <summary>
        /// </summary>
        public FlowStore(IFlowRepository repository, ITapetiConfig config)
        {
            this.repository = repository;
            this.config = config;
        }


        /// <inheritdoc />
        public async ValueTask Load()
        {
            if (inUse)
                throw new InvalidOperationException("Can only load the saved state once.");

            inUse = true;

            flowStates.Clear();
            continuationLookup.Clear();

            validatedMethods = new HashSet<string>();
            try
            {
                foreach (var flowStateRecord in await repository.GetStates<FlowState>())
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

            loaded = true;
        }
        

        private void ValidateContinuation(Guid flowId, Guid continuationId, ContinuationMetadata metadata)
        {
            // We could check all the things that are required for a continuation or converge method, but this should suffice
            // for the common scenario where you change code without realizing that it's signature has been persisted
            // ReSharper disable once InvertIf
            if (validatedMethods.Add(metadata.MethodName))
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
            */
        }


        /// <inheritdoc />
        public ValueTask<Guid?> FindFlowID(Guid continuationID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store is not yet loaded.");

            return new ValueTask<Guid?>(continuationLookup.TryGetValue(continuationID, out var result) ? result : null);
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock> LockFlowState(Guid flowID)
        {
            if (!loaded)
                throw new InvalidOperationException("Flow store should be loaded before storing flows.");

            inUse = true;

            var flowStatelock = new FlowStateLock(this, flowID, await locks.GetLock(flowID));
            return flowStatelock;
        }


        /// <inheritdoc />
        public ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(TimeSpan minimumAge)
        {
            var maximumDateTime = DateTime.UtcNow - minimumAge;

            return new ValueTask<IEnumerable<ActiveFlow>>(flowStates
                .Where(p => p.Value.CreationTime <= maximumDateTime)
                .Select(p => new ActiveFlow(p.Key, p.Value.CreationTime))
                .ToArray());
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

            public ValueTask<FlowState> GetFlowState()
            {
                if (flowLock == null)
                    throw new ObjectDisposedException("FlowStateLock");

                return new ValueTask<FlowState>(cachedFlowState?.FlowState?.Clone());
            }

            public async ValueTask StoreFlowState(FlowState newFlowState, bool persistent)
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

                cachedFlowState = new CachedFlowState(newFlowState, isNew ? DateTime.UtcNow : cachedFlowState.CreationTime, persistent);
                owner.flowStates[FlowID] = cachedFlowState;

                if (persistent)
                {
                    // Storing the flowstate in the underlying repository
                    if (isNew)
                    {
                        await owner.repository.CreateState(FlowID, cachedFlowState.FlowState, cachedFlowState.CreationTime);
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

            public async ValueTask DeleteFlowState()
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
