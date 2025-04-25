using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    /// <summary>
    /// Default implementation of <see cref="IFlowStore"/>. Delegates to the registered <see cref="IDynamicFlowStore"/> and
    /// <see cref="IDurableFlowStore"/> as required.
    /// </summary>
    public class FlowStore : IFlowStore
    {
        private readonly IDynamicFlowStore dynamicFlowStore;
        private readonly IDurableFlowStore durableFlowStore;


        /// <inheritdoc cref="FlowStore" />
        public FlowStore(IDynamicFlowStore dynamicFlowStore, IDurableFlowStore durableFlowStore)
        {
            this.dynamicFlowStore = dynamicFlowStore;
            this.durableFlowStore = durableFlowStore;
        }


        /// <inheritdoc />
        public async ValueTask Load()
        {
            await dynamicFlowStore.Load();
            await durableFlowStore.Load();
        }


        /// <inheritdoc />
        public ValueTask<IFlowStateLock> LockNewFlowState(Guid? newFlowID)
        {
            // No need to perform any locking as the FlowID is new and not yet stored
            return ValueTask.FromResult<IFlowStateLock>(new FlowStateLockWrapper(this, newFlowID ?? Guid.NewGuid(), null, FlowStateLockSource.New));
        }


        /// <inheritdoc />
        public async ValueTask<IFlowStateLock?> LockFlowStateByContinuation(Guid continuationID)
        {
            // The dynamic repository is very likely to be in-memory and fastest to look up
            var innerLock = await dynamicFlowStore.LockFlowStateByContinuation(continuationID);
            if (innerLock != null)
                return new FlowStateLockWrapper(this, innerLock.FlowID, innerLock, FlowStateLockSource.Dynamic);

            innerLock = await durableFlowStore.LockFlowStateByContinuation(continuationID);
            return innerLock != null 
                ? new FlowStateLockWrapper(this, innerLock.FlowID, innerLock, FlowStateLockSource.Durable) 
                : null;
        }


        /// <inheritdoc />
        public async ValueTask<IEnumerable<ActiveFlow>> GetActiveFlows(DateTime? maxCreationTime)
        {
            return (await dynamicFlowStore.GetActiveFlows(maxCreationTime))
                .Concat(await durableFlowStore.GetActiveFlows(maxCreationTime));
        }


        private async ValueTask<IFlowStateLock> StoreFlowState(Guid flowID, FlowState flowState, IFlowStateLock? currentInnerLock, FlowStateLockSource currentSource, FlowStateLockSource newSource)
        {
            if (newSource != currentSource)
            {
                // When transitioning from a durable queue to a dynamic queue or vice versa,
                // remove the state from the previous source
                if (currentSource != FlowStateLockSource.New && currentInnerLock is not null)
                    await currentInnerLock.DeleteFlowState();

                switch (newSource)
                {
                    case FlowStateLockSource.Dynamic:
                        return await dynamicFlowStore.LockNewFlowState(flowID);

                    case FlowStateLockSource.Durable:
                        return await durableFlowStore.LockNewFlowState(flowID);

                    case FlowStateLockSource.New:
                    default:
                        throw new ArgumentOutOfRangeException(nameof(newSource), newSource, null);
                }
            }

            if (currentInnerLock is null)
                throw new InvalidOperationException("Tapeti internal bug, please report: trying to store a flow state that was loaded without a lock");

            await currentInnerLock.StoreFlowState(flowState, newSource == FlowStateLockSource.Durable);
            return currentInnerLock;
        }



        private enum FlowStateLockSource
        {
            New,
            Dynamic,
            Durable
        }


        private class FlowStateLockWrapper : IFlowStateLock
        {
            public Guid FlowID { get; }

            private readonly FlowStore owner;
            private IFlowStateLock? innerLock;
            private FlowStateLockSource source;


            public FlowStateLockWrapper(FlowStore owner, Guid flowID, IFlowStateLock? innerLock, FlowStateLockSource source)
            {
                this.owner = owner;
                this.innerLock = innerLock;
                this.source = source;

                FlowID = flowID;
            }

            
            public void Dispose()
            {
            }


            public FlowState? GetFlowState()
            {
                return innerLock?.GetFlowState();
            }


            public async ValueTask StoreFlowState(FlowState flowState, bool persistent)
            {
                var newSource = persistent ? FlowStateLockSource.Durable : FlowStateLockSource.Dynamic;

                innerLock = await owner.StoreFlowState(FlowID, flowState, innerLock, source, newSource);
                source = newSource;
            }


            public ValueTask DeleteFlowState()
            {
                return innerLock?.DeleteFlowState() ?? default;
            }
        }
    }
}
