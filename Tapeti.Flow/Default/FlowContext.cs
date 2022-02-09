using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Tapeti.Flow.Default
{
    internal class FlowContext : IDisposable
    {
        public IFlowHandlerContext HandlerContext { get; set; }
        public IFlowStateLock FlowStateLock { get; set; }
        public FlowState FlowState { get; set; }

        public Guid ContinuationID { get; set; }
        public ContinuationMetadata ContinuationMetadata { get; set; }

        private int storeCalled;
        private int deleteCalled;


        public async Task Store(bool persistent)
        {
            storeCalled++;

            if (HandlerContext == null) throw new ArgumentNullException(nameof(HandlerContext));
            if (FlowState == null) throw new ArgumentNullException(nameof(FlowState));
            if (FlowStateLock == null) throw new ArgumentNullException(nameof(FlowStateLock));

            FlowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(HandlerContext.Controller);
            await FlowStateLock.StoreFlowState(FlowState, persistent);
        }

        public async Task Delete()
        {
            deleteCalled++;

            if (FlowStateLock != null)
                await FlowStateLock.DeleteFlowState();
        }

        public bool IsStoredOrDeleted()
        {
            return storeCalled > 0 || deleteCalled > 0;
        }

        public void EnsureStoreOrDeleteIsCalled()
        {
            if (!IsStoredOrDeleted())
                throw new InvalidProgramException("Neither Store nor Delete are called for the state of the current flow. FlowID = " + FlowStateLock?.FlowID);

            Debug.Assert(storeCalled <= 1, "Store called more than once!");
            Debug.Assert(deleteCalled <= 1, "Delete called more than once!");
        }

        public void Dispose()
        {
            FlowStateLock?.Dispose();
        }
    }
}
