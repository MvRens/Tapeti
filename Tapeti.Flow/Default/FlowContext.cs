using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal class FlowContext : IAsyncDisposable
    {
        private readonly IFlowHandlerContext? handlerContext;
        private IFlowStateLock? flowStateLock;
        private FlowState? flowState;


        public IFlowHandlerContext HandlerContext => handlerContext ?? throw new InvalidOperationException("FlowContext does not have a HandlerContext");
        public IFlowStateLock FlowStateLock => flowStateLock ?? throw new InvalidOperationException("FlowContext does not have a FlowStateLock");
        public FlowState FlowState => flowState ?? throw new InvalidOperationException("FlowContext does not have a FlowState");

        public bool HasFlowStateAndLock => flowState != null && flowStateLock != null;

        public Guid ContinuationID { get; set; }
        public ContinuationMetadata? ContinuationMetadata { get; set; }

        private int storeCalled;
        private int deleteCalled;


        public FlowContext(IFlowHandlerContext handlerContext, FlowState flowState, IFlowStateLock flowStateLock)
        {
            this.flowState = flowState;
            this.flowStateLock = flowStateLock;
            this.handlerContext = handlerContext;
        }


        public FlowContext(IFlowHandlerContext handlerContext)
        {
            this.handlerContext = handlerContext;
        }


        public void SetFlowState(FlowState newFlowState, IFlowStateLock newFlowStateLock)
        {
            flowState = newFlowState;
            flowStateLock = newFlowStateLock;
        }


        public ValueTask Store(bool persistent)
        {
            storeCalled++;

            FlowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(HandlerContext.Controller);
            return FlowStateLock.StoreFlowState(FlowState, persistent);
        }

        public ValueTask Delete()
        {
            deleteCalled++;
            return flowStateLock?.DeleteFlowState() ?? default;
        }

        public bool IsStoredOrDeleted()
        {
            return storeCalled > 0 || deleteCalled > 0;
        }

        public void EnsureStoreOrDeleteIsCalled()
        {
            if (!IsStoredOrDeleted())
                throw new InvalidProgramException("Neither Store nor Delete are called for the state of the current flow. FlowID = " + flowStateLock?.FlowID);

            Debug.Assert(storeCalled <= 1, "Store called more than once!");
            Debug.Assert(deleteCalled <= 1, "Delete called more than once!");
        }

        public async ValueTask DisposeAsync()
        {
            if (flowStateLock is not null)
                await flowStateLock.DisposeAsync();
        }
    }
}
