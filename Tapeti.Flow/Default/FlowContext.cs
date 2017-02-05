using System;
using System.Collections.Generic;
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

        public void Dispose()
        {
            FlowStateLock?.Dispose();
        }
    }
}
