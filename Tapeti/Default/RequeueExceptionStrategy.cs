using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class RequeueExceptionStrategy : IExceptionStrategy
    {
        public ConsumeResponse HandleException(IMessageContext context, Exception exception)
        {
            // TODO log exception
            return ConsumeResponse.Requeue;
        }
    }
}
