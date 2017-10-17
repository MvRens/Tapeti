using System;
using Tapeti.Config;

namespace Tapeti.Default
{
    public class RequeueExceptionStrategy : IExceptionStrategy
    {
        public void HandleException(IExceptionStrategyContext context)
        {
            context.HandlingResult.ConsumeResponse = ConsumeResponse.Requeue;
        }
    }
}
