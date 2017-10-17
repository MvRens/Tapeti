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
        public async Task Handle(IMessageContext context, ConsumeResponse response)
        {
            object flowContextObj;
            if (!context.Items.TryGetValue(ContextItems.FlowContext, out flowContextObj))
                return;
            var flowContext = (FlowContext)flowContextObj;

            if (flowContext?.FlowStateLock != null)
            {
                if (response == ConsumeResponse.Nack)
                {
                    await flowContext.FlowStateLock.DeleteFlowState();
                }
                flowContext.FlowStateLock.Dispose();
            }
        }
    }
}
