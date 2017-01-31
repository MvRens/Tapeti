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
            if (flowContext?.FlowState == null)
                return false;

            string continuation;
            if (!flowContext.FlowState.Continuations.TryGetValue(flowContext.ContinuationID, out continuation))
                return false;

            return continuation == MethodSerializer.Serialize(binding.Method);
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

            var flowStateID = await flowStore.FindFlowStateID(continuationID);
            if (!flowStateID.HasValue)
                return null;

            var flowStateLock = await flowStore.LockFlowState(flowStateID.Value);
            if (flowStateLock == null)
                return null;

            var flowState = await flowStateLock.GetFlowState();


            var flowMetadata = flowState != null ? Newtonsoft.Json.JsonConvert.DeserializeObject<FlowMetadata>(flowState.Metadata) : null;
            //var continuationMetaData = Newtonsoft.Json.JsonConvert.DeserializeObject<ContinuationMetadata>(continuation.MetaData);

            var flowContext = new FlowContext
            {
                MessageContext = context,
                ContinuationID = continuationID,
                FlowStateLock = flowStateLock,
                FlowState = flowState,
                Reply = flowMetadata?.Reply
            };

            // IDisposable items in the IMessageContext are automatically disposed
            context.Items.Add(ContextItems.FlowContext, flowContext);
            return flowContext;
        }
    }
}
