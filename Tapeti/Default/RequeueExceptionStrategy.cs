using Tapeti.Config;

// ReSharper disable UnusedMember.Global

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
