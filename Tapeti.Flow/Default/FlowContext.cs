using System;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    internal class FlowContext : IDisposable
    {
        public IMessageContext MessageContext { get; set; }
        public IFlowStateLock FlowStateLock { get; set; }
        public FlowState FlowState { get; set; }
        public Guid ContinuationID { get; set; }

        public FlowReplyMetadata Reply { get; set; }


        public void Dispose()
        {
            MessageContext?.Dispose();
            FlowStateLock?.Dispose();
        }
    }


    internal class FlowReplyMetadata
    {
        public string ReplyTo { get; set; }
        public string CorrelationId { get; set; }
        public string ResponseTypeName { get; set; }
    }


    internal class FlowMetadata
    {
        public string ControllerTypeName { get; set; }
        public FlowReplyMetadata Reply { get; set; }
    }


    internal class ContinuationMetadata
    {
        public string MethodName { get; set; }
        public string ConvergeMethodName { get; set; }
    }
}
