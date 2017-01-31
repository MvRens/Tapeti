using System;
using System.Threading.Tasks;
using Tapeti.Config;
using Tapeti.Flow.FlowHelpers;

namespace Tapeti.Flow.Default
{
    public class FlowMessageMiddleware : IMessageMiddleware
    {
        public async Task Handle(IMessageContext context, Func<Task> next)
        {
            var flowContext = (FlowContext)context.Items[ContextItems.FlowContext];
            if (flowContext != null)
            {
                Newtonsoft.Json.JsonConvert.PopulateObject(flowContext.FlowState.Data, context.Controller);

                await next();

                flowContext.FlowState.Continuations.Remove(flowContext.ContinuationID);
            }
            else
                await next();
        }
    }
}
