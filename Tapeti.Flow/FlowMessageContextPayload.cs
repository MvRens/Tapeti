using System;
using Tapeti.Config;
using Tapeti.Flow.Default;

namespace Tapeti.Flow
{
    /// <summary>
    /// Contains information about the flow for the current message. For internal use.
    /// </summary>
    internal class FlowMessageContextPayload : IMessageContextPayload, IDisposable
    {
        public FlowContext FlowContext { get; }

        /// <summary>
        /// Indicates if the current message handler is the last one to be called before a
        /// parallel flow is done and the convergeMethod will be called.
        /// Temporarily disables storing the flow state.
        /// </summary>
        public bool FlowIsConverging => FlowContext.FlowState.Continuations.Count == 0 &&
                                        FlowContext.ContinuationMetadata?.ConvergeMethodName != null;

        
        public FlowMessageContextPayload(FlowContext flowContext)
        {
            FlowContext = flowContext;
        }

        
        public void Dispose()
        {
            FlowContext.Dispose();
        }
    }
}
