using System;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    internal class FlowContext : IDisposable
    {
        public IMessageContext MessageContext { get; set; }
        public IFlowStateLock FlowStateLock { get; set; }
        public FlowState FlowState { get; set; }

        public Guid ContinuationID { get; set; }
        public ContinuationMetadata ContinuationMetadata { get; set; }

        private bool storeCalled;
        private bool deleteCalled;


        public async Task Store()
        {
            storeCalled = true;

            if (MessageContext == null) throw new ArgumentNullException(nameof(MessageContext));
            if (FlowState == null) throw new ArgumentNullException(nameof(FlowState));
            if (FlowStateLock == null) throw new ArgumentNullException(nameof(FlowStateLock));

            FlowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(MessageContext.Controller);
            await FlowStateLock.StoreFlowState(FlowState);
        }

        public async Task Delete()
        {
            deleteCalled = true;

            if (FlowStateLock != null)
                await FlowStateLock.DeleteFlowState();
        }

        public void EnsureStoreOrDeleteIsCalled()
        {
            if (!storeCalled && !deleteCalled)
                throw new InvalidProgramException("Neither Store nor Delete are called for the state of the current flow. FlowID = " + FlowStateLock?.FlowID);
        }

        public void Dispose()
        {
            FlowStateLock?.Dispose();
        }
    }
}
