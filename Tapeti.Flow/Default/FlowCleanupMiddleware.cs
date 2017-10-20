using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tapeti.Config;

namespace Tapeti.Flow.Default
{
    public class FlowCleanupMiddleware : ICleanupMiddleware
    {
        public async Task Handle(IMessageContext context, HandlingResult handlingResult)
        {
            object flowContextObj;
            if (!context.Items.TryGetValue(ContextItems.FlowContext, out flowContextObj))
                return;
            var flowContext = (FlowContext)flowContextObj;

            if (flowContext?.FlowStateLock != null)
            {
                if (handlingResult.ConsumeResponse == ConsumeResponse.Nack 
                    || handlingResult.MessageAction == MessageAction.ErrorLog)
                {
                    await flowContext.FlowStateLock.DeleteFlowState();
                }
                flowContext.FlowStateLock.Dispose();
            }
        }
    }
}
