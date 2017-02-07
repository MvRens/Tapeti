using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowBindingFilter : IBindingFilter
    {
        public async Task<bool> Accept(IMessageContext context, IBinding binding)
        {
            var flowContext = await GetFlowContext(context);
            if (flowContext?.ContinuationMetadata == null)
                return false;

            return flowContext.ContinuationMetadata.MethodName == MethodSerializer.Serialize(binding.Method);
        }


        private static async Task<FlowContext> GetFlowContext(IMessageContext context)
        {
            if (context.Items.ContainsKey(ContextItems.FlowContext))
                return (FlowContext)context.Items[ContextItems.FlowContext];

            if (context.Properties.CorrelationId == null)
                return null;

            Guid continuationID;
            if (!Guid.TryParse(context.Properties.CorrelationId, out continuationID))
                return null;

            var flowStore = context.DependencyResolver.Resolve<IFlowStore>();

            var flowID = await flowStore.FindFlowID(continuationID);
            if (!flowID.HasValue)
                return null;

            var flowStateLock = await flowStore.LockFlowState(flowID.Value);
            if (flowStateLock == null)
                return null;

            var flowState = await flowStateLock.GetFlowState();
            if (flowState == null)
                return null;

            ContinuationMetadata continuation;

            var flowContext = new FlowContext
            {
                MessageContext = context,

                FlowStateLock = flowStateLock,
                FlowState = flowState,

                ContinuationID = continuationID,
                ContinuationMetadata = flowState.Continuations.TryGetValue(continuationID, out continuation) ? continuation : null
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Items.Add(ContextItems.FlowContext, flowContext);
            return flowContext;
        }
    }
}
