using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowMessageFilterMiddleware : IMessageFilterMiddleware
    {
        public async Task Handle(IMessageContext context, Func<Task> next)
        {
            var flowContext = await GetFlowContext(context);
            if (flowContext?.ContinuationMetadata == null)
                return;

            if (flowContext.ContinuationMetadata.MethodName != MethodSerializer.Serialize(context.Binding.Method))
                return;

            await next();
        }


        private static async Task<FlowContext> GetFlowContext(IMessageContext context)
        {
            if (context.Items.ContainsKey(ContextItems.FlowContext))
                return (FlowContext)context.Items[ContextItems.FlowContext];

            if (context.Properties.CorrelationId == null)
                return null;

            if (!Guid.TryParse(context.Properties.CorrelationId, out var continuationID))
                return null;

            var flowStore = context.DependencyResolver.Resolve<IFlowStore>();

            var flowID = await flowStore.FindFlowID(continuationID);
            if (!flowID.HasValue)
                return null;

            var flowStateLock = await flowStore.LockFlowState(flowID.Value);

            var flowState = await flowStateLock.GetFlowState();
            if (flowState == null)
                return null;

            var flowContext = new FlowContext
            {
                MessageContext = context,

                FlowStateLock = flowStateLock,
                FlowState = flowState,

                ContinuationID = continuationID,
                ContinuationMetadata = flowState.Continuations.TryGetValue(continuationID, out var continuation) ? continuation : null
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Items.Add(ContextItems.FlowContext, flowContext);
            return flowContext;
        }
    }
}
