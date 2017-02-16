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

        private bool stored;


        public async Task EnsureStored()
        {
            if (stored)
                return;

            if (MessageContext == null) throw new ArgumentNullException(nameof(MessageContext));
            if (FlowState == null) throw new ArgumentNullException(nameof(FlowState));
            if (FlowStateLock == null) throw new ArgumentNullException(nameof(FlowStateLock));

            FlowState.Data = Newtonsoft.Json.JsonConvert.SerializeObject(MessageContext.Controller);
            await FlowStateLock.StoreFlowState(FlowState);

            stored = true;
        }

        public void Dispose()
        {
            FlowStateLock?.Dispose();
        }
    }
}
